Shader "Jagara/EntityFactionOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Glow Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Glow Radius (texels)", Range(1, 4)) = 2
        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0.3
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
                float _GlowIntensity;
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
                    return baseSample;
                }

                static const int MAX_RINGS = 4;
                float2 texel = _MainTex_TexelSize.xy;
                half glow = 0;

                [unroll]
                for (int r = 1; r <= MAX_RINGS; r++)
                {
                    if (r > (int)_OutlineWidth)
                    {
                        break;
                    }

                    float2 offset = texel * r;
                    half ringAlpha = 0;
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(offset.x, 0)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(offset.x, 0)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(offset.x, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(offset.x, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(offset.x, -offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x, offset.y)).a);

                    half falloff = 1.0 - (half)(r - 1) / (half)_OutlineWidth;
                    glow = max(glow, ringAlpha * falloff);
                }

                glow *= _GlowIntensity;
                return half4(_OutlineColor.rgb, glow);
            }
            ENDHLSL
        }
    }
}