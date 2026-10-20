// Unlit falling water for the backdrop waterfall ribbons: procedural streaks scroll down the strip (UV v runs
// downward from the lip), soft across the width (UV u), additive so it glows a little in the dark cave.
Shader "ProjectX/Waterfall"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.6, 0.8, 1, 1)
        _Speed ("Fall Speed (UV per second)", Float) = 1.6
        _StreakDensity ("Streak Density", Float) = 14
        _StreakLength ("Streak Length", Float) = 1.5
        _BasePercent ("Body Opacity (%)", Range(0, 100)) = 25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Waterfall"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Speed;
                float _StreakDensity;
                float _StreakLength;
                half _BasePercent;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // v decreases down the fall, so adding time moves the pattern downward
                float2 p = float2(input.uv.x * _StreakDensity, input.uv.y / _StreakLength + _Time.y * _Speed);
                float streaks = ValueNoise(p) * 0.65 + ValueNoise(p * float2(2.3, 2.7) + 7.1) * 0.35;
                streaks = smoothstep(0.45, 0.8, streaks);
                half edge = saturate(sin(input.uv.x * PI));
                edge *= edge;
                half body = _BasePercent * 0.01;
                half3 color = _Color.rgb * (_Color.a * edge * (body + streaks));
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
