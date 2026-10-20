// Unlit cloud card for the sky behind Zone 1: the texture (baked volumetric cloud from Art/Level, grey = light,
// alpha = density) is tinted between a shadow and a lit colour. Not fogged (the cards sit far past the fog), and
// the alpha fades out near the card borders so no rectangle edge ever shows.
Shader "ProjectX/CloudCard"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Cloud (A = density)", 2D) = "white" {}
        _LitColor ("Lit Colour", Color) = (1, 0.86, 0.7, 1)
        _ShadowColor ("Shadow Colour", Color) = (0.42, 0.46, 0.56, 1)
        _OpacityPercent ("Opacity (%)", Range(0, 100)) = 90
        _EdgeFadePercent ("Border Fade (% of card)", Range(1, 40)) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "CloudCard"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _LitColor;
                half4 _ShadowColor;
                half _OpacityPercent;
                half _EdgeFadePercent;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 cloud = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half e = _EdgeFadePercent * 0.01;
                half border = smoothstep(0, e, input.uv.x) * smoothstep(1, 1 - e, input.uv.x)
                            * smoothstep(0, e, input.uv.y) * smoothstep(1, 1 - e, input.uv.y);
                half3 color = lerp(_ShadowColor.rgb, _LitColor.rgb, cloud.r);
                return half4(color, cloud.a * border * _OpacityPercent * 0.01);
            }
            ENDHLSL
        }
    }
}
