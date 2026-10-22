using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Shows a <see cref="ButtonPrompt"/> while Rian is inside the trigger. Wire <see cref="Complete"/> to the event
    /// that means the action is done (a push finished, the sacrifice fired) to hide it, and <see cref="Reopen"/> to
    /// one that undoes it (the Fox called off the plate) to let it show again.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PromptZone : MonoBehaviour
    {
        [SerializeField] ButtonPrompt prompt;

        Collider rianCollider;
        bool done;

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out RianInput _))
                return;
            rianCollider = other;
            Refresh();
        }

        void OnTriggerExit(Collider other)
        {
            if (other != rianCollider)
                return;
            rianCollider = null;
            Refresh();
        }

        public void Complete()
        {
            done = true;
            Refresh();
        }

        public void Reopen()
        {
            done = false;
            Refresh();
        }

        void Refresh()
        {
            if (rianCollider != null && !done)
                prompt.Show();
            else
                prompt.Hide();
        }
    }
}
