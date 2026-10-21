using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Keeps the shore foam built by ShoreFoamGenerator moving: each layer's texture drifts and sways across the water,
    /// and its opacity swells and ebbs like waves washing against the rock, the lace a beat behind the surf. Sits on the
    /// `Shore Foam` group and gives every child renderer its own material copies at runtime, so the assets stay untouched.
    /// Each rock gets its own phase so they don't pulse in step.
    /// </summary>
    public class ShoreFoamMotion : MonoBehaviour
    {
        [System.Serializable]
        class Layer
        {
            [Tooltip("Steady drift of the texture, cm per second.")]
            public Vector2 driftCm;
            [Tooltip("Radius of the slow circular sway, cm.")]
            public float swayCm;
            [Tooltip("Seconds per sway circle.")]
            public float swayPeriod;
            [Tooltip("Seconds per swell.")]
            public float swellPeriod;
            [Tooltip("How much opacity drops between swells, percent.")]
            [Range(0f, 100f)] public float swellDepth;
            [Tooltip("How far this layer's swell lags the rock's phase, percent of a swell.")]
            [Range(0f, 100f)] public float swellLag;
        }

        // Material order on every foam renderer, as ShoreFoamGenerator assigns it.
        [SerializeField] Layer surf = new Layer { driftCm = Vector2.zero, swayCm = 45f, swayPeriod = 5f,
                                                  swellPeriod = 3.5f, swellDepth = 65f, swellLag = 0f };
        [SerializeField] Layer lace = new Layer { driftCm = new Vector2(15f, 10f), swayCm = 70f, swayPeriod = 7f,
                                                  swellPeriod = 3.5f, swellDepth = 50f, swellLag = 20f };
        [Tooltip("White water on the stone; drift Y runs up the rock.")]
        [SerializeField] Layer wash = new Layer { driftCm = new Vector2(5f, -25f), swayCm = 20f, swayPeriod = 3f,
                                                  swellPeriod = 3.5f, swellDepth = 75f, swellLag = 90f };

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        Layer[] layers;
        Material[][] instances;   // per renderer, one copy per layer
        Vector4[] baseSt;         // per layer: xy = repeats per metre, since the foam's UVs are in metres
        Color[] baseColor;
        float[] phases;

        void Awake()
        {
            layers = new[] { surf, lace, wash };
            var renderers = GetComponentsInChildren<Renderer>();
            instances = new Material[renderers.Length][];
            phases = new float[renderers.Length];
            baseSt = new Vector4[layers.Length];
            baseColor = new Color[layers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var shared = renderers[i].sharedMaterials;
                for (int l = 0; l < layers.Length && l < shared.Length; l++)
                {
                    var s = shared[l].GetTextureScale(BaseMap);
                    var o = shared[l].GetTextureOffset(BaseMap);
                    baseSt[l] = new Vector4(s.x, s.y, o.x, o.y);
                    baseColor[l] = shared[l].GetColor(BaseColor);
                }
                instances[i] = renderers[i].materials;  // own copies, so each rock keeps its own phase
                var c = renderers[i].bounds.center;
                phases[i] = Mathf.Repeat(c.x * 0.37f + c.z * 0.61f, 1f) * Mathf.PI * 2f;
            }
        }

        void OnDestroy()
        {
            foreach (var mats in instances)
            foreach (var m in mats)
                Destroy(m);
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < instances.Length; i++)
            for (int l = 0; l < layers.Length && l < instances[i].Length; l++)
                Apply(instances[i][l], layers[l], baseSt[l], baseColor[l], t, phases[i]);
        }

        static void Apply(Material mat, Layer layer, Vector4 st, Color color, float t, float phase)
        {
            float sway = Mathf.PI * 2f * t / layer.swayPeriod + phase;
            var metres = (layer.driftCm * t + layer.swayCm * new Vector2(Mathf.Cos(sway), Mathf.Sin(sway))) * 0.01f;
            var offset = new Vector2(st.z + metres.x * st.x, st.w + metres.y * st.y);
            mat.SetTextureOffset(BaseMap, new Vector2(Mathf.Repeat(offset.x, 1f), Mathf.Repeat(offset.y, 1f)));

            float swell = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * (t / layer.swellPeriod - layer.swellLag * 0.01f) + phase);
            color.a *= 1f - layer.swellDepth * 0.01f * (1f - swell);
            mat.SetColor(BaseColor, color);
        }
    }
}
