// World-space triplanar PBR for the level's stone and wood. The detail map is a neutral texture baked in
// Art/Level/Proto_Level.blend (alpha = height); the material tint keeps the level's palette. An optional
// moss layer grows on up-facing surfaces.
Shader "ProjectX/TriplanarStone"
{
    Properties
    {
        _BaseColor("Tint", Color) = (0.5, 0.5, 0.5, 1)
        [NoScaleOffset] _DetailMap("Detail Albedo (A = height)", 2D) = "grey" {}
        [NoScaleOffset][Normal] _NormalMap("Detail Normal", 2D) = "bump" {}
        _TileSize("Tile Size (m)", Float) = 2
        _DetailBoost("Detail Brightness (x)", Float) = 2.2
        _NormalStrength("Normal Strength (%)", Range(0, 200)) = 100
        _SmoothnessPercent("Smoothness (%)", Range(0, 100)) = 30
        _MacroPercent("Large-Scale Variation (%)", Range(0, 100)) = 35

        [NoScaleOffset] _MossMap("Moss Albedo (A = height)", 2D) = "black" {}
        [NoScaleOffset][Normal] _MossNormal("Moss Normal", 2D) = "bump" {}
        _MossTileSize("Moss Tile Size (m)", Float) = 1
        _MossPercent("Moss Amount (%)", Range(0, 100)) = 0
        _MossSharpness("Moss Edge Sharpness", Range(1, 20)) = 6

        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _TileSize;
            half _DetailBoost;
            half _NormalStrength;
            half _SmoothnessPercent;
            half _MacroPercent;
            float _MossTileSize;
            half _MossPercent;
            half _MossSharpness;
            half4 _EmissionColor;
        CBUFFER_END

        TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MossMap); SAMPLER(sampler_MossMap);
        TEXTURE2D(_MossNormal); SAMPLER(sampler_MossNormal);

        float3 TriWeights(float3 n)
        {
            float3 w = pow(abs(n), 4.0);
            return w / (w.x + w.y + w.z);
        }

        half4 TriSample(TEXTURE2D_PARAM(tex, smp), float3 p, float3 w)
        {
            return SAMPLE_TEXTURE2D(tex, smp, p.zy) * w.x
                 + SAMPLE_TEXTURE2D(tex, smp, p.xz) * w.y
                 + SAMPLE_TEXTURE2D(tex, smp, p.xy) * w.z;
        }

        // Whiteout-blended triplanar normal (Ben Golus), returns a world-space normal.
        float3 TriNormal(TEXTURE2D_PARAM(tex, smp), float3 p, float3 n, float3 w, half strength)
        {
            float3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, smp, p.zy), strength);
            float3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, smp, p.xz), strength);
            float3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, smp, p.xy), strength);
            tx = float3(tx.xy + n.zy, abs(tx.z) * n.x);
            ty = float3(ty.xy + n.xz, abs(ty.z) * n.y);
            tz = float3(tz.xy + n.xy, abs(tz.z) * n.z);
            return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
        }

        struct StoneSurface
        {
            half3 albedo;
            float3 normalWS;
            half smoothness;
            half occlusion;
        };

        StoneSurface EvaluateStone(float3 positionWS, float3 normalWS)
        {
            StoneSurface s;
            float3 n = normalize(normalWS);
            float3 w = TriWeights(n);
            float3 p = positionWS / max(_TileSize, 0.01);

            half4 detail = TriSample(TEXTURE2D_ARGS(_DetailMap, sampler_DetailMap), p, w);
            half macro = TriSample(TEXTURE2D_ARGS(_DetailMap, sampler_DetailMap), p * 0.137 + 0.31, w).a;
            half macroTone = lerp(1.0, 0.65 + macro * 0.7, _MacroPercent * 0.01);
            s.albedo = _BaseColor.rgb * detail.rgb * _DetailBoost * macroTone;
            s.normalWS = TriNormal(TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), p, n, w, _NormalStrength * 0.01);
            half height = detail.a;
            s.smoothness = _SmoothnessPercent * 0.01 * saturate(0.4 + height);
            s.occlusion = saturate(0.55 + height * 0.7);

            UNITY_BRANCH
            if (_MossPercent > 0)
            {
                float3 mp = positionWS / max(_MossTileSize, 0.01);
                half4 moss = TriSample(TEXTURE2D_ARGS(_MossMap, sampler_MossMap), mp, w);
                half up = saturate(s.normalWS.y);
                half cover = saturate((up + moss.a * 0.6 + macro * 0.4 - (1.6 - _MossPercent * 0.012)) * _MossSharpness);
                s.albedo = lerp(s.albedo, moss.rgb, cover);
                float3 mossN = TriNormal(TEXTURE2D_ARGS(_MossNormal, sampler_MossNormal), mp, n, w, 1.0);
                s.normalWS = normalize(lerp(s.normalWS, mossN, cover));
                s.smoothness = lerp(s.smoothness, 0.05, cover);
            }
            return s;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                StoneSurface stone = EvaluateStone(input.positionWS, input.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = stone.normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(stone.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = stone.albedo;
                surface.smoothness = stone.smoothness;
                surface.occlusion = stone.occlusion;
                surface.normalTS = half3(0, 0, 1);
                surface.emission = _EmissionColor.rgb;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = EvaluateStone(input.positionWS, input.normalWS).normalWS;
                return half4(NormalizeNormalPerPixel(n), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
