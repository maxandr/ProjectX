using UnityEngine;

namespace ProjectX
{
    /// <summary>What Rian gives up at a SacrificeNode: jump strength.</summary>
    public class RianVitality : MonoBehaviour
    {
        [SerializeField] RianController controller;

        [Range(0f, 1f)] public float jumpForceMultiplier = 0.85f;

        public void Drain()
        {
            controller.maxJumpForce *= jumpForceMultiplier;
        }
    }
}
