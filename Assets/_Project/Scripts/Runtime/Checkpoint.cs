using UnityEngine;

namespace ProjectX
{
    /// <summary>Trigger that becomes Rian's respawn point when she passes through it.</summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        static Checkpoint current;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<RianController>() != null)
                current = this;
        }

        public static void Respawn(RianController rian)
        {
            rian.Teleport(current != null ? current.transform.position : rian.SpawnPoint);
        }
    }
}
