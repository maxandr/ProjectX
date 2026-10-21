using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Plays the <see cref="AudioSource"/>'s clip while Rian charges a <see cref="SacrificeNode"/>: it starts with the
    /// hold, runs on to its end once the sacrifice fires, and fades out if she lets go early.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SacrificeAudio : MonoBehaviour
    {
        [SerializeField] SacrificeNode node;

        [Tooltip("Volume, percent.")]
        [Range(0f, 100f)] public float volumePercent = 100f;
        [Tooltip("Fade when the hold is cancelled, milliseconds.")]
        public float fadeOutMs = 300f;

        AudioSource source;
        Coroutine fade;

        void Awake() => source = GetComponent<AudioSource>();

        void OnEnable()
        {
            node.HoldStarted += Play;
            node.HoldCancelled += FadeOut;
        }

        void OnDisable()
        {
            node.HoldStarted -= Play;
            node.HoldCancelled -= FadeOut;
        }

        void Play()
        {
            StopFade();
            source.volume = volumePercent * 0.01f;
            source.Play();
        }

        void FadeOut()
        {
            if (!source.isPlaying || !isActiveAndEnabled)
                return;
            StopFade();
            float from = source.volume;
            fade = Tween.Run(this, fadeOutMs * 0.001f, t => source.volume = from * (1f - t) * (1f - t),
                onComplete: source.Stop);
        }

        void StopFade()
        {
            if (fade != null)
                StopCoroutine(fade);
            fade = null;
        }
    }
}
