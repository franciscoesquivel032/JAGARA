Shader "Jagara/EntityFactionOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 2)) = 0.5
        _OutlineIntensity ("Outline Intensity", Range(0, 1)) = 0.55
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
                float _OutlineIntensity;
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

                float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
                half neighborAlpha = 0;
                neighborAlpha = max(neighborAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, IN.uv + float2(texel.x, 0)).a);
                neighborAlpha = max(neighborAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, IN.uv - float2(texel.x, 0)).a);
                neighborAlpha = max(neighborAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, IN.uv + float2(0, texel.y)).a);
                neighborAlpha = max(neighborAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, IN.uv - float2(0, texel.y)).a);

                half outlineAlpha = neighborAlpha * _OutlineIntensity * _OutlineColor.a;
                return half4(_OutlineColor.rgb, outlineAlpha);
            }
            ENDHLSL
        }
    }
}
