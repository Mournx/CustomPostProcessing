#ifndef SOFTSHADOW_PCSS_INCLUDED
#define SOFTSHADOW_PCSS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float4 _DirLightPcssParams0[4];
float4 _DirLightPcssParams1[4];
float4 _DirLightPcssProjs[4];
float _ShadowTileTexelSize;
float4 _CascadeOffsetScales[4];
float _FindBlockerSampleCount;
float _PcfSampleCount;
float _PcssTemporalFilter; //frame index from 0 to 1023
float4 _PenumbraMaskTexelSize;
float4 _ColorAttachmentTexelSize;
TEXTURE2D(_PenumbraMaskTex);float4 _PenumbraMaskTex_TexelSize;

#define DISK_SAMPLE_COUNT 64

static const float2 poissonDisk[16] =
{
    float2( -0.94201624, -0.39906216 ),
    float2( 0.94558609, -0.76890725 ),
    float2( -0.094184101, -0.92938870 ),
    float2( 0.34495938, 0.29387760 ),
    float2( -0.91588581, 0.45771432 ),
    float2( -0.81544232, -0.87912464 ),
    float2( -0.38277543, 0.27676845 ),
    float2( 0.97484398, 0.75648379 ),
    float2( 0.44323325, -0.97511554 ),
    float2( 0.53742981, -0.47373420 ),
    float2( -0.26496911, -0.41893023 ),
    float2( 0.79197514, 0.19090188 ),
    float2( -0.24188840, 0.99706507 ),
    float2( -0.81409955, 0.91437590 ),
    float2( 0.19984126, 0.78641367 ),
    float2( 0.14383161, -0.14100790 )
};

static const float2 fibonacciSpiralDirection[DISK_SAMPLE_COUNT] =
{
    float2 (1, 0),
    float2 (-0.7373688780783197, 0.6754902942615238),
    float2 (0.08742572471695988, -0.9961710408648278),
    float2 (0.6084388609788625, 0.793600751291696),
    float2 (-0.9847134853154288, -0.174181950379311),
    float2 (0.8437552948123969, -0.5367280526263233),
    float2 (-0.25960430490148884, 0.9657150743757782),
    float2 (-0.46090702471337114, -0.8874484292452536),
    float2 (0.9393212963241182, 0.3430386308741014),
    float2 (-0.924345556137805, 0.3815564084749356),
    float2 (0.423845995047909, -0.9057342725556143),
    float2 (0.29928386444487326, 0.9541641203078969),
    float2 (-0.8652112097532296, -0.501407581232427),
    float2 (0.9766757736281757, -0.21471942904125949),
    float2 (-0.5751294291397363, 0.8180624302199686),
    float2 (-0.12851068979899202, -0.9917081236973847),
    float2 (0.764648995456044, 0.6444469828838233),
    float2 (-0.9991460540072823, 0.04131782619737919),
    float2 (0.7088294143034162, -0.7053799411794157),
    float2 (-0.04619144594036213, 0.9989326054954552),
    float2 (-0.6407091449636957, -0.7677836880006569),
    float2 (0.9910694127331615, 0.1333469877603031),
    float2 (-0.8208583369658855, 0.5711318504807807),
    float2 (0.21948136924637865, -0.9756166914079191),
    float2 (0.4971808749652937, 0.8676469198750981),
    float2 (-0.952692777196691, -0.30393498034490235),
    float2 (0.9077911335843911, -0.4194225289437443),
    float2 (-0.38606108220444624, 0.9224732195609431),
    float2 (-0.338452279474802, -0.9409835569861519),
    float2 (0.8851894374032159, 0.4652307598491077),
    float2 (-0.9669700052147743, 0.25489019011123065),
    float2 (0.5408377383579945, -0.8411269468800827),
    float2 (0.16937617250387435, 0.9855514761735877),
    float2 (-0.7906231749427578, -0.6123030256690173),
    float2 (0.9965856744766464, -0.08256508601054027),
    float2 (-0.6790793464527829, 0.7340648753490806),
    float2 (0.0048782771634473775, -0.9999881011351668),
    float2 (0.6718851669348499, 0.7406553331023337),
    float2 (-0.9957327006438772, -0.09228428288961682),
    float2 (0.7965594417444921, -0.6045602168251754),
    float2 (-0.17898358311978044, 0.9838520605119474),
    float2 (-0.5326055939855515, -0.8463635632843003),
    float2 (0.9644371617105072, 0.26431224169867934),
    float2 (-0.8896863018294744, 0.4565723210368687),
    float2 (0.34761681873279826, -0.9376366819478048),
    float2 (0.3770426545691533, 0.9261958953890079),
    float2 (-0.9036558571074695, -0.4282593745796637),
    float2 (0.9556127564793071, -0.2946256262683552),
    float2 (-0.50562235513749, 0.8627549095688868),
    float2 (-0.2099523790012021, -0.9777116131824024),
    float2 (0.8152470554454873, 0.5791133210240138),
    float2 (-0.9923232342597708, 0.12367133357503751),
    float2 (0.6481694844288681, -0.7614961060013474),
    float2 (0.036443223183926, 0.9993357251114194),
    float2 (-0.7019136816142636, -0.7122620188966349),
    float2 (0.998695384655528, 0.05106396643179117),
    float2 (-0.7709001090366207, 0.6369560596205411),
    float2 (0.13818011236605823, -0.9904071165669719),
    float2 (0.5671206801804437, 0.8236347091470047),
    float2 (-0.9745343917253847, -0.22423808629319533),
    float2 (0.8700619819701214, -0.49294233692210304),
    float2 (-0.30857886328244405, 0.9511987621603146),
    float2 (-0.4149890815356195, -0.9098263912451776),
    float2 (0.9205789302157817, 0.3905565685566777)
};


