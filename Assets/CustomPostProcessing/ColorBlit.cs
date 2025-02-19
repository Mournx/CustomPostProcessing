using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CPP.EFFECTS
{
    [VolumeComponentMenu("Custom Post-processing/Color Blit")]
    public class ColorBlit : CustomPostProcessing
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 2.0f);
        
        private const string mShaderName = "Hidden/CustomPostProcess/ColorBlit";
        public override bool IsActive() => mMaterial != null && intensity.value > 0;
        public override CustomPostProcessEvent evt => CustomPostProcessEvent.AfterOpaqueAndSky;
        public override int OrderInEvent => 0;

        public override void Setup()
        {
            if (mMaterial == null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShaderName);
        }
        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            if (mMaterial == null) return;
            mMaterial.SetFloat("_Intensity",intensity.value);
            Draw(cmd,source,destination,0);
        }
        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            CoreUtils.Destroy(mMaterial);
        }
    }
}