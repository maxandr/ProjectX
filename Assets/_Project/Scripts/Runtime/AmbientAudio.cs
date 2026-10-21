using System;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Sets a looping ambience's volume from keys placed along X, driven by Rian's position, so the sea and wind
    /// fade out and the cave fades in as she walks through the cave mouth.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientAudio : MonoBehaviour
    {
        [Serializable]
        public struct Key
        {
            public float x;
            [Tooltip("Share of the AudioSource's own volume, percent.")]
            public float volumePercent;
        }

        [SerializeField] Transform target;
        [Tooltip("Sorted by x, ascending.")]
        public Key[] keys;

        AudioSource source;
        float volume;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            volume = source.volume;
        }

        void LateUpdate()
        {
            if (keys == null || keys.Length == 0)
                return;
            source.volume = volume * Evaluate(target.position.x) * 0.01f;
        }

        float Evaluate(float x)
        {
            if (x <= keys[0].x)
                return keys[0].volumePercent;
            for (int i = 1; i < keys.Length; i++)
            {
                if (x > keys[i].x)
                    continue;
                float t = Ease.InOutSine(Mathf.InverseLerp(keys[i - 1].x, keys[i].x, x));
                return Mathf.Lerp(keys[i - 1].volumePercent, keys[i].volumePercent, t);
            }
            return keys[keys.Length - 1].volumePercent;
        }
    }
}
