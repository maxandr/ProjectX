// Unlit additive glow for the hair ribbon (TrailRenderer) and skylight beams (quads).
// "Across" is the axis that gets soft edges; "along" optionally fades toward 0.
Shader "ProjectX/AdditiveGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _EdgePower ("Edge Softness Power", Float) = 1
        _FadePower ("Fade Power", Float) = 1
        [ToggleUI] _UseFade ("Fade Along Length", Float) = 0
        [ToggleUI] _AcrossIsV ("Across Axis Is V", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "AdditiveGlow"
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
                half _EdgePower;
                half _FadePower;
                half _UseFade;
                half _AcrossIsV;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float across = lerp(input.uv.x, input.uv.y, _AcrossIsV);
                float along = lerp(input.uv.y, input.uv.x, _AcrossIsV);
                half edge = pow(saturate(sin(across * PI)), _EdgePower);
                half fade = lerp(1.0h, pow(saturate(along), _FadePower), _UseFade);
                half3 color = _Color.rgb * input.color.rgb * (_Color.a * input.color.a * edge * fade);
                // Additive: fog fades the glow toward black rather than toward the fog color.
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
