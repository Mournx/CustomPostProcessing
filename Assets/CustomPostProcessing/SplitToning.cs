using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CPP.EFFECTS
{
    [VolumeComponentMenu("Custom Post-processing/Split Toning")]
    public class SplitToning : CustomPostProcessing
    {
        [Tooltip("The color to use for shadows.")]
        public ColorParameter shadows = new ColorParameter(Color.grey, false, false, true);
        [Tooltip("The color to use for highlights.")]
        public ColorParameter highlights = new ColorParameter(Color.grey, false, false, true);
        [Tooltip("Balance between the colors in the highlights and shadows.")]
        public ClampedFloatParameter balance = new ClampedFloatParameter(0f, -100f, 100f);

        public override CustomPostProcessEvent evt => CustomPostProcessEvent.AfterPostProcess;
        public override int OrderInEvent => 97;
        public override bool IsActive() => shadows != Color.grey || highlights != Color.grey;

        private const string mShaderName = "Hidden/CustomPostProcess/SplitToning";

        private static readonly int _SplitShadows = Shader.PropertyToID("_SplitShadows"),
            _SplitHighlights = Shader.PropertyToID("_SplitHighlights");
        public override void Setup()
        {
            if (mMaterial == null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShaderName);
        }

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            Vector4 Shadows = shadows.value, Highlights = highlights.value;
            Shadows.w = balance.value / 100.0f; 
            Highlights.w = 0.0f;
            
            mMaterial.SetVector(_SplitShadows,Shadows);
            mMaterial.SetVector(_SplitHighlights,Highlights);

            Draw(cmd, source, destination, 0);
        }
    }
}