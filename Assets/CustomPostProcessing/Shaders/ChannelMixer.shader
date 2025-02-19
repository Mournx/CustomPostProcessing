Shader "Hidden/CustomPostProcess/ChannelMixer"
{
    SubShader
    {
        Tags { "RenderType"="Opaque"
               "RenderPipeline"="UniversalPipeline"
        }
        LOD 200
        ZWrite Off
        Cull off
        Pass
        {
            Name "ChannelMixerPass"
            
            HLSLPROGRAM
            #include "Common/CustomPostProcessing.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            half4 _ChannelMixerR;
            half4 _ChannelMixerG;
            half4 _ChannelMixerB;

            half4 frag(Varyings input):SV_Target
            {
                float4 color = GetSource(input);
                color.xyz = half3(dot(color.xyz,_ChannelMixerR.xyz),dot(color.xyz,_ChannelMixerG.xyz),dot(color.xyz,_ChannelMixerB.xyz)); 
                return color;
            }
            ENDHLSL
        }
    }
}
