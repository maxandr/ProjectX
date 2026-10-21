using System.Collections;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Plays the <see cref="AudioSource"/>'s grinding-stone clip while a <see cref="BasaltPillar"/> moves and fades it
    /// out once the pillar stops. Each puzzle tunes its own pitch, e.g. higher for a small stone, lower for a big one.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PillarMoveAudio : MonoBehaviour
    {
        [SerializeField] BasaltPillar pillar;

        [Tooltip("Playback pitch, percent (100 = original, lower = heavier stone).")]
        [Range(10f, 300f)] public float pitchPercent = 100f;
        [Tooltip("Volume, percent.")]
        [Range(0f, 100f)] public float volumePercent = 100f;
        [Tooltip("Fade after the pillar stops, milliseconds.")]
        public float fadeOutMs = 600f;

        AudioSource source;
        Coroutine routine;

        void Awake() => source = GetComponent<AudioSource>();

        void OnEnable()
        {
            pillar.MoveStarted += OnMoveStarted;
            pillar.Snapped += Stop;
        }

        void OnDisable()
        {
            pillar.MoveStarted -= OnMoveStarted;
            pillar.Snapped -= Stop;
            Stop();
        }

        void OnMoveStarted(float delay, float duration)
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(Play(delay, duration));
        }

        IEnumerator Play(float delay, float duration)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float volume = volumePercent * 0.01f;
            source.pitch = pitchPercent * 0.01f;
            source.volume = volume;
            source.Play();
            yield return new WaitForSeconds(duration);

            float fade = fadeOutMs * 0.001f;
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                float k = 1f - t / fade;
                source.volume = volume * k * k;
                yield return null;
            }
            Stop();
        }

        void Stop()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            source.Stop();
        }
    }
}
