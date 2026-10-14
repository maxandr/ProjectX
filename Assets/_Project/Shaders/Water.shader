// Water surface: refraction through the camera opaque texture with depth-based absorption, a screen-space
// raymarched reflection (falls back to the reflection probe / sky), sun glints and contact foam.
// The normal combines a few scrolling wind waves with the global ripple field from WaterRipples.
// Needs Depth Texture and Opaque Texture enabled on the URP asset.
Shader "ProjectX/Water"
{
    Properties
    {
        [Header(Colour)]
        _BaseColor ("Deep Color", Color) = (0.06, 0.16, 0.18, 1)
        _ShallowColor ("Shallow Tint", Color) = (0.75, 0.9, 0.9, 1)
        _Absorption ("Absorption (per metre of view ray)", Range(0, 10)) = 1.5
        _Refraction ("Refraction", Range(0, 0.2)) = 0.03

        [Header(Reflection)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.97
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 1
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 4
        _F0 ("Reflectance At Normal Incidence", Range(0, 1)) = 0.04
        [ToggleUI] _SSR ("Screen Space Reflection", Float) = 1
        _SSRDistance ("SSR Max Distance", Range(2, 60)) = 20
        _SSRThickness ("SSR Thickness", Range(0.05, 3)) = 0.5
        _Specular ("Sun Glint", Range(0, 20)) = 4

        [Header(Waves)]
        _WaveStrength ("Wind Wave Slope", Range(0, 0.5)) = 0.04
        _WaveScale ("Wind Wave Frequency", Range(0.1, 6)) = 1.5
        _WaveSpeed ("Wind Wave Speed", Range(0, 5)) = 1
        _RippleStrength ("Ripple Strength", Range(0, 4)) = 1

        [Header(Foam)]
        _FoamColor ("Foam Color (A = amount)", Color) = (0.9, 0.95, 1, 0.6)
        _FoamDistance ("Foam Depth", Range(0, 0.5)) = 0.04

        [Header(Shore Glow)]
        [HDR] _ShoreColor ("Shore Glow Color", Color) = (0.75, 0.85, 0.95, 0.5)
        _ShoreGain ("Shore Mask Gain", Range(0.5, 15)) = 6
        _ShoreLine ("Contact Line Width", Range(0, 1)) = 0.2
        _ShoreRingCount ("Ring Count", Range(0, 8)) = 3
        _ShoreRingWidth ("Ring Width", Range(0.01, 1)) = 0.25
        _ShoreRingSpeed ("Ring Speed", Range(-2, 2)) = 0.35
        _ShoreWobble ("Ring Wobble", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            // The shader composites the refracted scene itself, so it writes final colour.
            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // We trace our own reflection; skip URP's SSR lookup inside GlossyEnvironmentReflection.
            #define _SCREENSPACEREFLECTIONS_OFF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #define SSR_STEPS 32

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShallowColor;
                half _Absorption;
                half _Refraction;
                half _Smoothness;
                half _ReflectionStrength;
                half _FresnelPower;
                half _F0;
                half _SSR;
                float _SSRDistance;
                float _SSRThickness;
                half _Specular;
                half _WaveStrength;
                float _WaveScale;
                float _WaveSpeed;
                half _RippleStrength;
                half4 _FoamColor;
                float _FoamDistance;
                half4 _ShoreColor;
                half _ShoreGain;
                half _ShoreLine;
                float _ShoreRingCount;
                half _ShoreRingWidth;
                float _ShoreRingSpeed;
                half _ShoreWobble;
            CBUFFER_END

            // Set globally by WaterRipples. Area: xy = world XZ of the field's min corner, zw = 1 / world size.
            // Texel: xy = 1 / resolution, z = world size of one texel.
            TEXTURE2D(_WaterRippleTex);
            SAMPLER(sampler_WaterRippleTex);
            float4 _WaterRippleArea;
            float4 _WaterRippleTexel;
            // Set globally by WaterContactFoam: soft top-down mask of things poking out of the water.
            // Area: xy = world XZ of the mask's min corner, zw = 1 / world size.
            TEXTURE2D(_WaterContactTex);
            SAMPLER(sampler_WaterContactTex);
            float4 _WaterContactArea;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Slope (dh/dx, dh/dz) of a sum of scrolling sine waves.
            float2 WindSlope(float2 p)
            {
                static const float2 dirs[5] = { float2(1, 0.3), float2(-0.4, 1), float2(0.8, -0.7), float2(-1, -0.2), float2(0.2, 0.9) };
                static const float freqs[5] = { 1, 1.7, 2.9, 4.3, 6.1 };
                static const float weights[5] = { 0.35, 0.25, 0.18, 0.13, 0.09 };
                float2 slope = 0;
                for (int i = 0; i < 5; i++)
                {
                    float2 d = normalize(dirs[i]);
                    float f = freqs[i] * _WaveScale;
                    float phase = dot(d, p) * f + _Time.y * _WaveSpeed * sqrt(f) * 1.5;
                    slope += d * cos(phase) * weights[i];
                }
                return slope;
            }

            float RippleHeight(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_WaterRippleTex, sampler_WaterRippleTex, uv, 0).r;
            }

            float2 RippleSlope(float2 xz)
            {
                if (_WaterRippleArea.z == 0)
                    return 0;
                float2 uv = (xz - _WaterRippleArea.xy) * _WaterRippleArea.zw;
                float2 edge = min(uv, 1 - uv);
                float fade = smoothstep(0, 0.05, min(edge.x, edge.y));
                if (fade <= 0)
                    return 0;
                float2 e = _WaterRippleTexel.xy;
                float dx = RippleHeight(uv + float2(e.x, 0)) - RippleHeight(uv - float2(e.x, 0));
                float dz = RippleHeight(uv + float2(0, e.y)) - RippleHeight(uv - float2(0, e.y));
                return float2(dx, dz) / (2 * _WaterRippleTexel.z) * fade;
            }

            // 0 in open water, rising to 1 at the waterline of anything poking out.
            float ShoreProximity(float2 xz)
            {
                if (_WaterContactArea.z == 0)
                    return 0;
                float2 uv = (xz - _WaterContactArea.xy) * _WaterContactArea.zw;
                float2 edge = min(uv, 1 - uv);
                float fade = smoothstep(0, 0.05, min(edge.x, edge.y));
                return saturate(SAMPLE_TEXTURE2D_LOD(_WaterContactTex, sampler_WaterContactTex, uv, 0).r * _ShoreGain) * fade;
            }

            // Glowing rings that pulse outward from the waterline, broken up by the wave normal.
            half ShoreGlow(float2 xz, float2 slope)
            {
                float q = ShoreProximity(xz);
                if (q <= 0.001)
                    return 0;
                float d = 1 - q + (slope.x + slope.y) * _ShoreWobble;
                float wave = frac(d * _ShoreRingCount - _Time.y * _ShoreRingSpeed);
                half ring = smoothstep(1 - _ShoreRingWidth, 1 - _ShoreRingWidth * 0.5, wave) * (1 - smoothstep(0.92, 1, wave));
                half contact = smoothstep(1 - _ShoreLine, 1 - _ShoreLine * 0.4, q);
                return saturate(max(contact, ring * q * q * 0.6));
            }

            float2 ProjectToScreenUV(float3 positionWS, out float eyeDepth)
            {
                float4 cs = TransformWorldToHClip(positionWS);
                eyeDepth = cs.w;
                float2 ndc = cs.xy / cs.w;
                ndc.y *= _ProjectionParams.x;
                return ndc * 0.5 + 0.5;
            }

            float SceneEyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            // Marches the reflected ray against the depth texture. rgb = scene colour at the hit, a = confidence.
            half4 TraceReflection(float3 originWS, float3 dirWS, float2 pixel)
            {
                float jitter = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                float prevT = 0;
                UNITY_LOOP
                for (int i = 1; i <= SSR_STEPS; i++)
                {
                    float s = (i - jitter) / SSR_STEPS;
                    float t = _SSRDistance * s * s;
                    float rayDepth;
                    float2 uv = ProjectToScreenUV(originWS + dirWS * t, rayDepth);
                    if (rayDepth <= _ProjectionParams.y || any(uv < 0) || any(uv > 1))
                        break;

                    float dz = rayDepth - SceneEyeDepth(uv);
                    if (dz > 0 && dz < _SSRThickness + t * 0.1)
                    {
                        float lo = prevT, hi = t;
                        for (int j = 0; j < 4; j++)
                        {
                            float mid = 0.5 * (lo + hi);
                            float midDepth;
                            float2 midUV = ProjectToScreenUV(originWS + dirWS * mid, midDepth);
                            if (midDepth > SceneEyeDepth(midUV))
                                hi = mid;
                            else
                                lo = mid;
                        }
                        uv = ProjectToScreenUV(originWS + dirWS * hi, rayDepth);
                        float2 edge = min(uv, 1 - uv);
                        half fade = smoothstep(0, 0.08, min(edge.x, edge.y)) * (1 - smoothstep(0.6, 1, hi / _SSRDistance));
                        return half4(SampleSceneColor(uv), fade);
                    }
                    prevT = t;
                }
                return 0;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 viewVec = positionWS - _WorldSpaceCameraPos;
                float viewDist = length(viewVec);
                float3 V = -viewVec / viewDist;

                // Wind detail fades with distance so it doesn't shimmer at grazing angles.
                float windFade = saturate(1 - viewDist / 60);
                float2 slope = WindSlope(positionWS.xz) * _WaveStrength * windFade + RippleSlope(positionWS.xz) * _RippleStrength;
                float3 N = normalize(float3(-slope.x, 1, -slope.y));

                // Refraction: offset the lookup, but not onto things in front of the water.
                float surfaceDepth = -TransformWorldToView(positionWS).z;
                float2 refractUV = screenUV + N.xz * _Refraction;
                float sceneDepth = SceneEyeDepth(refractUV);
                if (sceneDepth < surfaceDepth)
                {
                    refractUV = screenUV;
                    sceneDepth = SceneEyeDepth(screenUV);
                }
                float pathLength = max(0, sceneDepth - surfaceDepth) * viewDist / surfaceDepth;
                float waterDepth = pathLength * abs(V.y);

                Light light = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 litColor = ambient + light.color * saturate(light.direction.y) * atten;

                half transmittance = exp(-pathLength * _Absorption);
                half3 deep = _BaseColor.rgb * litColor;
                half3 color = lerp(deep, SampleSceneColor(refractUV) * _ShallowColor.rgb, transmittance);

                // Reflection.
                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                half perceptualRoughness = 1 - _Smoothness;
                half3 reflection = GlossyEnvironmentReflection(R, positionWS, perceptualRoughness, 1, screenUV);
                if (_SSR > 0.5)
                {
                    half4 traced = TraceReflection(positionWS + float3(0, 0.02, 0), R, input.positionCS.xy);
                    reflection = lerp(reflection, traced.rgb, traced.a);
                }
                half fresnel = _F0 + (1 - _F0) * pow(1 - saturate(dot(N, V)), _FresnelPower);
                color = lerp(color, reflection, saturate(fresnel * _ReflectionStrength));

                // Sun glint.
                float3 H = normalize(light.direction + V);
                float specPower = exp2(10 * _Smoothness + 1);
                color += light.color * (atten * _Specular * pow(saturate(dot(N, H)), specPower));

                // Foam where the water is very shallow (against stones, legs, banks).
                half foam = saturate(1 - waterDepth / max(_FoamDistance, 1e-4)) * _FoamColor.a;
                color = lerp(color, _FoamColor.rgb * litColor, foam);

                // Shore glow is emissive so it still reads in the dark cavern.
                color += _ShoreColor.rgb * (_ShoreColor.a * ShoreGlow(positionWS.xz, slope));

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    CustomEditor "ProjectX.WaterShaderGUI"
}
