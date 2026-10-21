using System.Collections;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Plays a short creak each time a <see cref="ToppleBridge"/> is nudged, as a few one-shots in a row at falling
    /// volumes so one short crack reads as a creaking sway. Uses the <see cref="AudioSource"/>'s 3D settings.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class NudgeAudio : MonoBehaviour
    {
        [SerializeField] ToppleBridge bridge;
        [SerializeField] AudioClip clip;

        [Tooltip("Volume of each play in turn, percent.")]
        public float[] volumesPercent = { 100f, 55f };
        [Tooltip("Time from the start of one play to the next, milliseconds.")]
        public float intervalMs = 380f;

        AudioSource source;
        Coroutine routine;

        void Awake() => source = GetComponent<AudioSource>();

        void OnEnable() => bridge.Nudged += OnNudged;

        void OnDisable() => bridge.Nudged -= OnNudged;

        void OnNudged()
        {
            if (clip == null)
                return;
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            var interval = new WaitForSeconds(intervalMs * 0.001f);
            for (int i = 0; i < volumesPercent.Length; i++)
            {
                if (i > 0)
                    yield return interval;
                source.PlayOneShot(clip, volumesPercent[i] * 0.01f);
            }
            routine = null;
        }
    }
}
