using System.Collections.Generic;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A flooded trench. The Fox will not cross it until <see cref="isBridgeComplete"/> is set;
    /// Rian falling into the trigger volume respawns at the last checkpoint.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DeepWaterZone : MonoBehaviour
    {
        static readonly List<DeepWaterZone> all = new List<DeepWaterZone>();
        public static IReadOnlyList<DeepWaterZone> All => all;

        [Tooltip("World X of the left bank's edge.")]
        public float leftEdgeX;
        [Tooltip("World X of the right bank's edge.")]
        public float rightEdgeX;
        public bool isBridgeComplete;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RianController rian))
                Checkpoint.Respawn(rian);
        }
    }
}
