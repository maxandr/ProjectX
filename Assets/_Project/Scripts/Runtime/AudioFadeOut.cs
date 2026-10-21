using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Plays the <see cref="AudioSource"/>'s clip and fades it to silence over its last <see cref="fadeOutMs"/>,
    /// so a one-shot sound trails off instead of cutting. Call <see cref="Play"/> from an event (e.g. BatSwarm.onFired).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioFadeOut : MonoBehaviour
    {
        [Tooltip("Length of the fade at the end of the clip, milliseconds.")]
        public float fadeOutMs = 2000f;

        AudioSource source;
        float volume;
        Coroutine fade;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            volume = source.volume;
        }

        public void Play()
        {
            if (source.clip == null)
                return;
            if (fade != null)
                StopCoroutine(fade);

            source.volume = volume;
            source.Play();

            float length = source.clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            float duration = Mathf.Min(fadeOutMs * 0.001f, length);
            // Squared falloff sounds even to the ear; a linear one seems to hang and then cut.
            fade = Tween.Run(this, duration, t => source.volume = volume * (1f - t) * (1f - t),
                onComplete: source.Stop, delay: length - duration);
        }
    }
}
