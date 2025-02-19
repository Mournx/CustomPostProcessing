using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCSS
{
    [Serializable]
    internal class PCSSSettings
    {
        [Range(4, 64)] public int findBlockerSampleCount = 16;
        [Range(4, 64)] public int pcfSampleCount = 16;
        
        //light Params
        public float AngularDiameter = 1.5f;
        public float blockerSearchAngularDiameter = 12;
        public float minFilterMaxAngularDiameter = 10;
        public float maxPenumbraSize = 0.56f;
        public float maxSamplingDistance = 0.5f;
        public float minFilterSizeTexels = 1.5f;
        public float blockerSamplingClumpExponent = 2f;
        //PenumbraMask Params
        [Range(1, 32)] public int penumbraMaskScale = 4;
    }
    [DisallowMultipleRendererFeature("PCSS")]
    public class PCSS : ScriptableRendererFeature
    {
        [SerializeField] private PCSSSettings mSettings = new PCSSSettings();

        private Shader mShader;
        private const string mShaderName = "Hidden/Soft-Shadow/PCSS";

        private PCSSPass mPcssPass;
        private Material mMaterial;
        
        public override void Create()
        {
            mPcssPass = new PCSSPass();
            mPcssPass.renderPassEvent = RenderPassEvent.AfterRenderingGbuffer;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GetMaterials())
            {
                Debug.LogErrorFormat("{0}.AddRenderPasses(): Missing material. {1} render pass will not be added.", GetType().Name, name);
                return;
            }
            
            bool shouldAdd = mPcssPass.Setup(ref renderingData, ref mSettings, ref mMaterial);
            if(shouldAdd)
                renderer.EnqueuePass(mPcssPass);
        }

        private bool GetMaterials()
        {
            if(mShader == null)
                mShader = Shader.Find(mShaderName);
            if (mMaterial == null && mShader != null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShader);
            return mMaterial != null;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(mMaterial);
            
            mPcssPass?.Dispose();
            mPcssPass = null;
        }
    }

    class PCSSPass : ScriptableRenderPass
    {
        private PCSSSettings mSettings;

        private Material mMaterial;

        private ProfilingSampler mProfilingSampler = new ProfilingSampler("PCSS");

        private RenderTextureDescriptor mPcssDescriptor;

        private RTHandle mSourceTexture, mDestinationTexture;
        private RTHandle penumbraMaskTex, penumbraMaskBlurTempTex;

        private const string penumbraMaskName = "_PenumbraMaskTex",
            penumbraMaskBlurTempName = "_PenumbraMaskBlurTempTex";

        private int colorAttachmentWidth, colorAttachmentHeight;
        private int frameIndex;

        private static readonly int shadowCascadeCount = 4;

        private struct PcssCascadeData
        {
            public Vector4 dirLightPcssParams0;
            public Vector4 dirLightPcssParams1;
        }

        private PcssCascadeData[] pcssCascadeDatas;

        static class ShaderConstants
        {
            public static readonly int _DirLightPcssParams0 = Shader.PropertyToID("_DirLightPcssParams0");
            public static readonly int _DirLightPcssParams1 = Shader.PropertyToID("_DirLightPcssParams1");
            public static readonly int _DirLightPcssProjs = Shader.PropertyToID("_DirLightPcssProjs");
            public static readonly int _ShadowTileTexelSize = Shader.PropertyToID("_ShadowTileTexelSize");
            
            public static readonly int _ColorAttachmentTexelSize = Shader.PropertyToID("_ColorAttachmentTexelSize");
            public static readonly int _PenumbraMaskTexelSize = Shader.PropertyToID("_PenumbraMaskTexelSize");
            public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            public static readonly int _PenumbraMaskTex = Shader.PropertyToID("_PenumbraMaskTex");

            public static readonly int _FindBlockerSampleCount = Shader.PropertyToID("_FindBlockerSampleCount");
            public static readonly int _PcfSampleCount = Shader.PropertyToID("_PcfSampleCount");
            public static readonly int _PcssTemporalFilter = Shader.PropertyToID("_PcssTemporalFilter");
        }

        internal PCSSPass()
        {
            mSettings = new PCSSSettings();
            pcssCascadeDatas = new PcssCascadeData[shadowCascadeCount];
            frameIndex = 0;
        }

        internal bool Setup(ref RenderingData renderingData, ref PCSSSettings settings, ref Material material)
        {
            mSettings = settings;
            mMaterial = material;

            mPcssDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            SetupPenumbraMask();

            return true;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mMaterial == null)
            {
                Debug.LogErrorFormat("{0}.Execute(): Missing material. PCSS pass will not execute. Check for missing reference in the renderer resources.", GetType().Name);
                return;
            }
            
            var cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            mSourceTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
            mDestinationTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
            using (new ProfilingScope(cmd, mProfilingSampler))
            {
                    PackDirLightParams(cmd);

                    int shadowTileResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                        renderingData.shadowData.mainLightShadowmapWidth,
                        renderingData.shadowData.mainLightShadowmapHeight,
                        renderingData.shadowData.mainLightShadowCascadesCount);
                    cmd.SetGlobalFloat(ShaderConstants._ShadowTileTexelSize, 1f/(float)shadowTileResolution);
                    cmd.SetGlobalFloat(ShaderConstants._FindBlockerSampleCount, mSettings.findBlockerSampleCount);
                    cmd.SetGlobalFloat(ShaderConstants._PcfSampleCount, mSettings.pcfSampleCount);

                    frameIndex = frameIndex >= 1024 ? 0 : frameIndex + 1;
                    cmd.SetGlobalFloat(ShaderConstants._PcssTemporalFilter, frameIndex);

                    RenderPenumbraMask(cmd);
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            mSourceTexture = null;
            mDestinationTexture = null;
        }

        public void Dispose()
        {
            penumbraMaskTex?.Release();
            penumbraMaskTex = null;
            penumbraMaskBlurTempTex?.Release();
            penumbraMaskBlurTempTex = null;
        }

        private void SetupPenumbraMask()
        {
            mPcssDescriptor.msaaSamples = 1;
            mPcssDescriptor.depthBufferBits = 0;
            mPcssDescriptor.colorFormat = RenderTextureFormat.R8;
            mPcssDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            mPcssDescriptor.depthStencilFormat = GraphicsFormat.None;
            mPcssDescriptor.autoGenerateMips = false;
            mPcssDescriptor.useMipMap = false;
            colorAttachmentWidth = mPcssDescriptor.width = (int)(mPcssDescriptor.width / mSettings.penumbraMaskScale);
            colorAttachmentHeight = mPcssDescriptor.height = (int)(mPcssDescriptor.height / mSettings.penumbraMaskScale);
            
            RenderingUtils.ReAllocateIfNeeded(ref penumbraMaskTex, mPcssDescriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: penumbraMaskName);
            RenderingUtils.ReAllocateIfNeeded(ref penumbraMaskBlurTempTex, mPcssDescriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: penumbraMaskBlurTempName);
        }

        private void RenderPenumbraMask(CommandBuffer cmd)
        {
            cmd.SetGlobalVector(ShaderConstants._ColorAttachmentTexelSize, new Vector4(1f / colorAttachmentWidth, 1f / colorAttachmentHeight, colorAttachmentWidth, colorAttachmentHeight));
            cmd.SetGlobalVector(ShaderConstants._PenumbraMaskTexelSize, new Vector4(1f/mPcssDescriptor.width, 1f/mPcssDescriptor.height, mPcssDescriptor.width,mPcssDescriptor.height));
            cmd.SetGlobalVector(ShaderConstants._BlitScaleBias, new Vector4(1,1,0,0));

            Blitter.BlitCameraTexture(cmd, mSourceTexture, penumbraMaskTex, mMaterial, 0);
            
            cmd.SetGlobalTexture(ShaderConstants._PenumbraMaskTex, penumbraMaskTex);
            Blitter.BlitCameraTexture(cmd, penumbraMaskTex, penumbraMaskBlurTempTex, mMaterial, 1);
            
            cmd.SetGlobalTexture(ShaderConstants._PenumbraMaskTex, penumbraMaskBlurTempTex);
            Blitter.BlitCameraTexture(cmd, penumbraMaskBlurTempTex, penumbraMaskTex, mMaterial, 2);
            
            cmd.SetGlobalTexture(ShaderConstants._PenumbraMaskTex, penumbraMaskTex);
        }

        private void PackDirLightParams(CommandBuffer cmd)
        {
            float angularDiameter = mSettings.AngularDiameter;
            float dirlightDepth2Radius = Mathf.Tan(0.5f * Mathf.Deg2Rad * angularDiameter);

            float minFilterAngularDiameter = Mathf.Max(mSettings.blockerSearchAngularDiameter, mSettings.minFilterMaxAngularDiameter);
            float halfMinFilterAngularDiameterTangent = Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Max(minFilterAngularDiameter, angularDiameter));

            float halfBlockerSearchAngularDiameterTangent = Mathf.Tan(0.5f * Mathf.Deg2Rad * Mathf.Max(mSettings.blockerSearchAngularDiameter, angularDiameter));

            for (int i = 0; i < shadowCascadeCount; i++)
            {
                //depth2RadialScale
                pcssCascadeDatas[i].dirLightPcssParams0.x = dirlightDepth2Radius;
                //radial2DepthScale
                pcssCascadeDatas[i].dirLightPcssParams0.y = 1.0f / pcssCascadeDatas[i].dirLightPcssParams0.x;
                //maxBlockerDistance
                pcssCascadeDatas[i].dirLightPcssParams0.z = mSettings.maxPenumbraSize / (2.0f * halfMinFilterAngularDiameterTangent);
                //maxSamplingDistance
                pcssCascadeDatas[i].dirLightPcssParams0.w = mSettings.maxSamplingDistance;
                //minFilterRadius(in texels)
                pcssCascadeDatas[i].dirLightPcssParams1.x = mSettings.minFilterSizeTexels;
                //minFilterRadial2DepthScale
                pcssCascadeDatas[i].dirLightPcssParams1.y = 1.0f / halfMinFilterAngularDiameterTangent;
                //blockerRdial2DepthScale
                pcssCascadeDatas[i].dirLightPcssParams1.z = 1.0f / halfBlockerSearchAngularDiameterTangent;
                //blockerClumpSampleExponent
                pcssCascadeDatas[i].dirLightPcssParams1.w = 0.5f * mSettings.blockerSamplingClumpExponent;
            }

            Vector4[] dirLightPcssParams0 = new Vector4[shadowCascadeCount];
            Vector4[] dirLightPcssParams1 = new Vector4[shadowCascadeCount];
            for (int i = 0; i < shadowCascadeCount; i++)
            {
                dirLightPcssParams0[i] = pcssCascadeDatas[i].dirLightPcssParams0;
                dirLightPcssParams1[i] = pcssCascadeDatas[i].dirLightPcssParams1;
            }

            cmd.SetGlobalVectorArray(ShaderConstants._DirLightPcssParams0, dirLightPcssParams0);
            cmd.SetGlobalVectorArray(ShaderConstants._DirLightPcssParams1, dirLightPcssParams1);
        }
    }
}