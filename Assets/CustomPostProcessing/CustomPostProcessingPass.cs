using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomPostProcessingPass : ScriptableRenderPass
{
    private List<CustomPostProcessing> mCustomPostProcessings;
    private List<int> mActiveIndex;

    private string mProfilerTag;
    private List<ProfilingSampler> mProfilingSamplers;

    private RTHandle mSourceRT;
    private RTHandle mDestinationRT;
    private RTHandle mTempRT0;
    private RTHandle mTempRT1;

    private string mTempRT0Name => "_TemporaryRenderTexture0";
    private string mTempRT1Name => "_TemporaryRenderTexture1";

    public CustomPostProcessingPass(string profilerTag, List<CustomPostProcessing> customPostProcessings)
    {
        mProfilerTag = profilerTag;
        mCustomPostProcessings = customPostProcessings;
        mActiveIndex = new List<int>(customPostProcessings.Count);
        mProfilingSamplers = customPostProcessings.Select(c => new ProfilingSampler(c.ToString())).ToList();

        mTempRT0 = RTHandles.Alloc(mTempRT0Name, name: mTempRT0Name);
        mTempRT1 = RTHandles.Alloc(mTempRT0Name, name: mTempRT0Name);
    }

    public bool SetupCustomPostProcessing()
    {
        mActiveIndex.Clear();
        for (int i = 0; i < mCustomPostProcessings.Count; i++)
        {
            mCustomPostProcessings[i].Setup();
            if(mCustomPostProcessings[i].IsActive())
                mActiveIndex.Add(i);
        }

        return mActiveIndex.Count != 0;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.msaaSamples = 1;
        descriptor.depthBufferBits = 0;

        RenderingUtils.ReAllocateIfNeeded(ref mTempRT0, descriptor, name: mTempRT0Name);
        RenderingUtils.ReAllocateIfNeeded(ref mTempRT1, descriptor, name: mTempRT1Name);
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get(mProfilerTag);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        
        bool RT1Used = false;

        mDestinationRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
        mSourceRT = renderingData.cameraData.renderer.cameraColorTargetHandle;

        if (mActiveIndex.Count == 1)
        {
            int index = mActiveIndex[0];
            using (new ProfilingScope(cmd, mProfilingSamplers[index]))
            {
                mCustomPostProcessings[index].Render(cmd,ref renderingData,mSourceRT,mTempRT0);
            }
        }
        else
        {
            RT1Used = true;
            Blitter.BlitCameraTexture(cmd,mSourceRT,mTempRT0);
            
            for (int i = 0; i < mActiveIndex.Count; i++)
            {
                int index = mActiveIndex[i];
                var customPostProcessing = mCustomPostProcessings[index];
                using (new ProfilingScope(cmd, mProfilingSamplers[index]))
                {
                    customPostProcessing.Render(cmd,ref renderingData,mTempRT0,mTempRT1);
                }
                CoreUtils.Swap(ref mTempRT0,ref mTempRT1);
            }
        }
        Blitter.BlitCameraTexture(cmd,mTempRT0,mDestinationRT);
        
        cmd.ReleaseTemporaryRT(Shader.PropertyToID(mTempRT0.name));
        if(RT1Used)cmd.ReleaseTemporaryRT(Shader.PropertyToID(mTempRT1.name));
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        mSourceRT?.Release();
        mDestinationRT?.Release();
    }
}