float BlockerSearchRadius(float receiverDepth, float depth2RadialScale, float maxSampleZDistance, float minFilterRadius)
{
    #if UNITY_REVERSED_Z
        return max(min(1.0 - receiverDepth, maxSampleZDistance) * depth2RadialScale, minFilterRadius);
    #else
        return max(min(receiverDepth, maxSampleZDistance) * depth2RadialScale, minFilterRadius);
    #endif
}

//FibonacciSpiralDisk随机采样，Sample点更集中在中心,更适合FindBlocker
float2 ComputeSampleClumped(const in int sampleIndex, const in float sampleCountInverse, const in float clumpExponent, out float sampleDistNorm)
{
    sampleDistNorm = (float)sampleIndex * sampleCountInverse;
    sampleDistNorm = PositivePow(sampleDistNorm, clumpExponent);

    return fibonacciSpiralDirection[sampleIndex] * sampleDistNorm;
}
//使用FibonacciSpiralDisk做随机采样，它是一个Uniform的数组，元素只代表偏移的方向，而偏移的距离通过sampleIndex和sampleCount确定
float2 ComputeSampleUniform(const in int sampleIndex, const in float sampleCountInverse, const in float sampleBias, out float sampleDistNorm)
{
    sampleDistNorm = (float)sampleIndex * sampleCountInverse + sampleBias;
    sampleDistNorm = sqrt(sampleDistNorm);

    return fibonacciSpiralDirection[sampleIndex] * sampleDistNorm;
}

float2 ComputeFindBlockerSampleOffset(const in float filterRadius, const in int sampleIndex, const in float sampleCountInverse, const in float clumpExponent, const in float2 sampleJitter, const in float2 shadowmapInAtlasScale, out float sampleDistNorm)
{
    float2 offset = ComputeSampleClumped(sampleIndex, sampleCountInverse, clumpExponent, sampleDistNorm);
    //增加Temporal Jitter
    offset  = float2(offset.x * sampleJitter.y + offset.y * sampleJitter.x, offset.x * -sampleJitter.x + offset.y * sampleJitter.y);
    //应用searchRadius(已经经过depth2Radial转换,即基于角直径)
    offset *= filterRadius;
    //应用shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5，角直径是基于单个Tile的，因此要考虑Atlas上的缩放
    offset *= shadowmapInAtlasScale;

    return offset;
}

