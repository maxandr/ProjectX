using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Plays the <see cref="AudioSource"/>'s clip from <see cref="startMs"/> in, so a clip whose hit comes late
    /// lines up with the moment it belongs to. Call <see cref="Play"/> from an event (e.g. ToppleBridge.onToppleStart).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class OffsetAudio : MonoBehaviour
    {
        [Tooltip("Where in the clip playback starts, milliseconds.")]
        public float startMs;

        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
        }

        public void Play()
        {
            if (source.clip == null)
                return;
            source.Play();
            source.time = Mathf.Min(startMs * 0.001f, source.clip.length * 0.99f);
        }
    }
}
