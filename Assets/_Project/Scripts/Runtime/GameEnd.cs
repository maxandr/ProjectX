using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// The end of the level: Rian entering the trigger volume loses control and comes to a stop, the screen slowly
    /// fades out (<see cref="ScreenFade.FadeOut"/>) and the game restarts from the beginning (<see cref="GameReset.Restart"/>).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GameEnd : MonoBehaviour
    {
        [Tooltip("Seconds the screen takes to fade out before the game restarts.")]
        [Min(0f)] public float fadeTime = 2f;

        bool ending;

        void OnTriggerEnter(Collider other)
        {
            if (ending || !other.TryGetComponent(out RianInput input))
                return;

            ending = true;
            input.enabled = false; // no input: she decelerates to an idle stop (and lands if mid-jump)
            ScreenFade.FadeOut(fadeTime, GameReset.Restart);
        }
    }
}
