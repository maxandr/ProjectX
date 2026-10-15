using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A flooded trench: a <see cref="FoxBarrier"/> until a bridge opens it.
    /// Rian falling into the trigger volume respawns at the last checkpoint.
    /// </summary>
    public class DeepWaterZone : FoxBarrier
    {
        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RianController rian))
                Checkpoint.Respawn(rian);
        }
    }
}
