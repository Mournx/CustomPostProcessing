using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TAA
{
    internal static class Jitter
    {
        static internal float GetHalton(int index, int radix)
        {
            float result = 0.0f;
            float fraction = 1.0f / radix;
            while (index > 0)
            {
                result += (index % radix) * fraction;

                index /= radix;
                fraction /= radix;
            }

            return result;
        }
        //get [-0.5, 0.5] jitter vector2
        static internal Vector2 CalculateJitter(int frameIndex)
        {
            float jitterX = GetHalton((frameIndex & 1023) + 1, 2) - 0.5f;
            float jitterY = GetHalton((frameIndex & 1023) + 1, 3) - 0.5f;

            return new Vector2(jitterX, jitterY);
        }

        static internal Matrix4x4 CalculateJitterProjectionMatrix(ref CameraData cameraData, float jitterScale = 1.0f)
        {
            Matrix4x4 mat = cameraData.GetProjectionMatrix();

            int frameIndex = Time.frameCount;

            float width = cameraData.camera.pixelWidth;
            float height = cameraData.camera.pixelHeight;

            Vector2 jitter = CalculateJitter(frameIndex) * jitterScale;

            mat.m02 += jitter.x * (2.0f / width);
            mat.m12 += jitter.y * (2.0f / height);

            return mat;
        }
    }

    [Serializable]
    internal class TAASettings
    {
        [SerializeField] internal float JitterScale = 1.0f;
    }

    [DisallowMultipleRendererFeature("TAA")]
    public class TAA : ScriptableRendererFeature
    {
        [SerializeField] private TAASettings mSettings = new TAASettings();

        private Shader mShader;
        private const string mShaderName = "Hidden/Anti-Aliasing/TAA";

        private TAAPass mTaaPass;
        private JitterPass mJitterPass;
        private Material mMaterial;
        
        public override void Create()
        {
            if (mJitterPass == null)
            {
                mJitterPass = new JitterPass();
                mJitterPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            }

            if (mTaaPass == null)
            {
                mTaaPass = new TAAPass();
                mTaaPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.postProcessEnabled)
            {
                if (!GetMaterials())
                {
                    Debug.LogErrorFormat("{0}.AddRenderPasses(): Missing material. {1} render pass will not be added.", GetType().Name, name);
                    return;
                }
                bool shouldAdd = mJitterPass.Setup(ref mSettings) && mTaaPass.Setup(ref mSettings, ref mMaterial);

                if (shouldAdd)
                {
                    renderer.EnqueuePass(mJitterPass);
                    renderer.EnqueuePass(mTaaPass);
                }
            }
        }

        private bool GetMaterials()
        {
            if (mShader == null)
                mShader = Shader.Find(mShaderName);
            if (mMaterial == null && mShader != null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShader);
            return mMaterial != null;
        }
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(mMaterial);
            
            mJitterPass?.Dispose();
            
            mTaaPass?.Dispose();
            mTaaPass = null;
        }
    }

    class JitterPass : ScriptableRenderPass
    {
        private TAASettings mSettings;

        private ProfilingSampler mProfilingSampler = new ProfilingSampler("Jitter");

        internal JitterPass()
        {
            mSettings = new TAASettings();
        }

        internal bool Setup(ref TAASettings settings)
        {
            mSettings = settings;

            return true;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            using (new ProfilingScope(cmd, mProfilingSampler))
            {
                cmd.SetViewProjectionMatrices(renderingData.cameraData.GetViewMatrix(),Jitter.CalculateJitterProjectionMatrix(ref renderingData.cameraData, mSettings.JitterScale));
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            
        }
    }

    class TAAPass : ScriptableRenderPass
    {
        private TAASettings mSettings;

        private Material mMaterial;

        private ProfilingSampler mProfilingSampler = new ProfilingSampler("TAA");
        private RenderTextureDescriptor mTAADescriptor;

        private RTHandle mSourceTexture;
        private RTHandle mDestinationTexture;

        private static readonly int mTaaAccumulationTexID = Shader.PropertyToID("_TaaAccumulationTexture"),
            mPrevViewProjMatrixID = Shader.PropertyToID("_LastViewProjMatrix"),
            mViewProjMatrixNoJitterID = Shader.PropertyToID("_ViewProjMatrixNoJitter");

        private Matrix4x4 mPrevViewProjMatrix, mViewProjMatrix;

        private RTHandle mAccumulationTexture;
        private RTHandle mTaaTemporaryTexture;

        private bool mResetHistoryFrames;

        private const string mAccumulationTextureName = "_TaaAccumulationTexture",
            mTaaTemporaryTextureName = "_TaaTemporaryTexture";
        internal TAAPass()
        {
            mSettings = new TAASettings();
        }
        internal bool Setup(ref TAASettings settings, ref Material material)
        {
            mSettings = settings;
            mMaterial = material;
            
            ConfigureInput(ScriptableRenderPassInput.Normal);
            return mMaterial != null;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            mTAADescriptor = renderingData.cameraData.cameraTargetDescriptor;
            mTAADescriptor.msaaSamples = 1;
            mTAADescriptor.depthBufferBits = 0;
            
            mMaterial.SetVector("_SourceSize", new Vector4(mTAADescriptor.width,mTAADescriptor.height,1.0f/mTAADescriptor.width,1.0f/mTAADescriptor.height));

            mResetHistoryFrames = RenderingUtils.ReAllocateIfNeeded(ref mAccumulationTexture, mTAADescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: mAccumulationTextureName);
            if (mResetHistoryFrames)
                mPrevViewProjMatrix = renderingData.cameraData.GetProjectionMatrix() * renderingData.cameraData.GetViewMatrix();
            
            RenderingUtils.ReAllocateIfNeeded(ref mTaaTemporaryTexture, mTAADescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: mTaaTemporaryTextureName);

            mViewProjMatrix = renderingData.cameraData.GetProjectionMatrix() * renderingData.cameraData.GetViewMatrix();
            
            var renderer = renderingData.cameraData.renderer;
            ConfigureTarget(renderer.cameraColorTargetHandle);
            ConfigureClear(ClearFlag.None, Color.white);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mMaterial == null)
            {
                Debug.LogErrorFormat("{0}.Execute(): Missing material. TAA pass will not execute. Check for missing reference in the renderer resources.", GetType().Name);
                return;
            }

            var cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            mSourceTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
            mDestinationTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;

            using (new ProfilingScope(cmd, mProfilingSampler))
            {
                cmd.SetGlobalTexture(mTaaAccumulationTexID, mAccumulationTexture);
                cmd.SetGlobalFloat("_FrameInfluence", mResetHistoryFrames ? 1.0f : 0.1f);
                cmd.SetGlobalMatrix(mPrevViewProjMatrixID, mPrevViewProjMatrix);
                cmd.SetGlobalMatrix(mViewProjMatrixNoJitterID, mViewProjMatrix);
                
                //TAA
                Blitter.BlitCameraTexture(cmd, mSourceTexture, mTaaTemporaryTexture, mMaterial, 0);
                
                //Copy History
                Blitter.BlitCameraTexture(cmd, mTaaTemporaryTexture, mAccumulationTexture);
                
                //Final Pass
                Blitter.BlitCameraTexture(cmd, mTaaTemporaryTexture, mDestinationTexture);

                mPrevViewProjMatrix = mViewProjMatrix;
                
                mResetHistoryFrames = false;
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
            mTaaTemporaryTexture?.Release();
            mTaaTemporaryTexture = null;
            
            mAccumulationTexture?.Release();
            mAccumulationTexture = null;
        }
    }
}