#ifndef POSTPROCESSING_INCLUDED
#define POSTPROCESSING_INCLUDED

#include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include"Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

TEXTURE2D(_BlitTexture);
SAMPLER(sampler_LinearClamp);

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

struct Varyings
{
    float2 uv:TEXCOORD0;
    float4 vertex: SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

struct ScreenSpaceData
{
    float4 positionCS;
    float2 uv;
};

ScreenSpaceData GetScreenSpaceData(uint vertexID: SV_VertexID)
{
    ScreenSpaceData output;
    output.positionCS = float4(vertexID<=1?-1.0:3.0,vertexID==1?3.0:-1.0,0.0,1.0);
    output.uv = float2(vertexID<=1?0.0:2.0,vertexID==1?2.0:0.0);
    //不同API可能出现颠倒的情况
    if(_ProjectionParams.x<0.0)
        output.uv.y = 1.0 - output.uv.y;
    return output;
}

half4 GetSource(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BlitTexture,sampler_LinearClamp,uv);
}
half4 GetSource(Varyings input)
{
    return GetSource(input.uv);
}

float SampleDepth(float2 uv)
{
    #if defined(UNITY_STEREO_INSTANCING_ENABLED)||defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    return SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture,sampler_CameraDepthTexture,uv,unity_StereoEyeIndex).r;
    #else
    return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,sampler_CameraDepthTexture,uv);
    #endif
}

float SampleDepth(Varyings input)
{
    return SampleDepth(input.uv);
}

half GetLuminance(half3 colorLinear)
{
    #if _TONEMAP_ACES
    return AcesLuminance(colorLinear);
    #else
    return Luminance(colorLinear);
    #endif
}

Varyings Vert(uint vertexID: SV_VertexID)
{
    Varyings output;
    ScreenSpaceData data = GetScreenSpaceData(vertexID);
    output.vertex = data.positionCS;
    output.uv = data.uv;
    return output;
}

#endif