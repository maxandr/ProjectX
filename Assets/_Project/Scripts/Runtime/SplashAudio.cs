using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Plays a random splash clip when the <see cref="WaterRippleEmitter"/> reports entering the water,
    /// louder the faster the fall. Wading in from the bank (a slow entry) stays silent.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SplashAudio : MonoBehaviour
    {
        [SerializeField] WaterRippleEmitter emitter;
        [SerializeField] AudioClip[] splashClips;

        [Tooltip("Slowest fall (m/s) that makes a splash sound.")]
        public float minFallSpeed = 1f;
        [Tooltip("Fall speed (m/s) at which the splash reaches full volume.")]
        public float fullFallSpeed = 8f;
        [Tooltip("Volume at the slowest fall, percent.")]
        [Range(0f, 100f)] public float minVolumePercent = 40f;
        [Tooltip("Volume at full fall speed, percent.")]
        [Range(0f, 100f)] public float volumePercent = 100f;
        [Tooltip("Random pitch spread around 1, percent.")]
        [Range(0f, 50f)] public float pitchVariationPercent = 6f;

        AudioSource source;
        int lastClip = -1;

        void Awake() => source = GetComponent<AudioSource>();

        void OnEnable() => emitter.Splashed += OnSplashed;
        void OnDisable() => emitter.Splashed -= OnSplashed;

        void OnSplashed(float fallSpeed)
        {
            if (fallSpeed < minFallSpeed || splashClips == null || splashClips.Length == 0)
                return;

            // Never the same clip twice in a row.
            int index = Random.Range(0, splashClips.Length);
            if (index == lastClip && splashClips.Length > 1)
                index = (index + 1) % splashClips.Length;
            lastClip = index;

            float t = Mathf.InverseLerp(minFallSpeed, fullFallSpeed, fallSpeed);
            float spread = pitchVariationPercent * 0.01f;
            source.pitch = Random.Range(1f - spread, 1f + spread);
            source.PlayOneShot(splashClips[index], Mathf.Lerp(minVolumePercent, volumePercent, t) * 0.01f);
        }
    }
}
