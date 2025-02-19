Shader "Hidden/AO/HBAO"
{
    HLSLINCLUDE
    #pragma editor_sync_compilation
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    
    SAMPLER(sampler_BlitTexture);

    
    half4 _HBAOParams;

    float4 _ProjectionParams2;
    float4 _CameraViewTopLeftCorner;
    float4 _CameraViewXExtent;
    float4 _CameraViewYExtent;
    float4 _HBAOBlurRadius;
    float4 _SourceSize;
    float _RadiusPixel;

    #define INTENSITY _HBAOParams.x
    #define RADIUS _HBAOParams.y
    #define MAXRADIUSPIXEL _HBAOParams.z
    #define ANGLEBIAS _HBAOParams.w
    #define DIRECTION_COUNT 8
    #define STEP_COUNT 6
    
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
    
    //根据线性深度值和屏幕UV, 还原视角空间下的顶点位置
    half3 ReconstructViewPos(float2 uv, float linearEyeDepth)
    {
        //Screen(NDC) to CS: uv.y = 1.0 - uv.y
        //CS to VS: uv = 1.0 - uv;
        uv.x = 1.0 - uv.x;

        float zScale = -linearEyeDepth * _ProjectionParams2.x; // divide by near plane
        float3 viewPos = _CameraViewTopLeftCorner.xyz + _CameraViewXExtent.xyz * uv.x + _CameraViewYExtent.xyz * uv.y;
        viewPos *= zScale;

        return viewPos;
    }
    //还原视角空间法线
    half3 ReconstructViewNormals(float2 uv)
    {
        float3 normal = SampleSceneNormals(uv);
        normal = TransformWorldToViewNormal(normal, true);
        //erse z
        normal.z = -normal.z;

        return normal;
    }
    //距离衰减
    float FallOff(float dist)
    {
        return 1 - dist * dist / (RADIUS * RADIUS);
    }
    // https://www.derschmale.com/2013/12/20/an-alternative-implementation-for-hbao-2/
    inline float ComputeAO(float3 vpos, float3 stepVpos, float3 normal, inout float topOcclusion)
    {
        float3 h = stepVpos - vpos;
        float dist = length(h);
        float occlusion = dot(normal, h) / dist;
        float diff = max(occlusion - topOcclusion, 0);
        topOcclusion = max(occlusion, topOcclusion);
        return diff * saturate(FallOff(dist));
    }
    
    half4 HBAOFragment(Varyings input): SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float rawDepth = SampleSceneDepth(input.texcoord);
        float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
        
        //还原世界空间相机到顶点偏移向量
        float3 vpos = ReconstructViewPos(input.texcoord, linearDepth);
        float3 normal = ReconstructViewNormals(input.texcoord);

        float2 noise = float2(Random(input.texcoord.yx), Random(input.texcoord.xy));

        //计算步进值
        float stride = min(_RadiusPixel / vpos.z, MAXRADIUSPIXEL) / (STEP_COUNT + 1.0);
        if(stride < 1) return 0.0;
        float stepRadian = TWO_PI / DIRECTION_COUNT;
        
        half ao = 0.0;

        UNITY_UNROLL
        for (int d = 0;d < DIRECTION_COUNT; d++)
        {
            float radian = stepRadian * (d + noise.x);
            float sinr, cosr;
            sincos(radian, sinr, cosr);
            float2 direction = float2(cosr, sinr);

            float rayPixels = frac(noise.y) * stride + 1.0;

            float topOcclusion = ANGLEBIAS; //上一次(最大的)AO,初始值为angle bias

            UNITY_UNROLL
            for (int s = 0; s < STEP_COUNT; s++)
            {
                float2  uv2 = round(rayPixels * direction) * _SourceSize.zw + input.texcoord;
                float rawDepth2 = SampleSceneDepth(uv2);
                float linearDepth2 = LinearEyeDepth(rawDepth2, _ZBufferParams);
                float3 vpos2 = ReconstructViewPos(uv2, linearDepth2);
                ao += ComputeAO(vpos, vpos2, normal, topOcclusion);
                rayPixels += stride;
            }
        }
        //提高对比度
        ao = PositivePow(ao * rcp(STEP_COUNT * DIRECTION_COUNT) * INTENSITY, 0.6);
        return half4(ao, normal);
    }

    half CompareNormal(half3 d1, half3 d2)
    {
        return smoothstep(0.8, 1.0, dot(d1,d2));
    }

    half4 BilateralBlur(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float2 uv = input.texcoord;
        float2 delta = _HBAOBlurRadius * _SourceSize.zw;
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
    
    

    // ------------------------------------------------------------------
    // Gaussian Blur
    // ------------------------------------------------------------------
    half GaussianBlur(Varyings input) : SV_Target
    {
        half2 uv = input.texcoord;
        half2 pixelOffset = _HBAOBlurRadius * _SourceSize.zw;
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

    half4 FinalPassFragment(Varyings input) : SV_Target
    {
        half ao = 1.0 - GaussianBlur(input);
        return half4(0.0, 0.0, 0.0, ao);
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
            Name "HBAO_Occlusion"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment HBAOFragment
            ENDHLSL
        }
         // ------------------------------------------------------------------
        // Gaussian Blur
        // ------------------------------------------------------------------

        // 1 
        Pass
        {
            Name "HBAO_GaussianBlur"
            
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment GaussianBlur
            ENDHLSL    
        }
        //2 - Final Blur And Apply
        Pass
        {
            Name "HBAO_Gaussian_FinalPass"
            
            ZTest NotEqual
            ZWrite off
            Cull off
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add
            
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FinalPassFragment
            ENDHLSL
        }
    }
}
