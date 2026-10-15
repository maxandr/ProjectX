using UnityEngine;

namespace ProjectX
{
    /// <summary>What Rian gives up at a SacrificeNode: jump strength.</summary>
    public class RianVitality : MonoBehaviour
    {
        [SerializeField] RianController controller;

        /// <summary>Scales Rian's jump force by <paramref name="jumpForceMultiplier"/>.</summary>
        public void Drain(float jumpForceMultiplier)
        {
            controller.maxJumpForce *= jumpForceMultiplier;
        }
    }
}
