Shader "Hidden/CustomPostProcess/ColorAdjustments"
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
            Name "ColorAdjustmentPass"
            HLSLPROGRAM
            #include "Common/CustomPostProcessing.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag

            #pragma shader_feature EXPOSURE
            #pragma shader_feature CONTRAST
            #pragma shader_feature COLOR_FILTER
            #pragma shader_feature HUE_SHIFT
            #pragma shader_feature SATURATION

            float4 _ColorAdjustments;
            float4 _ColorFilter;

            half3 ColorAdjustmentExposure(half3 color)
            {
                return color * _ColorAdjustments.x;
            }
            half3 ColorAdjustmentContrast(half3 color)
            {
                //为了更好的效果，将颜色从线性空间转到logC空间（因为要取美术中灰）
                color = LinearToLogC(color);
                color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
                return LogCToLinear(color);
            }
            half3 ColorAdjustmentColorFilter(half3 color)
            {
                color = SRGBToLinear(color);
                color = color * _ColorFilter.rgb;
                return color;
            }
            half3 ColorAdjustmentHueShift(half3 color)
            {
                color = RgbToHsv(color);
                //将色相偏移添加到H
                float hue = color.x + _ColorAdjustments.z;
                //色相超出范围则截断
                color.x = RotateHue(hue,0.0,1.0);
                return HsvToRgb(color);
            }
            half3 ColorAdjustmentSaturation(half3 color)
            {
                float luminance = Luminance(color);
                return(color - luminance) * _ColorAdjustments.w + luminance;
            }
            half3 ColorAdjustment(half3 color)
            {
                //防止颜色值过大
                color = min(color,60.0);

                #ifdef EXPOSURE
                color = ColorAdjustmentExposure(color);
                #endif
                #ifdef CONTRAST
                color = ColorAdjustmentContrast(color);
                #endif
                #ifdef COLOR_FILTER
                color = ColorAdjustmentColorFilter(color);
                #endif
                #ifdef HUE_SHIFT
                color = ColorAdjustmentHueShift(color);
                #endif
                #ifdef SATURATION
                color = ColorAdjustmentSaturation(color);
                #endif

                //饱和度增加时可能产生负数
                return max(color,0.0);
            }
            half4 frag(Varyings input):SV_Target
            {
                half3 color = GetSource(input).xyz;
                half3 finalCol = ColorAdjustment(color);
                return half4(finalCol,1.0);
            }
            ENDHLSL
        }
    }
}
