// Mesh-particle gull (Meshes/Birds/Gull.asset) that flaps in the vertex shader. The particle renderer aligns each
// mesh to its velocity, so the flap axis is the velocity itself: each wing vertex's offset from that axis is turned
// around it, the outer wing further than the inner one. The mesh stores UV.x = colour slot (0 body, 0.25 beak,
// 0.5 wing, 1 tip) and UV.y = signed span (mesh units, + right wing); the particle system sends these custom
// vertex streams: Position, Normal, Color, UV, StableRandom.x, SizeX (TEXCOORD0), Center (TEXCOORD1),
// Velocity (TEXCOORD2). Lit by the main light and ambient, fogged, faded by the particle colour's alpha.
Shader "ProjectX/GullParticle"
{
    Properties
    {
        _BodyColor ("Body", Color) = (0.86, 0.87, 0.89, 1)
        _WingTopColor ("Wing Top", Color) = (0.62, 0.65, 0.7, 1)
        _WingUnderColor ("Wing Underside", Color) = (0.82, 0.83, 0.86, 1)
        _TipColor ("Wing Tip", Color) = (0.04, 0.04, 0.045, 1)
        _BeakColor ("Beak", Color) = (0.85, 0.65, 0.12, 1)
        _FlapsPerSecond ("Flaps per Second", Range(1, 6)) = 2.6
        _FlapDegrees ("Flap (degrees)", Range(1, 60)) = 34
        _ElbowDegrees ("Outer Wing Extra (degrees)", Range(1, 45)) = 24
        _GlidePercent ("Time Spent Gliding (%)", Range(1, 90)) = 35
        _AmbientPercent ("Ambient (%)", Range(1, 300)) = 150
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GullParticle"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor;
                half4 _WingTopColor;
                half4 _WingUnderColor;
                half4 _TipColor;
                half4 _BeakColor;
                float _FlapsPerSecond;
                float _FlapDegrees;
                float _ElbowDegrees;
                float _GlidePercent;
                half _AmbientPercent;
            CBUFFER_END

            struct Attributes
            {
                float3 positionWS : POSITION;
                float3 normalWS : NORMAL;
                half4 color : COLOR;
                float4 uvRandomSize : TEXCOORD0; // xy mesh UV, z stable random, w size
                float3 center : TEXCOORD1;
                float3 velocity : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                float slot : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            // Rotates v around the unit axis a by angle (radians); v is perpendicular to a.
            float3 RotateAround(float3 v, float3 a, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return v * c + cross(a, v) * s;
            }

            Varyings vert(Attributes input)
            {
                float span = input.uvRandomSize.y;
                float rnd = input.uvRandomSize.z;
                float3 forward = normalize(input.velocity + float3(0, 0, 1e-5));

                float t = _Time.y * _FlapsPerSecond * (0.85 + 0.3 * rnd) + rnd * 37.0;
                float phase = t * TWO_PI;
                // A slow per-gull wave switches between flapping and gliding with the wings held slightly up.
                float wave = sin(_Time.y * 0.45 + rnd * 23.0) * 0.5 + 0.5;
                float glideFrom = 1 - _GlidePercent * 0.01;
                float glide = smoothstep(glideFrom - 0.1, glideFrom + 0.1, wave);
                float inner = radians(lerp(8 + _FlapDegrees * sin(phase), 6, glide));
                float outer = radians(lerp(4 + _ElbowDegrees * sin(phase - 0.9), 2, glide));
                float outerWeight = saturate((abs(span) - 0.45) / 0.4);
                float angle = sign(span) * (inner + outer * outerWeight);

                float3 offset = input.positionWS - input.center;
                float3 lateral = offset - forward * dot(offset, forward);
                float3 along = offset - lateral;
                float3 positionWS = input.center + along + RotateAround(lateral, forward, angle);
                float3 normalWS = RotateAround(input.normalWS - forward * dot(input.normalWS, forward), forward, angle)
                                + forward * dot(input.normalWS, forward);

                Varyings output;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.color = input.color;
                output.slot = input.uvRandomSize.x;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS) * (frontFace ? 1 : -1);
                half3 albedo = _BodyColor.rgb;
                if (input.slot > 0.9) albedo = _TipColor.rgb;
                else if (input.slot > 0.4) albedo = frontFace ? _WingTopColor.rgb : _WingUnderColor.rgb;
                else if (input.slot > 0.15) albedo = _BeakColor.rgb;

                Light light = GetMainLight();
                half3 lit = light.color * saturate(dot(normalWS, light.direction) * 0.5 + 0.5)
                          + SampleSH(normalWS) * _AmbientPercent * 0.01;
                half3 color = MixFog(albedo * lit, input.fogFactor);
                return half4(color, input.color.a);
            }
            ENDHLSL
        }
    }
}
