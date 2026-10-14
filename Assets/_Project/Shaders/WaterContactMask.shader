// Used by WaterContactFoam. Pass 0 draws a renderer top-down into the mask, keeping only the slice of it at the
// waterline, which gives the outline where it pierces the water.
// Pass 1 is one direction of a Gaussian blur that turns the hard mask into a soft "distance to shore".
Shader "Hidden/ProjectX/WaterContactMask"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Footprint"
            BlendOp Max
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // xy = world XZ of the mask's min corner, zw = 1 / world size.
            float4 _ContactArea;
            float _ContactSurfaceY;
            // x = slab depth below the surface, y = slab height above it.
            float2 _ContactSlab;
            // XZ shear per metre of height. A straight-down view collapses vertical walls to zero-area lines;
            // shearing gives them width. Drawn twice with perpendicular shears so every wall shows in one.
            float2 _ContactShear;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float worldY : TEXCOORD0;
                float normalY : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL)
            {
                float3 world = mul(unity_ObjectToWorld, vertex).xyz;
                float2 xz = world.xz + (world.y - _ContactSurfaceY) * _ContactShear;
                float2 uv = (xz - _ContactArea.xy) * _ContactArea.zw;
                v2f o;
                o.pos = float4(uv * 2 - 1, 0.5, 1);
                #if UNITY_UV_STARTS_AT_TOP
                o.pos.y = -o.pos.y;
                #endif
                o.worldY = world.y;
                o.normalY = UnityObjectToWorldNormal(normal).y;
                return o;
            }

            // Only the thin slice of geometry around the waterline, and only its steep faces,
            // so floors just under the water and ceilings above it stay out of the mask.
            float4 frag(v2f i) : SV_Target
            {
                float h = i.worldY - _ContactSurfaceY;
                clip(h + _ContactSlab.x);
                clip(_ContactSlab.y - h);
                clip(0.7 - abs(i.normalY));
                return 1;
            }
            ENDCG
        }

        Pass
        {
            Name "Blur"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            // UV offset between taps.
            float2 _BlurStep;

            float4 frag(v2f_img i) : SV_Target
            {
                static const float weights[7] = { 0.1585, 0.1465, 0.1156, 0.0779, 0.0448, 0.0220, 0.0092 };
                float sum = tex2D(_MainTex, i.uv).r * weights[0];
                for (int k = 1; k < 7; k++)
                {
                    sum += tex2D(_MainTex, i.uv + _BlurStep * k).r * weights[k];
                    sum += tex2D(_MainTex, i.uv - _BlurStep * k).r * weights[k];
                }
                return sum;
            }
            ENDCG
        }
    }
}
