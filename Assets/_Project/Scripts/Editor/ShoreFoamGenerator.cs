using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectX
{
    /// <summary>
    /// Builds white surf around rocks that stand in water: a mesh that hugs each selected renderer's waterline,
    /// with a dense surf band against the stone and lacy foam fading out into the water. Select the rocks and run
    /// Tools/ProjectX/Build Shore Foam. Each rock gets a child of `Shore Foam` (next to its water) and a mesh asset in
    /// Meshes/ShoreFoam; re-running after moving a rock rebuilds both in place, keeping the GUIDs.
    /// </summary>
    static class ShoreFoamGenerator
    {
        const string MeshFolder = "Assets/_Project/Meshes/ShoreFoam";
        const string TextureFolder = "Assets/_Project/Textures/Level";
        const string MaterialFolder = "Assets/_Project/Materials/Detail";
        const string GroupName = "Shore Foam";
        const int TextureSize = 512;

        const float Cell = 0.3f;          // grid spacing of the foam mesh
        const float SurfWidth = 1.8f;     // dense band against the stone
        const float LaceWidth = 4f;       // lacy foam drifting out
        const float SurfLift = 0.03f;     // above the water, so it never z-fights it
        const float LaceLift = 0.015f;
        const float SurfTile = 3f;        // metres per texture repeat
        const float LaceTile = 6f;
        const float WashTile = 1.5f;
        const float WashMin = 0.15f;      // white water climbing the rock, height range above the water
        const float WashMax = 0.6f;
        const float WashSink = 0.1f;      // starts below the water, so its foot never shows a seam
        const float WashOut = 0.04f;      // off the stone, so it never z-fights it

        [MenuItem("Tools/ProjectX/Build Shore Foam")]
        static void Build()
        {
            var surfTexture = Texture("T_ShoreSurf", SurfPixel);
            var materials = new[] { Material("ShoreSurf", surfTexture, SurfTile),
                                    Material("ShoreLace", Texture("T_ShoreLace", LacePixel), LaceTile),
                                    Material("ShoreWash", surfTexture, WashTile, CullMode.Off) };
            Directory.CreateDirectory(MeshFolder);

            var waters = Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Exclude);
            int built = 0;
            foreach (var go in Selection.gameObjects)
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || r.GetComponent<WaterSurface>() != null)
                    continue;
                var water = WaterUnder(r.bounds, waters);
                if (water == null)
                    continue;
                float y = water.GetComponent<Renderer>().bounds.max.y;
                var outward = new List<Vector2>();
                var segments = Waterline(mf, y, outward);
                if (segments.Count == 0)
                    continue;

                var mesh = SaveMesh(r.name, FoamMesh(segments, outward, y));
                Place(r, water.transform.parent, mesh, materials);
                built++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShoreFoam] Built foam around {built} rock(s).");
        }

        static WaterSurface WaterUnder(Bounds rock, WaterSurface[] waters)
        {
            WaterSurface best = null;
            float bestY = float.MinValue;
            foreach (var water in waters)
            {
                var b = water.GetComponent<Renderer>().bounds;
                float y = b.max.y;
                if (y < rock.min.y || y > rock.max.y || y <= bestY)
                    continue;
                if (rock.max.x < b.min.x || rock.min.x > b.max.x || rock.max.z < b.min.z || rock.min.z > b.max.z)
                    continue;
                best = water;
                bestY = y;
            }
            return best;
        }

        /// <summary>
        /// Where the mesh's triangles cut the plane at <paramref name="y"/>, as world XZ segments; <paramref name="outward"/>
        /// gets each segment's face normal in XZ.
        /// </summary>
        static List<Vector4> Waterline(MeshFilter mf, float y, List<Vector2> outward)
        {
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var world = new Vector3[verts.Length];
            var m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++)
                world[i] = m.MultiplyPoint3x4(verts[i]);

            var segments = new List<Vector4>();
            var cut = new List<Vector2>(3);
            var tris = mesh.triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                cut.Clear();
                for (int e = 0; e < 3; e++)
                {
                    var a = world[tris[t + e]];
                    var b = world[tris[t + (e + 1) % 3]];
                    if ((a.y - y) * (b.y - y) >= 0f)
                        continue;
                    var p = Vector3.Lerp(a, b, (y - a.y) / (b.y - a.y));
                    cut.Add(new Vector2(p.x, p.z));
                }
                if (cut.Count != 2)
                    continue;
                var n = Vector3.Cross(world[tris[t + 1]] - world[tris[t]], world[tris[t + 2]] - world[tris[t]]);
                var normal = new Vector2(n.x, n.z).normalized;
                // Short pieces, so SegmentGrid can bin each one by its midpoint.
                int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(cut[0], cut[1]) / Cell));
                for (int k = 0; k < pieces; k++)
                {
                    var a = Vector2.Lerp(cut[0], cut[1], (float)k / pieces);
                    var b = Vector2.Lerp(cut[0], cut[1], (float)(k + 1) / pieces);
                    segments.Add(new Vector4(a.x, a.y, b.x, b.y));
                    outward.Add(normal);
                }
            }
            return segments;
        }

        /// <summary>
        /// Two grids over the waterline (submesh 0 the surf band, submesh 1 the lace around it) and submesh 2, white water
        /// climbing the stone, which is what shows from the low game camera that sees the flat layers edge-on.
        /// </summary>
        static Mesh FoamMesh(List<Vector4> segments, List<Vector2> outward, float y)
        {
            var index = new SegmentGrid(segments, LaceWidth * 1.5f);
            Vector2 min = index.Min - Vector2.one * LaceWidth * 1.5f, max = index.Max + Vector2.one * LaceWidth * 1.5f;
            int nx = Mathf.CeilToInt((max.x - min.x) / Cell) + 1, nz = Mathf.CeilToInt((max.y - min.y) / Cell) + 1;

            var distance = new float[nx * nz];
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
                distance[j * nx + i] = index.Distance(min + new Vector2(i, j) * Cell);

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var surf = Layer(distance, nx, nz, min, y + SurfLift, vertices, colors, uvs,
                p => SurfWidth * (0.55f + 0.9f * Mathf.PerlinNoise(p.x * 0.45f + 13.7f, p.y * 0.45f)), 0f);
            var lace = Layer(distance, nx, nz, min, y + LaceLift, vertices, colors, uvs,
                p => LaceWidth * (0.35f + 0.8f * Mathf.PerlinNoise(p.x * 0.18f, p.y * 0.18f + 41.3f)), 0.35f);
            var normals = new List<Vector3>();
            for (int i = 0; i < vertices.Count; i++)
                normals.Add(Vector3.up);
            var wash = Wash(segments, outward, y, vertices, colors, uvs, normals);

            var mesh = new Mesh { indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(surf, 0);
            mesh.SetTriangles(lace, 1);
            mesh.SetTriangles(wash, 2);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A strip standing on each waterline segment, just off the stone: opaque at the water, fading out at a height
        /// that wanders along the shore. U runs along the shore and V up it, both in metres.
        /// </summary>
        static List<int> Wash(List<Vector4> segments, List<Vector2> outward, float y,
            List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<Vector3> normals)
        {
            var tris = new List<int>();
            for (int s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                var off = outward[s] * WashOut;
                var normal = new Vector3(outward[s].x, 0f, outward[s].y);
                int first = vertices.Count;
                foreach (var p in new[] { new Vector2(seg.x, seg.y), new Vector2(seg.z, seg.w) })
                {
                    float top = Mathf.Lerp(WashMin, WashMax, Mathf.PerlinNoise(p.x * 2.2f + 7.1f, p.y * 2.2f));
                    // The foot, a band that stays nearly solid, then the fade to nothing at the top.
                    foreach (var (h, a) in new[] { (-WashSink, 1f), (top * 0.4f, 0.85f), (top, 0f) })
                    {
                        vertices.Add(new Vector3(p.x + off.x, y + h, p.y + off.y));
                        colors.Add(new Color(1f, 1f, 1f, a));
                        uvs.Add(new Vector2(p.x + p.y, h));
                        normals.Add(normal);
                    }
                }
                for (int row = 0; row < 2; row++)
                {
                    int a = first + row, b = a + 1, c = first + 3 + row, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(c); tris.Add(b); tris.Add(d);
                }
            }
            return tris;
        }

        /// <summary>
        /// Adds one grid layer and returns its triangles. Opacity is full up to <paramref name="solid"/> of the
        /// reach, then fades out at the reach given by <paramref name="width"/>, which wanders along the shore.
        /// </summary>
        static List<int> Layer(float[] distance, int nx, int nz, Vector2 min, float y,
            List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, System.Func<Vector2, float> width, float solid)
        {
            int first = vertices.Count;
            var alpha = new float[nx * nz];
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                var p = min + new Vector2(i, j) * Cell;
                float reach = width(p);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reach * solid, reach, distance[j * nx + i]));
                alpha[j * nx + i] = a;
                vertices.Add(new Vector3(p.x, y, p.y));
                colors.Add(new Color(1f, 1f, 1f, a));
                uvs.Add(p);  // metres; the material tiling sets the repeat, so ShoreFoamMotion can scroll in metres
            }

            var tris = new List<int>();
            for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                if (alpha[a] + alpha[b] + alpha[c] + alpha[d] < 0.01f)
                    continue;
                tris.Add(first + a); tris.Add(first + c); tris.Add(first + b);
                tris.Add(first + b); tris.Add(first + c); tris.Add(first + d);
            }
            return tris;
        }

        static Mesh SaveMesh(string rockName, Mesh mesh)
        {
            mesh.name = rockName + " Foam";
            string path = $"{MeshFolder}/{rockName} Foam.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>The foam object sits at the world origin, since its vertices are in world space.</summary>
        static void Place(Renderer rock, Transform zone, Mesh mesh, Material[] materials)
        {
            var group = zone != null ? zone.Find(GroupName) : null;
            if (group == null)
            {
                group = new GameObject(GroupName).transform;
                Undo.RegisterCreatedObjectUndo(group.gameObject, "Build Shore Foam");
                group.SetParent(zone, false);
                group.position = Vector3.zero;
                group.rotation = Quaternion.identity;
                group.localScale = Vector3.one;
            }

            string name = rock.name + " Foam";
            var foam = group.Find(name);
            if (foam == null)
            {
                foam = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                Undo.RegisterCreatedObjectUndo(foam.gameObject, "Build Shore Foam");
                foam.SetParent(group, true);
            }
            Undo.RecordObject(foam, "Build Shore Foam");
            foam.position = Vector3.zero;
            foam.rotation = Quaternion.identity;
            foam.localScale = new Vector3(1f / group.lossyScale.x, 1f / group.lossyScale.y, 1f / group.lossyScale.z);

            foam.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = foam.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(foam.gameObject, StaticEditorFlags.BatchingStatic);
            EditorUtility.SetDirty(foam.gameObject);
        }

        // Unlit, so the warm sun doesn't turn the surf orange; a touch below white so it doesn't glow against the dusk.
        static readonly Color FoamColor = new Color(0.82f, 0.85f, 0.88f, 1f);

        /// <summary>A transparent URP Particles Unlit material (it multiplies by vertex colour, which carries the fade).</summary>
        static Material Material(string name, Texture2D texture, float tileMeters, CullMode cull = CullMode.Back)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", Vector2.one / tileMeters);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetColor("_BaseColor", FoamColor);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)cull);
            mat.SetFloat("_ReceiveShadows", 1f);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_QueueOffset", 10f);  // after the water, which is plain Transparent; URP rebuilds the queue from this
            mat.renderQueue = (int)RenderQueue.Transparent + 10;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D Texture(string name, System.Func<float, float, float> alpha)
        {
            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha((float)x / TextureSize, (float)y / TextureSize));

            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            string path = $"{TextureFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Churned white water: big soft blotches with a web of lace running through them.
        static float SurfPixel(float u, float v)
        {
            float blotch = Smooth(0.35f, 0.65f, Fbm(u, v, 4, 5, 7));
            return Mathf.Clamp01(0.3f + 0.6f * blotch + 0.5f * Lace(u, v, 10, 3));
        }

        // GLSL smoothstep; Mathf.SmoothStep eases between its first two arguments instead.
        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // Breaking foam: Voronoi cell edges, broken up so it reads as streaks and bubbles rather than a net.
        static float LacePixel(float u, float v)
        {
            float breakup = Smooth(0.4f, 0.65f, Fbm(u, v, 3, 4, 19));
            float fine = Lace(u, v, 22, 5) * 0.6f;
            return Mathf.Clamp01((Lace(u, v, 9, 11) + fine) * breakup);
        }

        /// <summary>Tileable Voronoi edges (F2 − F1), 1 on the edge.</summary>
        static float Lace(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = float.MaxValue, f2 = float.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy;
                int wx = Wrap(gx, period), wy = Wrap(gy, period);
                float px = gx + Hash(wx, wy, seed), py = gy + Hash(wx, wy, seed + 1);
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
            return 1f - Smooth(0.02f, 0.2f, Mathf.Sqrt(f2) - Mathf.Sqrt(f1));
        }

        // Tileable fractal value noise on [0,1)^2; `period` is the lattice count of the first octave.
        static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * period, v * period, period, seed + o * 131);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period);
            int ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
            float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static int Wrap(int i, int period) => (i % period + period) % period;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Buckets the waterline segments so each grid point only measures the ones nearby.</summary>
        class SegmentGrid
        {
            readonly List<Vector4> segments;
            readonly Dictionary<Vector2Int, List<int>> bins = new Dictionary<Vector2Int, List<int>>();
            readonly float size;

            public Vector2 Min { get; } = Vector2.one * float.MaxValue;
            public Vector2 Max { get; } = Vector2.one * float.MinValue;

            public SegmentGrid(List<Vector4> segments, float size)
            {
                this.segments = segments;
                this.size = size;
                Vector2 min = Min, max = Max;
                for (int s = 0; s < segments.Count; s++)
                {
                    var seg = segments[s];
                    min = Vector2.Min(min, Vector2.Min(new Vector2(seg.x, seg.y), new Vector2(seg.z, seg.w)));
                    max = Vector2.Max(max, Vector2.Max(new Vector2(seg.x, seg.y), new Vector2(seg.z, seg.w)));
                    var key = Key(new Vector2((seg.x + seg.z) * 0.5f, (seg.y + seg.w) * 0.5f));
                    if (!bins.TryGetValue(key, out var list))
                        bins[key] = list = new List<int>();
                    list.Add(s);
                }
                Min = min;
                Max = max;
            }

            Vector2Int Key(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size));

            /// <summary>Distance to the nearest segment, or <see cref="float.MaxValue"/> beyond one bin away.</summary>
            public float Distance(Vector2 p)
            {
                var k = Key(p);
                float best = float.MaxValue;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!bins.TryGetValue(new Vector2Int(k.x + dx, k.y + dy), out var list))
                        continue;
                    foreach (int s in list)
                    {
                        var seg = segments[s];
                        Vector2 a = new Vector2(seg.x, seg.y), ab = new Vector2(seg.z, seg.w) - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                        best = Mathf.Min(best, (a + ab * t - p).sqrMagnitude);
                    }
                }
                return Mathf.Sqrt(best);
            }
        }
    }
}
