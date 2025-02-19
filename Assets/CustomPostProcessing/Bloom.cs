using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CPP.EFFECTS
{
    public enum BloomDownscaleMode
    {
        Half,
        Quarter
    }
    [VolumeComponentMenu("Custom Post-processing/Bloom")]
    public class Bloom : CustomPostProcessing
    {
        [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        [Tooltip("Strength of the bloom filter.")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        [Tooltip("Set the radius of the bloom effect.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        [Tooltip("Set the maximum intensity that Unity uses to calculate Bloom. If pixels in your Scene are more intense than this, URP renders them at their current intensity, but uses this intensity value for the purposes of Bloom calculations.")]
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);
        [Tooltip("Use the color picker to select a color for the Bloom effect to tint to.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);
        [Tooltip("Use bicubic sampling instead of bilinear sampling for the upsampling passes. This is slightly more expensive but helps getting smoother visuals.")]
        public BoolParameter highQualityFiltering = new BoolParameter(false);
        [Tooltip("The starting resolution that this effect begins processing."), AdditionalProperty]
        public DownscaleParameter downscale = new DownscaleParameter(BloomDownscaleMode.Half);
        [Tooltip("The maximum number of iterations in the effect processing sequence."), AdditionalProperty]
        public ClampedIntParameter maxIterations = new ClampedIntParameter(6, 2, 8);
        [Tooltip("Dirtiness texture to add smudges or dust to the bloom effect.")]
        public TextureParameter dirtTexture = new TextureParameter(null);
        [Tooltip("Amount of dirtiness.")]
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);

        private RTHandle[] m_BloomMipUp, m_BloomMipDown;

        private const int k_MaxPyramidSize = 16;
        
        public static int[] _BloomMipUp;
        public static int[] _BloomMipDown;

        private static readonly int mBloomParamsId = Shader.PropertyToID("_BloomParams"),
            _SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip"),
            mBloomParams2Id = Shader.PropertyToID("_BloomParams2"),
            _BloomTexture = Shader.PropertyToID("_BloomTexture"),
            mLensDirtParamsId = Shader.PropertyToID("_LensDirtParams"),
            mLensDirtIntensityId = Shader.PropertyToID("_LensDirtIntensity"),
            _LensDirtTexture = Shader.PropertyToID("_LensDirtTexture");
            
            
            
        private const string mShaderName = "Hidden/CustomPostProcess/Bloom";
        public override CustomPostProcessEvent evt => CustomPostProcessEvent.AfterPostProcess;
        public override int OrderInEvent => 91;
        public override bool IsActive() => mMaterial != null && (intensity.value > 0.0f);

        public override void Setup()
        {
            if (mMaterial == null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShaderName);

            _BloomMipDown = new int[k_MaxPyramidSize];
            _BloomMipUp = new int[k_MaxPyramidSize];
            m_BloomMipUp = new RTHandle[k_MaxPyramidSize];
            m_BloomMipDown = new RTHandle[k_MaxPyramidSize];
            
            for (int i = 0; i < k_MaxPyramidSize; i++)
            {
                _BloomMipUp[i] = Shader.PropertyToID("_BloomMipUp" + i);
                _BloomMipDown[i] = Shader.PropertyToID("_BloomMipDown" + i);
                // Get name, will get Allocated with descriptor later
                m_BloomMipUp[i] = RTHandles.Alloc(_BloomMipUp[i], name: "_BloomMipUp" + i);
                m_BloomMipDown[i] = RTHandles.Alloc(_BloomMipDown[i], name: "_BloomMipDown" + i);
            }
        }

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            if (mMaterial == null) return;
            int downres = 1;
            switch (downscale.value)
            {
                case BloomDownscaleMode.Half:
                    downres = 1;
                    break;
                case BloomDownscaleMode.Quarter:
                    downres = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            int tw = descriptor.width >> downres;
            int th = descriptor.height >> downres;
            //Determine the iteration count
            int maxSize=Mathf.Max(tw,th);
            int iterations=Mathf.FloorToInt(Mathf.Log(maxSize,2f)-1);
            int mipCount=Mathf.Clamp(iterations,1,maxIterations.value);
            //Pre-filteringparameters
            float Clamp=clamp.value;
            float Threshold=Mathf.GammaToLinearSpace(threshold.value);
            float thresholdKnee=Threshold*0.5f;//Hardcodedsoftknee

            //Materialsetup
            float Scatter=Mathf.Lerp(0.05f,0.95f,scatter.value);
            mMaterial.SetVector(mBloomParamsId,new Vector4(Scatter,Clamp,Threshold,thresholdKnee));
            CoreUtils.SetKeyword(mMaterial,ShaderKeywordStrings.BloomHQ,highQualityFiltering.value);
            //Prefilter
            for(int i=0;i<mipCount;i++)
            {
                RenderingUtils.ReAllocateIfNeeded(ref m_BloomMipUp[i],descriptor,name:m_BloomMipUp[i].name);
                RenderingUtils.ReAllocateIfNeeded(ref m_BloomMipDown[i],descriptor,name:m_BloomMipDown[i].name);
                descriptor.width=Mathf.Max(1,descriptor.width>>1);
                descriptor.height=Mathf.Max(1,descriptor.height>>1);
            }
            Blitter.BlitCameraTexture(cmd,source,m_BloomMipDown[0],RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,mMaterial,0);
            //Downsample-gaussian pyramid
            var lastDown=m_BloomMipDown[0];
            for(int i=1;i<mipCount;i++)
            {
                //Classic two pass gaussian blur-use mipUp as a temporary target
                //First pass does 2xdownsampling + 9-tap gaussian
                //Second pass does 9-tap gaussian using a 5-tap filter + bilinear filtering
                Blitter.BlitCameraTexture(cmd,lastDown,m_BloomMipUp[i],RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,mMaterial,1);
                Blitter.BlitCameraTexture(cmd,m_BloomMipUp[i],m_BloomMipDown[i],RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,mMaterial,2);

                lastDown = m_BloomMipDown[i];
            }

            //Upsample (bilinear by default,HQfiltering does bicubic instead
            for(int i=mipCount-2;i>=0;i--)
            {
                var lowMip=(i==mipCount-2)?m_BloomMipDown[i+1]:m_BloomMipUp[i+1];
                var highMip=m_BloomMipDown[i];
                var dst=m_BloomMipUp[i];

                cmd.SetGlobalTexture(_SourceTexLowMip,lowMip);
                Blitter.BlitCameraTexture(cmd,highMip,dst,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,mMaterial,3);
            }

            var Tint=tint.value.linear;
            var luma=ColorUtils.Luminance(Tint);
            Tint=luma>0f?Tint*(1f/luma):Color.white;

            var bloomParams=new Vector4(intensity.value,Tint.r,Tint.g,Tint.b);
            mMaterial.SetVector(mBloomParams2Id,bloomParams);

            cmd.SetGlobalTexture(_BloomTexture,m_BloomMipUp[0]);

            //Setup lens dirtiness
            //Keep the aspect ratio correct&center the dirt texture,we don't want it to be
            //stretch 
            var DirtTexture=dirtTexture.value==null?Texture2D.blackTexture:dirtTexture.value;
            float dirtRatio=DirtTexture.width/(float)DirtTexture.height;
            float screenRatio=descriptor.width/(float)descriptor.height;
            var dirtScaleOffset=new Vector4(1f,1f,0f,0f);
            float DirtIntensity=dirtIntensity.value;

            if(dirtRatio>screenRatio)
            {
                dirtScaleOffset.x=screenRatio/dirtRatio;
                dirtScaleOffset.z=(1f-dirtScaleOffset.x)*0.5f;
            }
            else if(screenRatio>dirtRatio)
            {
                dirtScaleOffset.y=dirtRatio/screenRatio;
                dirtScaleOffset.w=(1f-dirtScaleOffset.y)*0.5f;
            }

            mMaterial.SetVector(mLensDirtParamsId,dirtScaleOffset);
            mMaterial.SetFloat(mLensDirtIntensityId,DirtIntensity);
            mMaterial.SetTexture(_LensDirtTexture,DirtTexture);

            if(highQualityFiltering.value)
              mMaterial.EnableKeyword(DirtIntensity>0f?ShaderKeywordStrings.BloomHQDirt:ShaderKeywordStrings.BloomHQ);
            else
              mMaterial.EnableKeyword(DirtIntensity>0f?ShaderKeywordStrings.BloomLQDirt:ShaderKeywordStrings.BloomLQ);

            Blitter.BlitCameraTexture(cmd,source,destination,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store,mMaterial,4);

        }
    }
    [Serializable]
    public sealed class DownscaleParameter : VolumeParameter<BloomDownscaleMode>
    {
        /// <summary>
        /// Creates a new <see cref="UnityEngine.Rendering.Universal.DownscaleParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public DownscaleParameter(BloomDownscaleMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}