using UnityEngine;
using UnityEngine.Events;

namespace ProjectX
{
    /// <summary>
    /// Raises <see cref="onEntered"/> the first time Rian enters the trigger volume, e.g. to play a one-off sound.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RianTrigger : MonoBehaviour
    {
        public UnityEvent onEntered;

        bool fired;

        void OnTriggerEnter(Collider other)
        {
            if (fired || !other.TryGetComponent(out RianInput _))
                return;
            fired = true;
            onEntered.Invoke();
        }
    }
}
