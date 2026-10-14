using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Runs a ripple height field on the GPU over a window of the XZ plane that follows Rian, and publishes it
    /// to every <c>ProjectX/Water</c> material as global shader values. <see cref="WaterRippleEmitter"/>s add drops.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class WaterRipples : MonoBehaviour
    {
        const int MaxDrops = 16;
        const int ShiftAndDropsPass = 0;
        const int StepPass = 1;

        static WaterRipples instance;

        static readonly int AreaId = Shader.PropertyToID("_Area");
        static readonly int ShiftId = Shader.PropertyToID("_Shift");
        static readonly int DropsId = Shader.PropertyToID("_Drops");
        static readonly int DropCountId = Shader.PropertyToID("_DropCount");
        static readonly int PropagationId = Shader.PropertyToID("_Propagation");
        static readonly int DampingId = Shader.PropertyToID("_Damping");
        static readonly int RippleTexId = Shader.PropertyToID("_WaterRippleTex");
        static readonly int RippleAreaId = Shader.PropertyToID("_WaterRippleArea");
        static readonly int RippleTexelId = Shader.PropertyToID("_WaterRippleTexel");

        [SerializeField] Shader simulationShader;
        [Tooltip("The field is centred on this; defaults to Rian.")]
        [SerializeField] Transform follow;
        [Tooltip("World size (X, Z) of the simulated window.")]
        public Vector2 areaSize = new Vector2(28f, 14f);
        [Tooltip("Texels along X; Z follows the area's aspect.")]
        public int resolution = 512;
        [Tooltip("Wave speed per step; must stay at or below 0.5 to remain stable.")]
        [Range(0.05f, 0.5f)] public float propagation = 0.3f;
        [Tooltip("Amplitude kept per simulation step.")]
        [Range(0.9f, 1f)] public float damping = 0.985f;
        public float stepsPerSecond = 60f;

        [Header("Character Ripples (scaled per character by WaterRippleEmitter.scale)")]
        [Tooltip("Height of each wake ring while wading.")]
        [Range(0f, 0.1f)] public float wakeHeight = 0.02f;
        [Tooltip("Radius (m) of each wake drop.")]
        [Range(0.03f, 0.5f)] public float wakeRadius = 0.14f;
        [Tooltip("Distance moved (m) between wake drops.")]
        [Range(0.05f, 0.5f)] public float wakeSpacing = 0.15f;
        [Tooltip("Height of the splash when entering the water.")]
        [Range(0f, 0.2f)] public float splashHeight = 0.05f;
        [Tooltip("Radius (m) of the splash.")]
        [Range(0.05f, 1f)] public float splashRadius = 0.3f;
        [Tooltip("Fall speed (m/s) at which the splash reaches full height.")]
        public float splashSpeed = 5f;
        [Tooltip("How far above the surface a character's feet still touch the water.")]
        public float contactHeight = 0.05f;
        [Tooltip("How far below the surface a character still makes ripples.")]
        public float wadeDepth = 1.5f;

        public static WaterRipples Active => instance;

        readonly Vector4[] drops = new Vector4[MaxDrops];
        int dropCount;

        Material material;
        RenderTexture current, next;
        Vector2 origin;
        float texel;
        float stepDebt;
        bool hasOrigin;

        /// <summary>Queues a drop centred at <paramref name="position"/> (XZ). Negative height pushes the surface down.</summary>
        public static void AddDrop(Vector3 position, float radius, float height)
        {
            if (instance == null || instance.dropCount >= MaxDrops)
                return;
            instance.drops[instance.dropCount++] = new Vector4(position.x, position.z, radius, height);
        }

        void OnEnable()
        {
            instance = this;
            material = new Material(simulationShader) { hideFlags = HideFlags.HideAndDontSave };
            texel = areaSize.x / resolution;
            int height = Mathf.Max(1, Mathf.RoundToInt(areaSize.y / texel));
            current = CreateField(resolution, height);
            next = CreateField(resolution, height);
            hasOrigin = false;
        }

        void OnDisable()
        {
            if (instance == this)
                instance = null;
            Shader.SetGlobalVector(RippleAreaId, Vector4.zero);
            Destroy(material);
            current.Release();
            next.Release();
        }

        static RenderTexture CreateField(int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.RGHalf)
            {
                name = "WaterRipples",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
            return rt;
        }

        void LateUpdate()
        {
            if (follow == null)
            {
                var rian = FindAnyObjectByType<RianController>();
                if (rian == null)
                    return;
                follow = rian.transform;
            }

            // Snap the window to whole texels so scrolling never resamples (and blurs) the field.
            var centre = new Vector2(follow.position.x, follow.position.z);
            var newOrigin = centre - areaSize * 0.5f;
            newOrigin = new Vector2(Mathf.Round(newOrigin.x / texel) * texel, Mathf.Round(newOrigin.y / texel) * texel);
            var shift = hasOrigin ? (newOrigin - origin) : Vector2.zero;
            origin = newOrigin;
            hasOrigin = true;

            var size = new Vector2(texel * current.width, texel * current.height);
            material.SetVector(AreaId, new Vector4(origin.x, origin.y, size.x, size.y));
            material.SetVector(ShiftId, new Vector2(shift.x / size.x, shift.y / size.y));
            material.SetVectorArray(DropsId, drops);
            material.SetFloat(DropCountId, dropCount);
            Blit(ShiftAndDropsPass);
            dropCount = 0;

            material.SetFloat(PropagationId, propagation);
            material.SetFloat(DampingId, damping);
            stepDebt += Time.deltaTime * stepsPerSecond;
            int steps = Mathf.Min(Mathf.FloorToInt(stepDebt), 4);
            stepDebt = Mathf.Min(stepDebt - steps, 1f);
            for (int i = 0; i < steps; i++)
                Blit(StepPass);

            Shader.SetGlobalTexture(RippleTexId, current);
            Shader.SetGlobalVector(RippleAreaId, new Vector4(origin.x, origin.y, 1f / size.x, 1f / size.y));
            Shader.SetGlobalVector(RippleTexelId, new Vector4(1f / current.width, 1f / current.height, texel, 0f));
        }

        void Blit(int pass)
        {
            Graphics.Blit(current, next, material, pass);
            (current, next) = (next, current);
        }
    }
}
