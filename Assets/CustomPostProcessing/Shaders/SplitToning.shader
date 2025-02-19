Shader "Hidden/CustomPostProcess/SplitToning"
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
            Name "SplitToningPass"
            
            HLSLPROGRAM
            #include "Common/CustomPostProcessing.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            half4 _SplitShadows;
            half4 _SplitHighlights;

            half4 frag(Varyings input):SV_Target
            {
                float4 color = GetSource(input);
                float balance = _SplitShadows.w;
                float3 colorGamma = PositivePow(color, 1.0 / 2.2);

                float luma = saturate(GetLuminance(saturate(colorGamma)) + balance);
                float3 splitShadows = lerp((0.5).xxx, _SplitShadows.xyz, 1.0 - luma);
                float3 splitHighlights = lerp((0.5).xxx, _SplitHighlights.xyz, luma);
                colorGamma = SoftLight(colorGamma, splitShadows);
                colorGamma = SoftLight(colorGamma, splitHighlights);

                color.xyz = PositivePow(colorGamma, 2.2);
                return color;
            }
            ENDHLSL
        }
    }
}
