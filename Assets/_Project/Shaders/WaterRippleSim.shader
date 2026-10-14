// Height-field ripple simulation driven by WaterRipples. R = current height, G = previous height.
// Pass 0 scrolls the field (the area follows Rian) and stamps new drops; pass 1 runs one wave-equation step.
Shader "Hidden/ProjectX/WaterRippleSim"
{
    Properties
    {
        _MainTex ("Field", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;

        float Inside(float2 uv)
        {
            return all(uv >= 0) && all(uv <= 1) ? 1 : 0;
        }

        float4 Field(float2 uv)
        {
            return tex2D(_MainTex, uv) * Inside(uv);
        }
        ENDCG

        Pass
        {
            Name "ShiftAndDrops"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            // xy = world XZ of the field's min corner, zw = world size.
            float4 _Area;
            float2 _Shift;
            // xy = world XZ, z = radius, w = height added at the centre.
            float4 _Drops[16];
            float _DropCount;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 field = Field(i.uv + _Shift);
                float2 world = _Area.xy + i.uv * _Area.zw;
                [loop] for (int d = 0; d < (int)_DropCount; d++)
                {
                    float2 delta = world - _Drops[d].xy;
                    float r = _Drops[d].z;
                    field.r += _Drops[d].w * exp(-dot(delta, delta) / (r * r));
                }
                return field;
            }
            ENDCG
        }

        Pass
        {
            Name "Step"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            float _Propagation;
            float _Damping;

            float4 frag(v2f_img i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy;
                float4 c = Field(i.uv);
                float sum = Field(i.uv + float2(t.x, 0)).r + Field(i.uv - float2(t.x, 0)).r
                          + Field(i.uv + float2(0, t.y)).r + Field(i.uv - float2(0, t.y)).r;
                float next = (2 * c.r - c.g + _Propagation * (sum - 4 * c.r)) * _Damping;

                // Soak waves up near the border so they don't bounce off the edge of the field.
                float2 edge = min(i.uv, 1 - i.uv);
                next *= smoothstep(0, 0.06, min(edge.x, edge.y));
                return float4(next, c.r, 0, 0);
            }
            ENDCG
        }
    }
}