float2 ComputePcfSampleOffset(const in float filterSize, const in float samplingFilterSize, const in int sampleIndex, const in float sampleCountInverse,
    const in float sampleCountBias, const in float2 sampleJitter, const in float2 shadowmapInAtlasScale, const in float radial2DepthScale,
    float maxPcssOffset, out float zOffset)
{
    #if UNITY_REVERSED_Z
    #define Z_OFFSET_DIRECTION 1
    #else
    #define Z_OFFSET_DIRECTION (-1)
    #endif

    float sampleDistNorm;
    float2 offset = ComputeSampleUniform(sampleIndex, sampleCountInverse, sampleCountBias, sampleDistNorm);
    //增加Temporal Jitter
    offset  = float2(offset.x * sampleJitter.y + offset.y * sampleJitter.x, offset.x * -sampleJitter.x + offset.y * sampleJitter.y);
    //应用Penumbra评估得到的FilterSize
    offset *= samplingFilterSize;
    //应用shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5
    offset *= shadowmapInAtlasScale;

    zOffset = min(filterSize * sampleDistNorm * radial2DepthScale, maxPcssOffset) * Z_OFFSET_DIRECTION;

    return offset;
}

float2 ComputePcfSampleJitter(float2 pixCoord, uint frameIndex)
{
    float sampleJitterAngle = InterleavedGradientNoise(pixCoord, frameIndex) * 2.0 * PI;
    return float2(sin(sampleJitterAngle),cos(sampleJitterAngle));
}

float FindBlocker(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float2 shadowCoord, float receiverDepth, float searchRadius,
    float2 minCoord, float2 maxCoord, int sampleCount, float clumpExponent,
    float2 shadowmapInAtlasScale, float2 sampleJitter, float minFilterRadius, float minFilterRadial2DepthScale,
    float radial2DepthScale)
{
    #if UNITY_REVERSED_Z
    #define Z_OFFSET_DIRECTION 1
    #else
    #define Z_OFFSET_DIRECTION (-1)
    #endif

    float depthSum = 0.0;
    float depthCount = 0.0;
    float sampleCountInverse = rcp((float)sampleCount);

    for(int i = 0;i < sampleCount && i < DISK_SAMPLE_COUNT; i++)
    {
        float sampleDistNorm;
        float2 offset = ComputeFindBlockerSampleOffset(searchRadius,i,sampleCountInverse, clumpExponent, sampleJitter, shadowmapInAtlasScale, sampleDistNorm);
        float2 sampleCoord = shadowCoord + offset;
        float sampleDepth = SAMPLE_TEXTURE2D_LOD(ShadowMap, sampler_ShadowMap, sampleCoord, 0.0).r;

        //对receiver z 做Cone Base偏移 消除自遮挡
        float radialOffset = searchRadius * sampleDistNorm;
        float zOffset = radialOffset * (radialOffset < minFilterRadius ? minFilterRadial2DepthScale : radial2DepthScale);
        float receiverDepthWithOffset = receiverDepth + (Z_OFFSET_DIRECTION) * zOffset;

        if(!(any(sampleCoord < minCoord) || any(sampleCoord > maxCoord)) &&
#if UNITY_REVERSED_Z
                (sampleDepth > receiverDepthWithOffset)
#else
                (sampleDepth < receiverDepthWithOffset)
#endif
                )
        {
            depthSum += sampleDepth;
            depthCount += 1.0;
        }
    }

    return depthCount > FLT_EPS ? (depthSum / depthCount) : 0;
}

