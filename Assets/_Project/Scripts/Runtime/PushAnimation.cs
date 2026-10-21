using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Plays Rian's push animation (the Animator's "Pushing" bool) while something tells it she is pushing,
    /// e.g. a <see cref="PushSensor"/>'s onPushingChanged.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PushAnimation : MonoBehaviour
    {
        static readonly int PushingId = Animator.StringToHash("Pushing");

        Animator animator;

        void Awake() => animator = GetComponent<Animator>();

        public void SetPushing(bool pushing) => animator.SetBool(PushingId, pushing);
    }
}
