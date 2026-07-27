Shader "Jagara/EntityFactionOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 4)) = 1
        _Saturation ("Saturation Multiplier", Range(0, 3)) = 1.25
        _Brightness ("Brightness Multiplier", Range(0, 3)) = 1.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _Saturation;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                if (baseSample.a > 0.001)
                {
                    half luma = dot(baseSample.rgb, half3(0.299, 0.587, 0.114));
                    half3 boosted = lerp(luma.xxx, baseSample.rgb, _Saturation) * _Brightness;
                    return half4(boosted, baseSample.a);
                }

                float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
                half neighborAlpha = 0;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(texel.x, 0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(texel.x, 0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, texel.y)).a;

                if (neighborAlpha > 0.001)
                {
                    return half4(_OutlineColor.rgb, _OutlineColor.a);
                }

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}