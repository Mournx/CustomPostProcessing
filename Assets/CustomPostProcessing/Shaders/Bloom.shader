Shader "Hidden/CustomPostProcess/Bloom"
{
  HLSLINCLUDE
        #include "Common/CustomPostProcessing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        
        #if _BLOOM_LQ_DIRT || _BLOOM_HQ_DIRT
            #define BLOOM_DIRT
        #endif

        TEXTURE2D(_SourceTexLowMip);
        TEXTURE2D(_LensDirtTexture);
        TEXTURE2D(_BloomTexture);
        

        float4 _BlitTexture_TexelSize;
        float4 _SourceTexLowMip_TexelSize;
        float4 _BloomTexture_TexelSize;

        float4 _BloomParams; // x: scatter, y: clamp, z: threshold (linear), w: threshold knee
        #define Scatter             _BloomParams.x
        #define ClampMax            _BloomParams.y
        #define Threshold           _BloomParams.z
        #define ThresholdKnee       _BloomParams.w

        float4 _BloomParams2;//x:intensity yzw:tint
        #define BloomIntensity          _BloomParams2.x
        #define BloomTint               _BloomParams2.yzw
        #define LensDirtScale           _LensDirtParams.xy
        #define LensDirtOffset          _LensDirtParams.zw
        #define LensDirtIntensity       _LensDirtIntensity.x
        
        
        half4 FragPrefilter(Varyings input) : SV_Target
        {
            float2 uv = input.uv;
            #if _BLOOM_HQ
                float texelSize = _BlitTexture_TexelSize.x;
                half4 A = GetSource( uv + texelSize * float2(-1.0, -1.0));
                half4 B = GetSource( uv + texelSize * float2(0.0, -1.0));
                half4 C = GetSource( uv + texelSize * float2(1.0, -1.0));
                half4 D = GetSource( uv + texelSize * float2(-0.5, -0.5));
                half4 E = GetSource( uv + texelSize * float2(0.5, -0.5));
                half4 F = GetSource( uv + texelSize * float2(-1.0, 0.0));
                half4 G = GetSource(uv);
                half4 H = GetSource( uv + texelSize * float2(1.0, 0.0));
                half4 I = GetSource( uv + texelSize * float2(-0.5, -0.5));
                half4 J = GetSource( uv + texelSize * float2(0.5,0.5));
                half4 K = GetSource( uv + texelSize * float2(-1.0, 1.0));
                half4 L = GetSource( uv + texelSize * float2(0.0, 1.0));
                half4 M = GetSource( uv + texelSize * float2(1.0, 1.0));

                half2 div = (1.0 / 4.0) * half2(0.5, 0.125);

                half4 o = (D + E + I + J) * div.x;
                o += (A + B + G + F) * div.y;
                o += (B + C + H + G) * div.y;
                o += (F + G + L + K) * div.y;
                o += (G + H + M + L) * div.y;

                half3 color = o.xyz;
            #else
                half3 color = GetSource(uv).xyz;
            #endif

            // User controlled clamp to limit crazy high broken spec
            color = min(ClampMax, color);

            // Thresholding
            half brightness = Max3(color.r, color.g, color.b);
            half softness = clamp(brightness - Threshold + ThresholdKnee, 0.0, 2.0 * ThresholdKnee);
            softness = (softness * softness) / (4.0 * ThresholdKnee + 1e-4);
            half multiplier = max(brightness - Threshold, softness) / max(brightness, 1e-4);
            color *= multiplier;

            // Clamp colors to positive once in prefilter. Encode can have a sqrt, and sqrt(-x) == NaN. Up/Downsample passes would then spread the NaN.
            color = max(color, 0);
            return half4(color,1.0);
        }
        half4 FragBlurH(Varyings input) : SV_Target
        {
            float texelSize = _BlitTexture_TexelSize.x * 2.0;
            float2 uv = input.uv;

            // 9-tap gaussian blur on the downsampled source
            half3 c0 = GetSource(uv - float2(texelSize * 4.0, 0.0));
            half3 c1 = GetSource(uv - float2(texelSize * 3.0, 0.0));
            half3 c2 = GetSource(uv - float2(texelSize * 2.0, 0.0));
            half3 c3 = GetSource(uv - float2(texelSize * 1.0, 0.0));
            half3 c4 = GetSource(uv);
            half3 c5 = GetSource(uv + float2(texelSize * 1.0, 0.0));
            half3 c6 = GetSource(uv + float2(texelSize * 2.0, 0.0));
            half3 c7 = GetSource(uv + float2(texelSize * 3.0, 0.0));
            half3 c8 = GetSource(uv + float2(texelSize * 4.0, 0.0));

            half3 color = c0 * 0.01621622 + c1 * 0.05405405 + c2 * 0.12162162 + c3 * 0.19459459
            + c4 * 0.22702703
            + c5 * 0.19459459 + c6 * 0.12162162 + c7 * 0.05405405 + c8 * 0.01621622;

            return half4(color,1.0);
        }

        half4 FragBlurV(Varyings input) : SV_Target
        {
            float texelSize = _BlitTexture_TexelSize.y;
            float2 uv = input.uv;

            // Optimized bilinear 5-tap gaussian on the same-sized source (9-tap equivalent)
            half3 c0 = GetSource(uv - float2(0.0, texelSize * 3.23076923));
            half3 c1 = GetSource(uv - float2(0.0, texelSize * 1.38461538));
            half3 c2 = GetSource(uv);
            half3 c3 = GetSource(uv + float2(0.0, texelSize * 1.38461538));
            half3 c4 = GetSource(uv + float2(0.0, texelSize * 3.23076923));

            half3 color = c0 * 0.07027027 + c1 * 0.31621622
            + c2 * 0.22702703
            + c3 * 0.31621622 + c4 * 0.07027027;

            return half4(color,1.0);
        }   
        half3 Upsample(float2 uv)
        {
            half3 highMip = GetSource(uv);

            #if _BLOOM_HQ
               half3 lowMip = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_SourceTexLowMip, sampler_LinearClamp), uv, _SourceTexLowMip_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex);
            #else
               half3 lowMip = SAMPLE_TEXTURE2D_X(_SourceTexLowMip, sampler_LinearClamp, uv);
            #endif

            return lerp(highMip, lowMip, Scatter);
        }

        half4 FragUpsample(Varyings input) : SV_Target
        {
            half3 color = Upsample(input.uv);
            return half4(color,1.0);
        }
        half4 FragFinal(Varyings input) : SV_Target
        {
           half3 color = GetSource(input);
            #if _BLOOM_HQ
            half4 bloom =  SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_BloomTexture, sampler_LinearClamp), input.uv, _BloomTexture_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex);
            #else
            half4 bloom = SAMPLE_TEXTURE2D_X(_BloomTexture, sampler_LinearClamp, input.uv);
            #endif
            
            bloom *= BloomIntensity;
            color += bloom.xyz * BloomTint;

            #if defined(BLOOM_DIRT)
            {
               half3 dirt = SAMPLE_TEXTURE2D(_LensDirtTexture,sampler_LinearClamp,input.uv * LensDirtScale + LensDirtOffset).xyz;
               dirt *= LensDirtIntensity;
               color += bloom.xyz * dirt;
            }
            #endif
            return half4(color,1.0);
        }
   ENDHLSL     
   SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 200
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Bloom Prefilter"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragPrefilter
                #pragma multi_compile_local _ _BLOOM_HQ
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Blur Horizontal"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlurH
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Blur Vertical"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBlurV
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Upsample"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragUpsample
                #pragma multi_compile_local _ _BLOOM_HQ
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Combine"
            
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragFinal
                #pragma multi_compile_local _ _BLOOM_HQ
            ENDHLSL
        }
    }
}
