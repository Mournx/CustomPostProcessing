using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum CustomPostProcessEvent
{
    AfterOpaqueAndSky,
    BeforePostProcess,
    AfterPostProcess
}
public abstract class CustomPostProcessing : VolumeComponent,IPostProcessComponent,IDisposable
{
    protected Material mMaterial = null;
    private Material mCopyMaterial = null;

    private const string mCopyShaderName = "Hidden/CustomPostProcess/PostProcessCopy";
    public abstract bool IsActive();
    public virtual bool IsTileCompatible() => false;

    public virtual CustomPostProcessEvent evt => CustomPostProcessEvent.AfterPostProcess;
    public virtual int OrderInEvent => 0;

    private int mSourceTextureId = Shader.PropertyToID("_BlitTexture");

    protected override void OnEnable()
    {
        base.OnEnable();
        if (mCopyMaterial == null)
            mCopyMaterial = CoreUtils.CreateEngineMaterial(mCopyShaderName);
    }
    public abstract void Setup();
    public abstract void Render(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source,
        RTHandle destination);

    public virtual void Draw(CommandBuffer cmd, in RTHandle source, in RTHandle destination, int pass = -1)
    {
        cmd.SetGlobalTexture(mSourceTextureId,source);
        cmd.SetRenderTarget(destination,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        if(pass==-1||mMaterial==null)
            cmd.DrawProcedural(Matrix4x4.identity, mCopyMaterial,0,MeshTopology.Triangles,3);
        else cmd.DrawProcedural(Matrix4x4.identity, mMaterial,pass,MeshTopology.Triangles,3);
    }
    public void Dispose()
    {
       Dispose(true);
       GC.SuppressFinalize(this);
    }
    public virtual void Dispose(bool disposing){}
}
