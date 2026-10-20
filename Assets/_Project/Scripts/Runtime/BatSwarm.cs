using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A one-shot scare: when Rian first enters the trigger volume, the <see cref="swarm"/> particle system turns to
    /// face a point just beside the main camera and bursts, so the bats stream out of the dark and fly past the lens.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BatSwarm : MonoBehaviour
    {
        [Tooltip("The bat particle system; it emits along its local +Z (cone shape), so it is aimed by rotating it.")]
        [SerializeField] ParticleSystem swarm;

        [Tooltip("Offset from the camera (metres, camera space) the swarm aims at, so the bats skim past instead of hitting the lens dead-centre.")]
        [SerializeField] Vector3 aimOffset = new(1.5f, 1.2f, 0f);

        bool fired;

        void OnTriggerEnter(Collider other)
        {
            if (fired || swarm == null || !other.TryGetComponent(out RianInput _))
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            fired = true;
            Vector3 target = cam.transform.TransformPoint(aimOffset);
            swarm.transform.rotation = Quaternion.LookRotation(target - swarm.transform.position);
            swarm.Play();
        }
    }
}
