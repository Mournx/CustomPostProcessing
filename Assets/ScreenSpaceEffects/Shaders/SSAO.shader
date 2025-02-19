Shader "Hidden/AO/SSAO"
{
    HLSLINCLUDE
    #pragma editor_sync_compilation
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    
    SAMPLER(sampler_BlitTexture);

    
    half4 _SSAOParams;

    float4 _ProjectionParams2;
    float4 _CameraViewTopLeftCorner;
    float4 _CameraViewXExtent;
    float4 _CameraViewYExtent;
    float4 _SSAOBlurRadius;
    float4 _SourceSize;

    #define INTENSITY _SSAOParams.x
    #define RADIUS _SSAOParams.y
    #define FALLOFF _SSAOParams.z
    #if defined(SHADER_API_GLES) && !defined(SHADER_API_GLES3)
    static const int SAMPLE_COUNT = 3;
    #elif defined(_SAMPLE_COUNT_HIGH)
        static const int SAMPLE_COUNT = 12;
    #elif defined(_SAMPLE_COUNT_MEDIUM)
        static const int SAMPLE_COUNT = 8;
    #else
        static const int SAMPLE_COUNT = 4;
    #endif

    #define SAMPLE_BASEMAP(uv)  half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, UnityStereoTransformScreenSpaceTex(uv)));
    #define SAMPLE_BASEMAP_R(uv)        half(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, UnityStereoTransformScreenSpaceTex(uv)).r);
    
    float Random(float2 p)
    {
        return frac(sin(dot(p,float2(12.9898,78.233))) * 43758.5453);
    }
    half4 PackAONormal(half ao, half3 n)
    {
        n *= 0.5f + 0.5f;
        return half4(ao, n);
    }

    half3 GetPackedNormal(half4 p)
    {
        return p.gba * 2.0f - 1.0f;
    }

    half GetPackedAO(half4 p)
    {
        return p.r;
    }
    
    //获取半球上随机一点
    half3 PickSamplePoint(float2 uv, int sampleIndex, half rcpSampleCount, half3 normal)
    {
        half gn = InterleavedGradientNoise(uv * _ScreenParams.xy, sampleIndex);
        half u = frac(Random(half2(0.0, sampleIndex)) + gn) * 2.0 - 1.0;
        half theta = Random(half2(1.0, sampleIndex) + gn) * TWO_PI;
        half u2 = sqrt(1.0 - u * u);

        //全球上随机一点
        half3 v = half3(u2 * cos(theta), u2 * sin(theta), u);
        v *= sqrt(sampleIndex * rcpSampleCount); //随着采样次数越向外采样

        //半球上随机一点 逆半球法线翻转   确保v和normal一个方向
        v = faceforward(v, -normal, v); 

        //缩放到[0, RADIUS]
        v *= RADIUS;
        return v;
    }
    
    half3 ReconstructViewPos(float2 uv, float linearEyeDepth)
    {
        uv.y = 1.0 - uv.y;

        float zScale = linearEyeDepth * _ProjectionParams2.x;
        float3 viewPos = _CameraViewTopLeftCorner.xyz + _CameraViewXExtent.xyz * uv.x + _CameraViewYExtent.xyz * uv.y;
        viewPos *= zScale;

        return viewPos;
    }

    half4 SSAOFragment(Varyings input): SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float rawDepth = SampleSceneDepth(input.texcoord);
        float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

        float3 normal = SampleSceneNormals(input.texcoord);
        //还原世界空间相机到顶点偏移向量
        float3 vpos = ReconstructViewPos(input.texcoord, linearDepth);

        const half rcpSampleCount = rcp(SAMPLE_COUNT);
        half ao = 0.0;

        UNITY_UNROLL
        for (int i = 0;i < SAMPLE_COUNT; i++)
        {
            half3 offset = PickSamplePoint(input.texcoord, i, rcpSampleCount, normal);
            half3 vpos2 = vpos + offset;

            //把采样点从世界坐标变换到裁剪空间
            half4 spos2 = mul(UNITY_MATRIX_VP, vpos2);
            //计算采样点的屏幕UV
            half2 uv2 = half2(spos2.x, spos2.y * _ProjectionParams.x) / spos2.w * 0.5 + 0.5;

            float rawDepth2 = SampleSceneDepth(uv2);
            float linearDepth2 = LinearEyeDepth(rawDepth2, _ZBufferParams);
            //判断采样点是否被遮蔽
            half IsInsideRadius = abs(spos2.w - linearDepth2) < RADIUS ? 1.0 : 0.0;

            half3 difference = ReconstructViewPos(uv2, linearDepth2) - vpos;//光线向量
            half inten = max(dot(difference, normal) - 0.004 * linearDepth, 0.0) * rcp(dot(difference, difference) + 0.0001);
            ao += inten * IsInsideRadius;
        }
        ao *= RADIUS;

        half falloff = 1.0 - linearDepth * half(rcp(FALLOFF));
        falloff = falloff*falloff;
        //提高AO对比度
        ao = PositivePow(saturate(ao * INTENSITY * falloff * rcpSampleCount), 0.6);
        return PackAONormal(ao, normal);
    }

    half CompareNormal(half3 d1, half3 d2)
    {
        return smoothstep(0.8, 1.0, dot(d1,d2));
    }

    half4 BlurFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float2 uv = input.texcoord;
        float2 delta = _SSAOBlurRadius * _SourceSize.zw;
        half4 p0 =  SAMPLE_BASEMAP(uv                       );
        half4 p1a = SAMPLE_BASEMAP(uv - delta * 1.3846153846);
        half4 p1b = SAMPLE_BASEMAP(uv + delta * 1.3846153846);
        half4 p2a = SAMPLE_BASEMAP(uv - delta * 3.2307692308);
        half4 p2b = SAMPLE_BASEMAP(uv + delta * 3.2307692308);

        half3 n0 = GetPackedNormal(p0);


        half w0 = half(0.2270270270);
        half w1a = CompareNormal(n0, GetPackedNormal(p1a)) * half(0.3162162162);
        half w1b = CompareNormal(n0, GetPackedNormal(p1b)) * half(0.3162162162);
        half w2a = CompareNormal(n0, GetPackedNormal(p2a)) * half(0.0702702703);
        half w2b = CompareNormal(n0, GetPackedNormal(p2b)) * half(0.0702702703);

        half s = half(0.0);
        s += GetPackedAO(p0) * w0;
        s += GetPackedAO(p1a) * w1a;
        s += GetPackedAO(p1b) * w1b;
        s += GetPackedAO(p2a) * w2a;
        s += GetPackedAO(p2b) * w2b;
        s *= rcp(w0 + w1a + w1b + w2a + w2b);

        return PackAONormal(s, n0);
    }

    //对角线Blur 进一步缩小噪点
    half BlurSmall(const float2 uv, const float2 delta)
    {
        half4 p0 = SAMPLE_BASEMAP(uv                             );
        half4 p1 = SAMPLE_BASEMAP(uv + float2(-delta.x, -delta.y));
        half4 p2 = SAMPLE_BASEMAP(uv + float2( delta.x, -delta.y));
        half4 p3 = SAMPLE_BASEMAP(uv + float2(-delta.x,  delta.y));
        half4 p4 = SAMPLE_BASEMAP(uv + float2( delta.x,  delta.y));

        half3 n0 = GetPackedNormal(p0);

        half w0 = 1.0;
        half w1 = CompareNormal(n0, GetPackedNormal(p1));
        half w2 = CompareNormal(n0, GetPackedNormal(p2));
        half w3 = CompareNormal(n0, GetPackedNormal(p3));
        half w4 = CompareNormal(n0, GetPackedNormal(p4));

        half s = 0.0;
        s += GetPackedAO(p0) * w0;
        s += GetPackedAO(p1) * w1;
        s += GetPackedAO(p2) * w2;
        s += GetPackedAO(p3) * w3;
        s += GetPackedAO(p4) * w4;

        return s *= rcp(w0 + w1 + w2 + w3 + w4);
    }

    half4 FinalBlur(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        const float2 uv = input.texcoord;
        const float2 delta = _SourceSize.zw;
        half ao = 1.0 - BlurSmall(uv, delta );
        return half4(0.0, 0.0, 0.0, ao);
    }

    // ------------------------------------------------------------------
    // Gaussian Blur
    // ------------------------------------------------------------------
    half GaussianBlur(Varyings input) : SV_Target
    {
        half2 uv = input.texcoord;
        half2 pixelOffset = _SSAOBlurRadius * _SourceSize.zw;
        half colOut = 0;

        //Kernel Width 7 * 7
        const int stepCount = 2;

        const half gWeights[stepCount] =
        {
            0.44908,
            0.05092
        };
        const half gOffsets[stepCount] =
        {
            0.53805,
            2.06278
        };

        UNITY_UNROLL
        for (int i = 0;i < stepCount; i++)
        {
            half2 texCoordOffset = gOffsets[i] * pixelOffset;
            half4 p1 = SAMPLE_BASEMAP(uv + texCoordOffset);
            half4 p2 = SAMPLE_BASEMAP(uv - texCoordOffset);
            half col = p1.r + p2.r;
            colOut += gWeights[i] * col;
        }
        return colOut; //垂直模糊返回 1.0 - colOut
    }

    // ------------------------------------------------------------------
    // Kawase Blur
    // ------------------------------------------------------------------
    half kawaseBlurFilter(half2 texCoord, half2 pixelSize, half iteration)
    {
        half2 texCoordSample;
        half2 halfPixelSize = pixelSize * 0.5;
        half2 dUV = ( pixelSize.xy * half2( iteration, iteration ) ) + halfPixelSize.xy;

        half cOut;

        //Sample top left pixel
        texCoordSample.x = texCoord.x - dUV.x;
        texCoordSample.x = texCoord.y + dUV.y;
        cOut += SAMPLE_BASEMAP_R(texCoordSample);

        //Sample top right pixel
        texCoordSample.x = texCoord.x + dUV.x;
        texCoordSample.x = texCoord.y + dUV.y;
        cOut += SAMPLE_BASEMAP_R(texCoordSample);

        //Sample bottom right pixel
        texCoordSample.x = texCoord.x + dUV.x;
        texCoordSample.x = texCoord.y - dUV.y;
        cOut += SAMPLE_BASEMAP_R(texCoordSample);

        //Sample bottom left pixel
        texCoordSample.x = texCoord.x - dUV.x;
        texCoordSample.x = texCoord.y - dUV.y;
        cOut += SAMPLE_BASEMAP_R(texCoordSample);

        cOut *= half(0.25);

        return cOut;
    }
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType"="Opaque"
               "RenderPipeline"="UniversalPipeline"
        }
        LOD 200
        ZTest Always
        ZWrite Off
        Cull off
        
        //0 - Occlusion estimation
        Pass
        {
            Name "SSAO_Occlusion"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment SSAOFragment
                #pragma multi_compile_local_fragment _SAMPLE_COUNT_LOW _SAMPLE_COUNT_MEDIUM _SAMPLE_COUNT_HIGH
            ENDHLSL
        }
         // ------------------------------------------------------------------
        // Bilateral Blur
        // ------------------------------------------------------------------

        // 1 
        Pass
        {
            Name "SSAO_BilateralBlur"
            
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment BlurFragment
            ENDHLSL    
        }
        //2 - Final Blur And Apply
        Pass
        {
            Name "SSAO_Bilateral_FinalPass"
            
            ZTest NotEqual
            ZWrite off
            Cull off
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add
            
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FinalBlur
            ENDHLSL
        }
    }
}