float EstimatePenumbra(float receiverDepth, float avgBlockerDepth, float depth2RadialScale, float maxSampleZDistance,
                        float minFilterRadius, out float filterSize, out float blockerDistance)
{
    if(avgBlockerDepth < Eps_float())
        return 0;
    else
    {
        blockerDistance = min(abs(avgBlockerDepth - receiverDepth) * 0.9, maxSampleZDistance);
        filterSize = blockerDistance * depth2RadialScale;
        return max(filterSize, minFilterRadius);
    }
}

float PCF(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float samplingFilterSize, int sampleCount, 
                                float2 shadowmapInAtlasScale, float2 sampleJitter, float2 minCoord, float2 maxCoord,
                                float radial2DepthScale, float filterSize, float maxPcssOffset)
{
    float shadowAttenuationSum = 0.0;
    float sampleSum = 0.0;

    float sampleCountInverse = rcp((float)sampleCount);
    float sampleCountBias = 0.5 * sampleCountInverse;

    for(int i = 0; i < sampleCount && i < DISK_SAMPLE_COUNT; i++)
    {
        float zOffset;
        float2 offset = ComputePcfSampleOffset(filterSize, samplingFilterSize, i, sampleCountInverse,
            sampleCountBias, sampleJitter, shadowmapInAtlasScale, radial2DepthScale,
            maxPcssOffset, zOffset);
        //对receiver z 做Cone Base偏移 消除自遮挡
        float3 sampleCoord = (shadowCoord.xyz + float3(offset, zOffset));

        //只有sampleCoord不超出Tile时执行采样
        if(!(any(sampleCoord < minCoord) || any(sampleCoord > maxCoord)))
        {
            shadowAttenuationSum += SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, sampleCoord.xyz);
            sampleSum += 1.0;
        }
    }

    return shadowAttenuationSum / sampleSum;
}

//For forward shading
float ForwardPCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float4 shadowMapSize)
{
    int cascadeIndex = (int)shadowCoord.w;
    float receiverDepth = shadowCoord.z;
    //当前片元所处Tile的边界
    //shadowmapInAtlasOffset=当前Tile在ShadowAtlas上的起始点，范围[0,1]
    float2 shadowmapInAtlasOffset = _CascadeOffsetScales[cascadeIndex].xy;
    //shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5
    float2 shadowmapInAtlasScale = _CascadeOffsetScales[cascadeIndex].zw;
    float2 minCoord = shadowmapInAtlasOffset;
    float2 maxCoord = shadowmapInAtlasOffset + shadowmapInAtlasScale;
    float2 sampleJitter = float2(1.0, 1.0);

    //单个Tile的Texel大小(1/2048)
    float texelSize = shadowMapSize.x / shadowmapInAtlasScale.x;

    float depth2RadialScale = _DirLightPcssParams0[cascadeIndex].x;
    float radial2DepthScale = _DirLightPcssParams0[cascadeIndex].y;
    float maxBlockerDistance = _DirLightPcssParams0[cascadeIndex].z;
    float maxSamplingDistance = _DirLightPcssParams0[cascadeIndex].w;
    float minFilterRadius = texelSize * _DirLightPcssParams1[cascadeIndex].x;
    float minFilterRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].y;
    float blockerRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].z;
    float blockerClumpSampleExponent = _DirLightPcssParams1[cascadeIndex].w;
    float maxPcssOffset = maxSamplingDistance;
    float maxSampleZDistance = maxBlockerDistance;

    //计算遮挡物平均深度
    float blockerSearchRadius = BlockerSearchRadius(receiverDepth, depth2RadialScale, maxSamplingDistance, minFilterRadius);
    float avgBlockerDepth = FindBlocker(TEXTURE2D_ARGS(ShadowMap, sampler_LinearClamp), shadowCoord.xy, receiverDepth, blockerSearchRadius,
        minCoord, maxCoord, _FindBlockerSampleCount, blockerClumpSampleExponent,
        shadowmapInAtlasScale, sampleJitter, minFilterRadius, minFilterRadial2DepthScale,
        blockerRadial2DepthScale);
    //评估PCF范围
    float filterSize, blockerDistance;
    float samplingFilterSize = EstimatePenumbra(receiverDepth, avgBlockerDepth, depth2RadialScale, maxSampleZDistance,
        minFilterRadius, filterSize, blockerDistance);
    maxPcssOffset = min(maxPcssOffset, blockerDistance * 0.25f);
    //PCF采样
    return PCF(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingFilterSize, _PcfSampleCount,
        shadowmapInAtlasScale, sampleJitter, minCoord, maxCoord,
        minFilterRadial2DepthScale, filterSize, maxPcssOffset);
}

