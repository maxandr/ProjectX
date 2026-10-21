using System;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Gives every sound the cave's echo once Rian is inside: keeps the AudioReverbZone on the listener and blends
    /// its room level from keys placed along X, driven by Rian's position, so the echo rises across the cave mouth.
    /// The zone's own settings are the full-strength echo.
    /// </summary>
    [RequireComponent(typeof(AudioReverbZone))]
    public class CaveReverb : MonoBehaviour
    {
        [Serializable]
        public struct Key
        {
            public float x;
            [Tooltip("Share of the zone's room level, percent.")]
            public float wetPercent;
        }

        const int SilentRoom = -10000;

        [SerializeField] Transform target;
        [SerializeField] Transform listener;
        [Tooltip("Sorted by x, ascending.")]
        public Key[] keys;

        AudioReverbZone zone;
        int room;

        void Awake()
        {
            zone = GetComponent<AudioReverbZone>();
            room = zone.room;
        }

        void LateUpdate()
        {
            transform.position = listener.position;
            if (keys == null || keys.Length == 0)
                return;
            float wet = Evaluate(target.position.x) * 0.01f;
            zone.room = wet > 0.0001f ? Mathf.Max(SilentRoom, room + Mathf.RoundToInt(2000f * Mathf.Log10(wet))) : SilentRoom;
        }

        float Evaluate(float x)
        {
            if (x <= keys[0].x)
                return keys[0].wetPercent;
            for (int i = 1; i < keys.Length; i++)
            {
                if (x > keys[i].x)
                    continue;
                float t = Ease.InOutSine(Mathf.InverseLerp(keys[i - 1].x, keys[i].x, x));
                return Mathf.Lerp(keys[i - 1].wetPercent, keys[i].wetPercent, t);
            }
            return keys[keys.Length - 1].wetPercent;
        }
    }
}
