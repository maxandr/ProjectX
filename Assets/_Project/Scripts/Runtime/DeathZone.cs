using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A chasm or other fall hazard: a <see cref="FoxBarrier"/> until a bridge opens it.
    /// Rian entering the trigger volume respawns at the last checkpoint.
    /// </summary>
    public class DeathZone : FoxBarrier
    {
        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RianController rian))
                Checkpoint.Respawn(rian);
        }
    }
}
