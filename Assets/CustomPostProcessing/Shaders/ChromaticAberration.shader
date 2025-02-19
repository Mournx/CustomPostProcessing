Shader "Hidden/CustomPostProcess/ChromaticAberration"
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
            Name "ChromaticAberrationPass"
            
            HLSLPROGRAM
            #include "Common/CustomPostProcessing.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            float _ChromaticAmount;

            half4 frag(Varyings input):SV_Target
            {
                float2 uv = input.uv;
                // Very fast version of chromatic aberration from HDRP using 3 samples and hardcoded
                // spectral lut. Performs significantly better on lower end GPUs.
                float2 coords = 2.0 * uv - 1.0;
                float2 end = uv - coords * dot(coords, coords) * _ChromaticAmount;
                float2 delta = (end - uv) / 3.0;

                half r = GetSource(uv).x;
                half g = GetSource(delta + uv).y;
                half b = GetSource(delta * 2.0 + uv).z;

                return half4(r, g, b, 1.0);
            }
            ENDHLSL
        }
    }
}
