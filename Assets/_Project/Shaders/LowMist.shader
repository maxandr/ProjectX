// Unlit alpha-blended sheet of low mist for vertical quads standing on the water.
// UV.y runs from the water line (0) to the top of the sheet (1); UV.x along its length.
// Density thins with height and toward the ends, drifts as world-space noise, and fades where the
// sheet meets geometry (soft particles), so the quads never show a hard edge.
Shader "ProjectX/LowMist"
{
    Properties
    {
        _Color ("Mist Color (A = density)", Color) = (0.95, 0.78, 0.6, 0.55)
        _SunTint ("Sun Tint", Range(0, 2)) = 0.6
        _HeightPower ("Height Falloff", Range(0.3, 6)) = 1.8
        _EndFade ("End Fade (UV x)", Range(0.01, 0.5)) = 0.2
        _NoiseScale ("Noise Scale (per metre)", Float) = 0.12
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.7
        _DriftSpeed ("Drift Speed (m/s)", Float) = 0.6
        _SoftDistance ("Soft Intersection (m)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "LowMist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _SunTint;
                half _HeightPower;
                half _EndFade;
                float _NoiseScale;
                half _NoiseStrength;
                float _DriftSpeed;
                float _SoftDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.screenPos = ComputeScreenPos(pos.positionCS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half height = pow(saturate(1.0 - input.uv.y), _HeightPower);
                half ends = saturate(input.uv.x / _EndFade) * saturate((1.0 - input.uv.x) / _EndFade);

                // Two octaves drifting at different speeds; Y is in the noise so the sheet billows.
                float t = _Time.y * _DriftSpeed;
                float2 p = float2(input.positionWS.x + input.positionWS.z * 0.37, input.positionWS.y * 2.5) * _NoiseScale;
                float n = ValueNoise(p + float2(t * _NoiseScale, 0))
                        * 0.65 + ValueNoise(p * 2.7 + float2(-t * _NoiseScale * 1.6, t * _NoiseScale * 0.4)) * 0.35;
                half noise = lerp(1.0h, saturate(n * 1.6 - 0.25), _NoiseStrength);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float soft = saturate((sceneDepth - input.screenPos.w) / _SoftDistance);

                half alpha = _Color.a * height * ends * noise * soft;

                Light sun = GetMainLight();
                half3 color = _Color.rgb + sun.color * _SunTint * 0.25h;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
