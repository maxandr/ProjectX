// Unlit, unfogged glowing card for button prompts (the key icon and its line). Premultiplied alpha, so HDR colour
// blooms. The quad turns around the vertical axis through its pivot to face the camera, so cards stacked on one
// pivot column stay lined up. _Alpha is the fade driven by ButtonPrompt; _Pulse makes the glow breathe.
Shader "ProjectX/PromptGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.15
        _PulseSpeed ("Pulse Speed", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "PromptGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Alpha;
                half _PulseAmount;
                half _PulseSpeed;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                float3 toCamera = _WorldSpaceCameraPos - pivot;
                toCamera.y = 0;
                float3 forward = normalize(toCamera + float3(0, 0, 1e-4));
                float3 right = normalize(cross(forward, float3(0, 1, 0)));
                float2 scale = float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21));
                float3 positionWS = pivot + right * (input.positionOS.x * scale.x) + float3(0, input.positionOS.y * scale.y, 0);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half pulse = 1.0h + _PulseAmount * sin(_Time.y * _PulseSpeed);
                half alpha = tex.a * _Color.a * _Alpha;
                return half4(tex.rgb * _Color.rgb * alpha * pulse, alpha);
            }
            ENDHLSL
        }
    }
}
