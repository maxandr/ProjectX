using System;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Blends ambient light, sun and fog between keys placed along X, driven by Rian's position
    /// (golden-hour flats → dark cavern → deeper darkness).
    /// </summary>
    public class ZoneLighting : MonoBehaviour
    {
        [Serializable]
        public struct Key
        {
            public float x;
            public Color ambient;
            public Color sunColor;
            public float sunIntensity;
            public Color fogColor;
            public float fogDensity;
        }

        [SerializeField] Transform target;
        [SerializeField] Light sun;
        [Tooltip("Sorted by x, ascending.")]
        public Key[] keys;

        void Awake()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
        }

        void LateUpdate()
        {
            if (keys == null || keys.Length == 0)
                return;
            Apply(Evaluate(target.position.x));
        }

        Key Evaluate(float x)
        {
            if (x <= keys[0].x)
                return keys[0];
            for (int i = 1; i < keys.Length; i++)
            {
                if (x > keys[i].x)
                    continue;
                float t = Ease.InOutSine(Mathf.InverseLerp(keys[i - 1].x, keys[i].x, x));
                return Lerp(keys[i - 1], keys[i], t);
            }
            return keys[keys.Length - 1];
        }

        static Key Lerp(Key a, Key b, float t) => new Key
        {
            x = Mathf.Lerp(a.x, b.x, t),
            ambient = Color.Lerp(a.ambient, b.ambient, t),
            sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
            sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
        };

        void Apply(Key key)
        {
            RenderSettings.ambientLight = key.ambient;
            RenderSettings.fogColor = key.fogColor;
            RenderSettings.fogDensity = key.fogDensity;
            sun.color = key.sunColor;
            sun.intensity = key.sunIntensity;
        }
    }
}
