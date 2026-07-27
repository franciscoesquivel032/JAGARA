Shader "Jagara/FogUnlit"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.62, 0.62, 0.72, 1)
        _Density ("Density", Range(0, 1)) = 0.35
        _NoiseScale ("Noise Scale", Float) = 0.15
        _Drift1 ("Layer 1 Drift", Vector) = (0.02, 0.012, 0, 0)
        _Drift2 ("Layer 2 Drift", Vector) = (-0.016, 0.025, 0, 0)
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

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half _Density;
                float _NoiseScale;
                float4 _Drift1;
                float4 _Drift2;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float GradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = dot(Hash22(i) * 2.0 - 1.0, f);
                float b = dot(Hash22(i + float2(1, 0)) * 2.0 - 1.0, f - float2(1, 0));
                float c = dot(Hash22(i + float2(0, 1)) * 2.0 - 1.0, f - float2(0, 1));
                float d = dot(Hash22(i + float2(1, 1)) * 2.0 - 1.0, f - float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz).xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 p1 = IN.worldPos * _NoiseScale + _Drift1.xy * t;
                float2 p2 = IN.worldPos * _NoiseScale * 1.9 + _Drift2.xy * t;
                float n1 = GradientNoise(p1);
                float n2 = GradientNoise(p2);
                float n = saturate((n1 * 0.65 + n2 * 0.35) * 0.5 + 0.5);
                n = smoothstep(0.25, 0.85, n);
                half alpha = n * _Density * _FogColor.a;
                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
