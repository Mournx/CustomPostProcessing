using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Printing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ScreenSpaceEffects
{
    [Serializable]
    internal class HBAOSettings
    {
        [SerializeField] internal float Intensity = 0.5f;
        [SerializeField] internal float Radius = 0.5f;
        [SerializeField] internal float MaxRadiusPixels = 32f;
        [SerializeField] internal float AngleBias = 0.1f;
    }

    [DisallowMultipleRendererFeature("HBAO")]
    public class HBAO : ScriptableRendererFeature
    {
        [SerializeField] private HBAOSettings mSettings = new HBAOSettings();

        private Shader mShader;
        private const string mShaderName = "Hidden/AO/HBAO";

        private HBAOPass mHBAOPass;
        private Material mMaterial;

        public override void Create()
        {
            if (mHBAOPass == null)
            {
                mHBAOPass = new HBAOPass();
                mHBAOPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
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

                bool shouldAdd = mHBAOPass.Setup(ref mSettings, ref mMaterial);
                
                if(shouldAdd)
                    renderer.EnqueuePass(mHBAOPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(mMaterial);

            mHBAOPass?.Dispose();
            mHBAOPass = null;
        }
        private bool GetMaterials()
        {
            if(mShader == null)
                mShader = Shader.Find(mShaderName);
            if (mMaterial == null && mShader != null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShader);
            return mMaterial != null;
        }

        class HBAOPass : ScriptableRenderPass
        {
            private HBAOSettings mSettings;

            private Material mMaterial;
            
            private ProfilingSampler mProfilingSampler = new ProfilingSampler("HBAO");

            private RenderTextureDescriptor mHBAODescriptor;

            private RTHandle mSourceTexture;
            private RTHandle mDestinationTexture;

            private static readonly int mProjectionParams2ID = Shader.PropertyToID("_ProjectionParams2"),
                mCameraViewTopLeftCornerID = Shader.PropertyToID("_CameraViewTopLeftCorner"),
                mCameraViewXExtentID = Shader.PropertyToID("_CameraViewXExtent"),
                mCameraViewYExtentID = Shader.PropertyToID("_CameraViewYExtent"),
                mHBAOParamsID = Shader.PropertyToID("_HBAOParams"),
                mRadiusPixelID = Shader.PropertyToID("_RadiusPixel"),
                mSourceSizeID = Shader.PropertyToID("_SourceSize"),
                mHBAOBlurRadiusID = Shader.PropertyToID("_HBAOBlurRadius");

            private RTHandle mHBAOTexture0, mHBAOTexture1;

            private const string mHBAOTexture0Name = "_HBAO_OcclusionTexture0",
                mHBAOTexture1Name = "_HBAO_OcclusionTexture1";

            internal HBAOPass()
            {
                mSettings = new HBAOSettings();
            }

            internal bool Setup(ref HBAOSettings settings, ref Material material)
            {
                mSettings = settings;
                mMaterial = material;
                
                ConfigureInput(ScriptableRenderPassInput.Normal);

                return mMaterial != null;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                mHBAODescriptor = renderingData.cameraData.cameraTargetDescriptor;
                mHBAODescriptor.msaaSamples = 1;
                mHBAODescriptor.depthBufferBits = 0;

                Matrix4x4 proj = renderingData.cameraData.GetProjectionMatrix();
                Matrix4x4 projInv = proj.inverse;

                Vector4 topLeftCorner = projInv.MultiplyPoint(new Vector4(-1.0f, 1.0f, -1.0f, 1.0f));
                Vector4 topRightCorner = projInv.MultiplyPoint(new Vector4(1.0f, 1.0f, -1.0f, 1.0f));
                Vector4 bottomLeftCorner = projInv.MultiplyPoint(new Vector4(-1.0f, -1.0f, -1.0f, 1.0f));

                Vector4 cameraXExtent = topRightCorner - topLeftCorner;
                Vector4 cameraYExtent = bottomLeftCorner - topLeftCorner;

                mMaterial.SetVector(mCameraViewTopLeftCornerID, topLeftCorner);
                mMaterial.SetVector(mCameraViewXExtentID, cameraXExtent);
                mMaterial.SetVector(mCameraViewYExtentID, cameraYExtent);
                mMaterial.SetVector(mProjectionParams2ID, new Vector4(1.0f/renderingData.cameraData.camera.nearClipPlane, renderingData.cameraData.worldSpaceCameraPos.x, renderingData.cameraData.worldSpaceCameraPos.y, renderingData.cameraData.worldSpaceCameraPos.z));

                var tanHalfFovY = Mathf.Tan(renderingData.cameraData.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                mMaterial.SetVector(mHBAOParamsID, new Vector4(mSettings.Intensity, mSettings.Radius * 1.5f, mSettings.MaxRadiusPixels, mSettings.AngleBias));
                mMaterial.SetFloat(mRadiusPixelID, renderingData.cameraData.camera.pixelHeight * mSettings.Radius * 1.5f / tanHalfFovY / 2.0f);

                RenderingUtils.ReAllocateIfNeeded(ref mHBAOTexture0, mHBAODescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: mHBAOTexture0Name);
                RenderingUtils.ReAllocateIfNeeded(ref mHBAOTexture1, mHBAODescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: mHBAOTexture1Name);
                
                ConfigureTarget(renderer.cameraColorTargetHandle);
                ConfigureClear(ClearFlag.None, Color.white);
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (mMaterial == null)
                {
                    Debug.LogErrorFormat("{0}.Execute(): Missing material. HBAO pass will not execute. Check for missing reference in the renderer resources.", GetType().Name);
                    return;
                }

                var cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                mSourceTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
                mDestinationTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;

                using (new ProfilingScope(cmd, mProfilingSampler))
                {
                    cmd.SetGlobalVector(mSourceSizeID,new Vector4(mHBAODescriptor.width, mHBAODescriptor.height, 1.0f/mHBAODescriptor.width, 1.0f/mHBAODescriptor.height));
                    
                    //HBAO
                    Blitter.BlitCameraTexture(cmd, mSourceTexture, mHBAOTexture0, mMaterial, 0);
                    
                    //Horizontal Blur
                    cmd.SetGlobalVector(mHBAOBlurRadiusID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                    Blitter.BlitCameraTexture(cmd, mHBAOTexture0, mHBAOTexture1, mMaterial,1);
                    
                    //Final Pass & Vertical Blur
                    cmd.SetGlobalVector(mHBAOBlurRadiusID, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
                    Blitter.BlitCameraTexture(cmd, mHBAOTexture1, mDestinationTexture, mMaterial, 2);
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
                mHBAOTexture0?.Release();
                mHBAOTexture1?.Release();
            }
        }
    }
    
}
