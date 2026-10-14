using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Generates tileable wet-sand textures (albedo, normal, mask) and a TerrainLayer that uses them, then
    /// adds the layer to every terrain in the open scenes. Run from Tools/ProjectX/Generate Wet Sand Terrain Layer;
    /// re-running overwrites the textures in place, so the layer and its GUIDs stay the same.
    /// </summary>
    static class WetSandGenerator
    {
        const string Folder = "Assets/_Project/Terrain/WetSand";
        const int Size = 1024;
        const float TileMeters = 4f;

        // Albedo is authored in sRGB.
        static readonly Color SandDark = new Color(0.30f, 0.24f, 0.17f);
        static readonly Color SandLight = new Color(0.46f, 0.38f, 0.27f);
        static readonly Color GrainDark = new Color(0.16f, 0.13f, 0.10f);
        static readonly Color GrainLight = new Color(0.62f, 0.55f, 0.44f);

        const float SmoothnessDry = 0.45f;  // ripple crests drain first
        const float SmoothnessWet = 0.80f;  // troughs hold a film of water
        const float NormalStrength = 6f;

        [MenuItem("Tools/ProjectX/Generate Wet Sand Terrain Layer")]
        static void Generate()
        {
            Directory.CreateDirectory(Folder);

            var height = new float[Size * Size];
            var wet = new float[Size * Size];
            var albedo = new Color[Size * Size];
            var mask = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size, v = (float)y / Size;

                // Wind ripples: bands along U, warped by low-frequency noise so they meander.
                float warp = Fbm(u, v, 3, 3, 11) - 0.5f;
                float phase = (v * 9f + warp * 1.6f) * Mathf.PI * 2f;
                float ripple = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(phase), 1.6f);
                float rippleFade = Mathf.SmoothStep(0.25f, 0.75f, Fbm(u, v, 2, 2, 23));
                ripple *= rippleFade;

                float lumps = Fbm(u, v, 6, 4, 37);
                float grain = Fbm(u, v, 128, 2, 51);

                float h = ripple * 0.55f + lumps * 0.35f + grain * 0.10f;
                height[y * Size + x] = h;

                // Puddled troughs are wetter (darker, glossier) than crests.
                float w = Mathf.Clamp01(1f - ripple * 0.8f - (lumps - 0.5f) * 0.9f);
                wet[y * Size + x] = w;

                Color c = Color.Lerp(SandLight, SandDark, Mathf.SmoothStep(0f, 1f, w));
                c = Color.Lerp(c, c * 0.85f, Fbm(u, v, 24, 3, 67));
                float speck = Hash(x, y, 79);
                if (speck > 0.985f) c = Color.Lerp(c, GrainDark, 0.6f);
                else if (speck < 0.01f) c = Color.Lerp(c, GrainLight, 0.5f);
                albedo[y * Size + x] = c;
            }

            var normal = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = H(height, x + 1, y) - H(height, x - 1, y);
                float dy = H(height, x, y + 1) - H(height, x, y - 1);
                var n = new Vector3(-dx * NormalStrength, -dy * NormalStrength, 1f).normalized;
                normal[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);

                int i = y * Size + x;
                float cavity = Mathf.Clamp01((H(height, x - 2, y) + H(height, x + 2, y) + H(height, x, y - 2)
                                              + H(height, x, y + 2)) * 0.25f - height[i] + 0.5f);
                float ao = Mathf.Lerp(1f, 0.75f, Mathf.Clamp01((cavity - 0.5f) * 40f));
                float smooth = Mathf.Lerp(SmoothnessDry, SmoothnessWet, wet[i]) - Hash(x, y, 97) * 0.08f;
                mask[i] = new Color(0f, ao, height[i], smooth);  // HDRP/URP mask map: metallic, AO, height, smoothness
            }

            var albedoTex = Save("WetSand_Albedo", albedo, srgb: true, isNormal: false);
            var normalTex = Save("WetSand_Normal", normal, srgb: false, isNormal: true);
            var maskTex = Save("WetSand_Mask", mask, srgb: false, isNormal: false);

            string layerPath = Folder + "/WetSand.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = albedoTex;
            layer.normalMapTexture = normalTex;
            layer.maskMapTexture = maskTex;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(TileMeters, TileMeters);
            layer.tileOffset = Vector2.zero;
            layer.metallic = 0f;
            layer.smoothness = 1f;
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = Vector4.one;
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssets();

            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                var data = terrain.terrainData;
                var layers = data.terrainLayers;
                if (System.Array.IndexOf(layers, layer) >= 0) continue;
                Undo.RecordObject(data, "Add Wet Sand Layer");
                ArrayUtility.Insert(ref layers, 0, layer);  // first layer fills the whole terrain
                data.terrainLayers = layers;
                EditorUtility.SetDirty(data);
                Debug.Log($"[WetSand] Added layer to terrain '{terrain.name}'.", terrain);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = layer;
            Debug.Log($"[WetSand] Generated {layerPath}.");
        }

        static float H(float[] h, int x, int y) => h[((y % Size + Size) % Size) * Size + (x % Size + Size) % Size];

        static Texture2D Save(string name, Color[] pixels, bool srgb, bool isNormal)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, !srgb);
            tex.SetPixels(pixels);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
    }
}
