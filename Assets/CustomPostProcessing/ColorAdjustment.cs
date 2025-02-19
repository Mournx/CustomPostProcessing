using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CPP.EFFECTS
{
    [VolumeComponentMenu("Custom Post-processing/Color Adjustment")]
    public class ColorAdjustment : CustomPostProcessing
    {
        public FloatParameter postExposure = new FloatParameter(0.0f);//后曝光
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0.0f, -100.0f, 100.0f);//对比度
        public ColorParameter colorFilter = new ColorParameter(Color.white, true, false, false);//颜色滤镜
        public ClampedFloatParameter hueShift = new ClampedFloatParameter(0.0f, -180.0f, 180.0f);//色相偏移
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0.0f, -100.0f, 100.0f);//饱和度
        
        private const string mShaderName = "Hidden/CustomPostProcess/ColorAdjustments";

        public override bool IsActive() => mMaterial != null && (IsPostExposureActive() || IsContrastActive() ||
                                                                 IsColorFilterActive() || IsHueShiftActive() ||
                                                                 IsSaturationActive());
        private bool IsPostExposureActive() => postExposure.value != 0.0f;
        private bool IsContrastActive() => contrast.value != 0.0f;
        private bool IsColorFilterActive() => colorFilter.value != Color.white;
        private bool IsHueShiftActive() => hueShift.value != 0.0f;
        private bool IsSaturationActive() => saturation.value != 0.0f;

        public override CustomPostProcessEvent evt => CustomPostProcessEvent.AfterPostProcess;
        public override int OrderInEvent => 99;

        private int mColorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
            mColorFilterId = Shader.PropertyToID("_ColorFilter");

        private const string mExposureKeyword = "EXPOSURE",
            mContrastKeyword = "CONTRAST",
            mHueShiftKeyword = "HUE_SHIFT",
            mSaturationKeyword = "SATURATION",
            mColorFilterKeyword = "COLOR_FILTER";
        public override void Setup()
        {
            if (mMaterial == null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShaderName);
        }
        
        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            if (mMaterial == null) return;
            Vector4 colorAdjustmentsVector4 = new Vector4(
                Mathf.Pow(2f,postExposure.value),//曝光度，曝光单位是2的幂次
                contrast.value*0.01f+1f,//对比度，从[-100,100]映射到[0,2]
                hueShift.value*(1.0f/360.0f),//色相偏移，从[-180,180]映射到[-0.5,0.5]
                saturation.value*0.01f+1.0f);//饱和度，从[-100,100]映射到[0,2]
            mMaterial.SetVector(mColorAdjustmentsId,colorAdjustmentsVector4);
            mMaterial.SetColor(mColorFilterId,colorFilter.value);
            
            SetupKeyWord(mExposureKeyword,IsPostExposureActive());
            SetupKeyWord(mContrastKeyword,IsContrastActive());
            SetupKeyWord(mHueShiftKeyword,IsHueShiftActive());
            SetupKeyWord(mSaturationKeyword,IsSaturationActive());
            SetupKeyWord(mColorFilterKeyword,IsColorFilterActive());
            
            Draw(cmd, source, destination, 0);
        }

        private void SetupKeyWord(string keyword, bool enabled = true)
        {
            if(enabled)mMaterial.EnableKeyword(keyword);
            else mMaterial.DisableKeyword(keyword);
        }

        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            CoreUtils.Destroy(mMaterial);
        }
    }

}