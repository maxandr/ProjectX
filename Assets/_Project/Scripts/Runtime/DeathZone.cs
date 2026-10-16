using System.Collections;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A chasm or other fall hazard: Rian entering the trigger volume respawns at the last checkpoint,
    /// after <see cref="respawnDelay"/> so the fall (e.g. a splash) can be seen.
    /// Blocking the Fox is a separate <see cref="FoxBarrier"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DeathZone : MonoBehaviour
    {
        [Tooltip("Seconds between Rian entering the zone and the respawn.")]
        [SerializeField, Min(0f)] float respawnDelay = 0.6f;

        Coroutine pending;

        void OnDisable() => pending = null;

        void OnTriggerEnter(Collider other)
        {
            if (pending == null && other.TryGetComponent(out RianController rian))
                pending = StartCoroutine(Respawn(rian));
        }

        IEnumerator Respawn(RianController rian)
        {
            if (respawnDelay > 0f)
                yield return new WaitForSeconds(respawnDelay);
            pending = null;
            Checkpoint.Respawn(rian);
        }
    }
}
