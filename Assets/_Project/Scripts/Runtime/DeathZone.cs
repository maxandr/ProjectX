using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A chasm or other fall hazard: Rian entering the trigger volume respawns at the last checkpoint.
    /// Blocking the Fox is a separate <see cref="FoxBarrier"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DeathZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RianController rian))
                Checkpoint.Respawn(rian);
        }
    }
}
