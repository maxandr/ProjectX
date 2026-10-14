using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectX
{
    /// <summary>
    /// Builds a soft top-down mask of everything poking out of the water (rocks, reeds, banks, pillars) around Rian,
    /// and publishes it to the <c>ProjectX/Water</c> shader, which draws glowing shore rings from it.
    /// Characters with a <see cref="WaterRippleEmitter"/> are skipped; they make ripples instead.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class WaterContactFoam : MonoBehaviour
    {
        const int FootprintPass = 0;
        const int BlurPass = 1;

        static readonly int ContactAreaId = Shader.PropertyToID("_ContactArea");
        static readonly int ContactSurfaceYId = Shader.PropertyToID("_ContactSurfaceY");
        static readonly int ContactSlabId = Shader.PropertyToID("_ContactSlab");
        static readonly int ContactShearId = Shader.PropertyToID("_ContactShear");
        static readonly Vector4[] Shears = { new Vector4(1f, 1f), new Vector4(1f, -1f) };
        static readonly int BlurStepId = Shader.PropertyToID("_BlurStep");
        static readonly int FoamTexId = Shader.PropertyToID("_WaterContactTex");
        static readonly int FoamAreaId = Shader.PropertyToID("_WaterContactArea");

        [SerializeField] Shader maskShader;
        [Tooltip("The mask is centred on this; defaults to Rian.")]
        [SerializeField] Transform follow;
        [Tooltip("World size (X, Z) of the masked window.")]
        public Vector2 areaSize = new Vector2(36f, 30f);
        [Tooltip("Shifts the window into the scene (+Z) so background rocks are covered too.")]
        public float depthOffset = 6f;
        [Tooltip("Texels along X; Z follows the area's aspect.")]
        public int resolution = 256;
        [Tooltip("How far (metres) the glow reaches out from an object.")]
        [Range(0.2f, 3f)] public float spread = 0.9f;
        [Tooltip("Geometry this far below the surface still counts as touching it.")]
        public float sliceBelow = 0.06f;
        [Tooltip("Geometry this far above the surface still counts as touching it.")]
        public float sliceAbove = 0.1f;
        [Tooltip("Seconds between rescans of the scene for renderers.")]
        public float rescanInterval = 2f;

        readonly List<Renderer> candidates = new List<Renderer>();
        Material material;
        RenderTexture mask, blurred;
        CommandBuffer commands;
        float texel;
        float nextScan;

        void OnEnable()
        {
            material = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
            texel = areaSize.x / resolution;
            int height = Mathf.Max(1, Mathf.RoundToInt(areaSize.y / texel));
            mask = CreateMask(resolution, height);
            blurred = CreateMask(resolution, height);
            commands = new CommandBuffer { name = "Water Contact Mask" };
            nextScan = 0f;
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(FoamAreaId, Vector4.zero);
            Destroy(material);
            mask.Release();
            blurred.Release();
            commands.Release();
        }

        static RenderTexture CreateMask(int width, int height)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf)
            {
                name = "WaterContactMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        void Rescan()
        {
            candidates.Clear();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer))
                    continue;
                if (r.GetComponent<WaterSurface>() != null || r.GetComponentInParent<WaterRippleEmitter>() != null)
                    continue;
                bool opaque = true;
                foreach (var m in r.sharedMaterials)
                    opaque &= m != null && m.renderQueue < (int)RenderQueue.GeometryLast;
                if (opaque)
                    candidates.Add(r);
            }
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
            if (Time.unscaledTime >= nextScan)
            {
                Rescan();
                nextScan = Time.unscaledTime + rescanInterval;
            }

            // Snap to whole texels so the soft mask doesn't shimmer as the window slides.
            var centre = new Vector2(follow.position.x, follow.position.z + depthOffset);
            var origin = centre - areaSize * 0.5f;
            origin = new Vector2(Mathf.Round(origin.x / texel) * texel, Mathf.Round(origin.y / texel) * texel);
            var size = new Vector2(texel * mask.width, texel * mask.height);
            var area = new Vector4(origin.x, origin.y, 1f / size.x, 1f / size.y);

            commands.Clear();
            commands.SetRenderTarget(mask);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetGlobalVector(ContactAreaId, area);
            commands.SetGlobalVector(ContactSlabId, new Vector4(sliceBelow, sliceAbove));
            foreach (var r in candidates)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                var b = r.bounds;
                if (b.max.x < origin.x || b.min.x > origin.x + size.x || b.max.z < origin.y || b.min.z > origin.y + size.y)
                    continue;
                var water = WaterSurface.At(b.center) ?? WaterSurface.At(new Vector3(b.min.x, b.center.y, b.center.z))
                    ?? WaterSurface.At(new Vector3(b.max.x, b.center.y, b.center.z));
                if (water == null || b.min.y >= water.SurfaceY + sliceAbove || b.max.y <= water.SurfaceY - sliceBelow)
                    continue;
                commands.SetGlobalFloat(ContactSurfaceYId, water.SurfaceY);
                foreach (var shear in Shears)
                {
                    commands.SetGlobalVector(ContactShearId, shear);
                    for (int sub = 0; sub < r.sharedMaterials.Length; sub++)
                        commands.DrawRenderer(r, material, sub, FootprintPass);
                }
            }
            Graphics.ExecuteCommandBuffer(commands);

            // Six taps either side, so each tap steps a sixth of the spread.
            float step = spread / 6f;
            material.SetVector(BlurStepId, new Vector2(step / size.x, 0f));
            Graphics.Blit(mask, blurred, material, BlurPass);
            material.SetVector(BlurStepId, new Vector2(0f, step / size.y));
            Graphics.Blit(blurred, mask, material, BlurPass);

            Shader.SetGlobalTexture(FoamTexId, mask);
            Shader.SetGlobalVector(FoamAreaId, area);
        }
    }
}
