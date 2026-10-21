using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectX
{
    /// <summary>
    /// Plays a random footstep clip on the OnFootstep/OnLand animation events baked into the character's clips.
    /// Sits next to the Animator that fires them.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] AudioClip[] footstepClips;
        [Tooltip("Animation clips whose steps play at the run volume; steps from any other clip play at the walk volume.")]
        [SerializeField] AnimationClip[] runClips;

        [Tooltip("Walking footstep volume, percent.")]
        [FormerlySerializedAs("volumePercent")]
        [Range(0f, 100f)] public float walkVolumePercent = 35f;
        [Tooltip("Running footstep volume, percent.")]
        [Range(0f, 100f)] public float runVolumePercent = 60f;
        [Tooltip("Landing volume, percent.")]
        [Range(0f, 100f)] public float landVolumePercent = 100f;
        [Tooltip("Random pitch spread around 1, percent.")]
        [Range(0f, 50f)] public float pitchVariationPercent = 8f;

        AudioSource source;
        int lastClip = -1;

        void Awake() => source = GetComponent<AudioSource>();

        // Blend trees fire the events of every clip they mix, so only the dominant clip counts.
        void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
                Play(IsRun(animationEvent.animatorClipInfo.clip) ? runVolumePercent : walkVolumePercent);
        }

        void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
                Play(landVolumePercent);
        }

        bool IsRun(AnimationClip clip) => runClips != null && System.Array.IndexOf(runClips, clip) >= 0;

        void Play(float percent)
        {
            if (footstepClips == null || footstepClips.Length == 0)
                return;

            // Never the same clip twice in a row.
            int index = Random.Range(0, footstepClips.Length);
            if (index == lastClip && footstepClips.Length > 1)
                index = (index + 1) % footstepClips.Length;
            lastClip = index;

            float spread = pitchVariationPercent * 0.01f;
            source.pitch = Random.Range(1f - spread, 1f + spread);
            source.PlayOneShot(footstepClips[index], percent * 0.01f);
        }
    }
}
