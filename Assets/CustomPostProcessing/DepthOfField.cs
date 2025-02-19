using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CPP.EFFECTS
{
    public enum DepthOfFieldMode
    {
        Off,
        Gaussian,
        Bokeh
    }
    [VolumeComponentMenu("Custom Post-processing/Depth Of Field")]
    public class DepthOfField : CustomPostProcessing
    {
        public DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(DepthOfFieldMode.Off);
          
        [Tooltip("The distance at which the blurring will start.")]
        public MinFloatParameter gaussianStart = new MinFloatParameter(10f, 0f);
        [Tooltip("The distance at which the blurring will reach its maximum radius.")]
        public MinFloatParameter gaussianEnd = new MinFloatParameter(30f, 0f);
        [Tooltip("The maximum radius of the gaussian blur. Values above 1 may show under-sampling artifacts.")]
        public ClampedFloatParameter gaussianMaxRadius = new ClampedFloatParameter(1f, 0.5f, 1.5f);
        [Tooltip("Use higher quality sampling to reduce flickering and improve the overall blur smoothness.")]
        public BoolParameter highQualitySampling = new BoolParameter(false);
        [Tooltip("The distance to the point of focus.")]
        public MinFloatParameter focusDistance = new MinFloatParameter(10f, 0.1f);
        [Tooltip("The ratio of aperture (known as f-stop or f-number). The smaller the value is, the shallower the depth of field is.")]
        public ClampedFloatParameter aperture = new ClampedFloatParameter(5.6f, 1f, 32f);
        [Tooltip("The distance between the lens and the film. The larger the value is, the shallower the depth of field is.")]
        public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, 1f, 300f);
        [Tooltip("The number of aperture blades.")]
        public ClampedIntParameter bladeCount = new ClampedIntParameter(5, 3, 9);
        [Tooltip("The curvature of aperture blades. The smaller the value is, the more visible aperture blades are. A value of 1 will make the bokeh perfectly circular.")]
        public ClampedFloatParameter bladeCurvature = new ClampedFloatParameter(1f, 0f, 1f);
        [Tooltip("The rotation of aperture blades in degrees.")]
        public ClampedFloatParameter bladeRotation = new ClampedFloatParameter(0f, -180f, 180f);

        /// <inheritdoc/>
        public override bool IsActive()
        {
            if (mode.value == DepthOfFieldMode.Off || SystemInfo.graphicsShaderLevel < 35)
                return false;

            return mode.value != DepthOfFieldMode.Gaussian || SystemInfo.supportedRenderTargetCount > 1;
        }

        private const string mShaderName1 = "Hidden/CustomPostProcess/GaussianDepthOfField",
            mShaderName2 = "Hidden/CustomPostProcess/BokehDepthOfField";
        
        RTHandle m_FullCoCTexture;
        RTHandle m_HalfCoCTexture;
        RTHandle m_PingTexture;
        RTHandle m_PongTexture;
        RenderTargetIdentifier[] m_MRT2;
        Vector4[] m_BokehKernel;
        int m_BokehHash;
        // Needed if the device changes its render target width/height (ex, Mobile platform allows change of orientation)
        float m_BokehMaxRadius;
        float m_BokehRCPAspect;

        public override CustomPostProcessEvent evt => CustomPostProcessEvent.AfterPostProcess;
        public override int OrderInEvent => 90;

        private static readonly  int _CoCParams = Shader.PropertyToID("_CoCParams"),
            _DownSampleScaleFactor = Shader.PropertyToID("_DownSampleScaleFactor"),
            _FullCoCTexture = Shader.PropertyToID("_FullCoCTexture"),
            _HalfCoCTexture = Shader.PropertyToID("_HalfCoCTexture"),
            _ColorTexture = Shader.PropertyToID("_ColorTexture"),
            _DofTexture = Shader.PropertyToID("_DofTexture"),
            _BokehConstants = Shader.PropertyToID("_BokehConstants"),
            _BokehKernel = Shader.PropertyToID("_BokehKernel");
            

        public override void Setup()
        {
            if (mMaterial == null)
            {
                if (mode.value == DepthOfFieldMode.Gaussian)
                    mMaterial = CoreUtils.CreateEngineMaterial(mShaderName1);
                else if (mode.value == DepthOfFieldMode.Bokeh)
                    mMaterial = CoreUtils.CreateEngineMaterial(mShaderName2);
            }

            m_MRT2 = new RenderTargetIdentifier[2];
        }

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            if (mode.value == DepthOfFieldMode.Gaussian)
                DoGaussianDepthOfField(cmd, ref renderingData, source, destination);
            else if (mode.value == DepthOfFieldMode.Bokeh)
                DoBokehDepthOfField(cmd, ref renderingData, source, destination);
        }

        private void DoGaussianDepthOfField(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            int downSample = 2;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            int wh = descriptor.width / downSample;
            int hh = descriptor.height / downSample;
            float farStart = gaussianStart.value;
            float farEnd = Mathf.Max(farStart, gaussianEnd.value);
            // Assumes a radius of 1 is 1 at 1080p
            // Past a certain radius our gaussian kernel will look very bad so we'll clamp it for
            // very high resolutions (4K+).
            float maxRadius = gaussianMaxRadius.value * (wh / 1080f);
            maxRadius = Mathf.Min(maxRadius, 2f);

            CoreUtils.SetKeyword(mMaterial, ShaderKeywordStrings.HighQualitySampling, highQualitySampling.value);
            mMaterial.SetVector(_CoCParams, new Vector3(farStart, farEnd, maxRadius));

            descriptor.graphicsFormat = GraphicsFormat.R16_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref m_FullCoCTexture, descriptor, name: "_FullCoCTexture");
            descriptor.width = wh;
            descriptor.height = hh;
            RenderingUtils.ReAllocateIfNeeded(ref m_HalfCoCTexture, descriptor, name: "_HalfCoCTexture");
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref m_PingTexture, descriptor, name: "_PingTexture");
            RenderingUtils.ReAllocateIfNeeded(ref m_PongTexture, descriptor, name: "_PongTexture");
            
            cmd.SetGlobalVector(_DownSampleScaleFactor, new Vector4(1.0f / downSample, 1.0f / downSample, downSample, downSample));
            
            // Compute CoC
            Blitter.BlitCameraTexture(cmd, source, m_FullCoCTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 0);

            // Downscale & prefilter color + coc
            m_MRT2[0] = m_HalfCoCTexture.nameID;
            m_MRT2[1] = m_PingTexture.nameID;

            cmd.SetGlobalTexture(_FullCoCTexture, m_FullCoCTexture.nameID);
            CoreUtils.SetRenderTarget(cmd, m_MRT2, m_HalfCoCTexture);
            Vector2 viewportScale = source.useScaling ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y) : Vector2.one;
            Blitter.BlitTexture(cmd, source, viewportScale, mMaterial, 1);

            // Blur
            cmd.SetGlobalTexture(_HalfCoCTexture, m_HalfCoCTexture.nameID);
            Blitter.BlitCameraTexture(cmd, m_PingTexture, m_PongTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 2);
            Blitter.BlitCameraTexture(cmd, m_PongTexture, m_PingTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 3);

            // Composite
            cmd.SetGlobalTexture(_ColorTexture, m_PingTexture.nameID);
            Blitter.BlitCameraTexture(cmd, source, destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 4);
        }
        
        void PrepareBokehKernel(float maxRadius, float rcpAspect)
        {
            const int kRings = 4;
            const int kPointsPerRing = 7;

            // Check the existing array
            if (m_BokehKernel == null)
                m_BokehKernel = new Vector4[42];

            // Fill in sample points (concentric circles transformed to rotated N-Gon)
            int idx = 0;
            float BladeCount = bladeCount.value;
            float curvature = 1f - bladeCurvature.value;
            float rotation = bladeRotation.value * Mathf.Deg2Rad;
            const float PI = Mathf.PI;
            const float TWO_PI = Mathf.PI * 2f;

            for (int ring = 1; ring < kRings; ring++)
            {
                float bias = 1f / kPointsPerRing;
                float radius = (ring + bias) / (kRings - 1f + bias);
                int points = ring * kPointsPerRing;

                for (int point = 0; point < points; point++)
                {
                    // Angle on ring
                    float phi = 2f * PI * point / points;

                    // Transform to rotated N-Gon
                    // Adapted from "CryEngine 3 Graphics Gems" [Sousa13]
                    float nt = Mathf.Cos(PI / BladeCount);
                    float dt = Mathf.Cos(phi - (TWO_PI / BladeCount) * Mathf.Floor((BladeCount * phi + Mathf.PI) / TWO_PI));
                    float r = radius * Mathf.Pow(nt / dt, curvature);
                    float u = r * Mathf.Cos(phi - rotation);
                    float v = r * Mathf.Sin(phi - rotation);

                    float uRadius = u * maxRadius;
                    float vRadius = v * maxRadius;
                    float uRadiusPowTwo = uRadius * uRadius;
                    float vRadiusPowTwo = vRadius * vRadius;
                    float kernelLength = Mathf.Sqrt((uRadiusPowTwo + vRadiusPowTwo));
                    float uRCP = uRadius * rcpAspect;

                    m_BokehKernel[idx] = new Vector4(uRadius, vRadius, kernelLength, uRCP);
                    idx++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float GetMaxBokehRadiusInPixels(float viewportHeight)
        {
            // Estimate the maximum radius of bokeh (empirically derived from the ring count)
            const float kRadiusInPixels = 14f;
            return Mathf.Min(0.05f, kRadiusInPixels / viewportHeight);
        }

        private void DoBokehDepthOfField(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            int downSample = 2;
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            int wh = descriptor.width / downSample;
            int hh = descriptor.height / downSample;

            // "A Lens and Aperture Camera Model for Synthetic Image Generation" [Potmesil81]
            float F = focalLength.value / 1000f;
            float A = focalLength.value / aperture.value;
            float P = focusDistance.value;
            float maxCoC = (A * F) / (P - F);
            float maxRadius = GetMaxBokehRadiusInPixels(descriptor.height);
            float rcpAspect = 1f / (wh / (float)hh);
            
            cmd.SetGlobalVector(_CoCParams, new Vector4(P, maxCoC, maxRadius, rcpAspect));

            // Prepare the bokeh kernel constant buffer
            int hash = GetHashCode();
            if (hash != m_BokehHash || maxRadius != m_BokehMaxRadius || rcpAspect != m_BokehRCPAspect)
            {
                m_BokehHash = hash;
                m_BokehMaxRadius = maxRadius;
                m_BokehRCPAspect = rcpAspect;
                PrepareBokehKernel(maxRadius, rcpAspect);
            }

            cmd.SetGlobalVectorArray(_BokehKernel, m_BokehKernel);

            descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref m_FullCoCTexture, descriptor, name: "_FullCoCTexture");
            descriptor.width = wh;
            descriptor.height = hh;
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref m_PingTexture, descriptor, name: "_PingTexture");
            RenderingUtils.ReAllocateIfNeeded(ref m_PongTexture, descriptor, name: "_PongTexture");
            
            cmd.SetGlobalVector(_DownSampleScaleFactor, new Vector4(1.0f / downSample, 1.0f / downSample, downSample, downSample));
            float uvMargin = (1.0f / descriptor.height);
            cmd.SetGlobalVector(_BokehConstants, new Vector4(uvMargin, uvMargin * 2.0f));

            // Compute CoC
            Blitter.BlitCameraTexture(cmd, source, m_FullCoCTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 0);
            cmd.SetGlobalTexture(_FullCoCTexture, m_FullCoCTexture.nameID);

            // Downscale & prefilter color + coc
            Blitter.BlitCameraTexture(cmd, source, m_PingTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 1);

            // Bokeh blur
            Blitter.BlitCameraTexture(cmd, m_PingTexture, m_PongTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 2);

            // Post-filtering
            Blitter.BlitCameraTexture(cmd, m_PongTexture, m_PingTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 3);

            // Composite
            cmd.SetGlobalTexture(_DofTexture, m_PingTexture.nameID);
            Blitter.BlitCameraTexture(cmd, source, destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, mMaterial, 4);
        }
    }
    [Serializable]
    public sealed class DepthOfFieldModeParameter : VolumeParameter<DepthOfFieldMode>
    {
        /// <summary>
        /// Creates a new <see cref="DepthOfFieldModeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public DepthOfFieldModeParameter(DepthOfFieldMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}