//For deferred shading
float DeferredPCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float4 shadowMapSize, float2 screenCoord)
{
    float penumbraMask = SAMPLE_TEXTURE2D(_PenumbraMaskTex, sampler_LinearClamp, screenCoord).r;
    if(penumbraMask > Eps_float())
    {
        int cascadeIndex = (int)shadowCoord.w;
        float receiverDepth = shadowCoord.z;
        //当前片元所处Tile的边界
        //shadowmapInAtlasOffset=当前Tile在ShadowAtlas上的起始点，范围[0,1]
        float2 shadowmapInAtlasOffset = _CascadeOffsetScales[cascadeIndex].xy;
        //shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5
        float2 shadowmapInAtlasScale = _CascadeOffsetScales[cascadeIndex].zw;
        float2 minCoord = shadowmapInAtlasOffset;
        float2 maxCoord = shadowmapInAtlasOffset + shadowmapInAtlasScale;

        //单个Tile的Texel大小(1/2048)
        float texelSize = shadowMapSize.x / shadowmapInAtlasScale.x;
        
        float depth2RadialScale = _DirLightPcssParams0[cascadeIndex].x;
        float radial2DepthScale = _DirLightPcssParams0[cascadeIndex].y;
        float maxBlokcerDistance = _DirLightPcssParams0[cascadeIndex].z;
        float maxSamplingDistance = _DirLightPcssParams0[cascadeIndex].w;
        float minFilterRadius = texelSize * _DirLightPcssParams1[cascadeIndex].x;
        float minFilterRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].y;
        float blockerRadial2DepthScale = _DirLightPcssParams1[cascadeIndex].z;
        float blockerClumpSampleExponent = _DirLightPcssParams1[cascadeIndex].w;
        float maxPcssOffset = maxSamplingDistance * abs(_DirLightPcssProjs[cascadeIndex].z);
        float maxSampleZDistance = maxBlokcerDistance * abs(_DirLightPcssProjs[cascadeIndex].z);
        
        //计算Temporal Jitter
        float2 sampleJitter = ComputePcfSampleJitter(screenCoord, (uint)_PcssTemporalFilter);
        
        //计算遮挡物平均深度值
        float blockerSearchRadius = BlockerSearchRadius(receiverDepth, depth2RadialScale, maxSamplingDistance, minFilterRadius);
        float avgBlockerDepth = FindBlocker(TEXTURE2D_ARGS(ShadowMap, sampler_LinearClamp), shadowCoord.xy, receiverDepth, blockerSearchRadius,
            minCoord, maxCoord, _FindBlockerSampleCount, blockerClumpSampleExponent,
            shadowmapInAtlasScale, sampleJitter, minFilterRadius, minFilterRadial2DepthScale,
            blockerRadial2DepthScale);
        //评估PCF范围
        float filterSize, blockerDistance;
        float samplingFilterSize = EstimatePenumbra(receiverDepth, avgBlockerDepth, depth2RadialScale, maxSampleZDistance,
            minFilterRadius, filterSize, blockerDistance);
        maxPcssOffset = min(maxPcssOffset, blockerDistance * 0.25f);
        //PCF采样
        return PCF(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingFilterSize, _PcfSampleCount,
            shadowmapInAtlasScale, sampleJitter, minCoord, maxCoord,
            minFilterRadial2DepthScale, filterSize, maxPcssOffset);
    }
    else
    {
        return SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord);
    }
}


#endif