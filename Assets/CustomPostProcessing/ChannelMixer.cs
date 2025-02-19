using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CPP.EFFECTS
{
    [VolumeComponentMenu("Custom Post-processing/Channel Mixer")]
    public class ChannelMixer : CustomPostProcessing
    {
        public ClampedFloatParameter redOutRedIn = new ClampedFloatParameter(100f, -200f, 200f);
        public ClampedFloatParameter redOutGreenIn = new ClampedFloatParameter(0f, -200f, 200f);
        public ClampedFloatParameter redOutBlueIn = new ClampedFloatParameter(0f, -200f, 200f);
        public ClampedFloatParameter greenOutRedIn = new ClampedFloatParameter(0f, -200f, 200f);
        public ClampedFloatParameter greenOutGreenIn = new ClampedFloatParameter(100f, -200f, 200f);
        public ClampedFloatParameter greenOutBlueIn = new ClampedFloatParameter(0f, -200f, 200f);
        public ClampedFloatParameter blueOutRedIn = new ClampedFloatParameter(0f, -200f, 200f);
        public ClampedFloatParameter blueOutGreenIn = new ClampedFloatParameter(0f, -200f, 200f);
        public ClampedFloatParameter blueOutBlueIn = new ClampedFloatParameter(100f, -200f, 200f);

        private const string mShaderName = "Hidden/CustomPostProcess/ChannelMixer";
        public override CustomPostProcessEvent evt => CustomPostProcessEvent.AfterPostProcess;
        public override int OrderInEvent => 98;

        /// <inheritdoc/>
        public override bool IsActive()
        {
            return mMaterial!=null&&(redOutRedIn.value != 100f
                || redOutGreenIn.value != 0f
                || redOutBlueIn.value != 0f
                || greenOutRedIn.value != 0f
                || greenOutGreenIn.value != 100f
                || greenOutBlueIn.value != 0f
                || blueOutRedIn.value != 0f
                || blueOutGreenIn.value != 0f
                || blueOutBlueIn.value != 100f);
        }
        public override void Setup()
        {
            if (mMaterial == null)
                mMaterial = CoreUtils.CreateEngineMaterial(mShaderName);
        }

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, RTHandle source, RTHandle destination)
        {
            Vector4 channelMixerR = new Vector4(redOutRedIn.value / 100.0f, redOutGreenIn.value / 100.0f,
                redOutBlueIn.value / 100.0f, 0.0f);
            Vector4 channelMixerG = new Vector4(greenOutRedIn.value / 100.0f, greenOutGreenIn.value / 100.0f,
                greenOutBlueIn.value / 100.0f, 0.0f);
            Vector4 channelMixerB = new Vector4(blueOutRedIn.value / 100.0f, blueOutGreenIn.value / 100.0f,
                blueOutBlueIn.value / 100.0f);
            
            mMaterial.SetVector("_ChannelMixerR",channelMixerR);
            mMaterial.SetVector("_ChannelMixerG",channelMixerG);
            mMaterial.SetVector("_ChannelMixerB",channelMixerB);
            
            Draw(cmd,source,destination,0);
        }
        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            CoreUtils.Destroy(mMaterial);
        }
    }

}
