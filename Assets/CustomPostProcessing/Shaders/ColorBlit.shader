Shader "Hidden/CustomPostProcess/ColorBlit"
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
            Name "ColorBlitPass"
            
            HLSLPROGRAM
            #include "Common/CustomPostProcessing.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            float _Intensity;

            half4 frag(Varyings input):SV_Target
            {
                float4 color = GetSource(input);
                return color * float4(0, _Intensity, 0, 1);
            }
            ENDHLSL
        }
    }
}
