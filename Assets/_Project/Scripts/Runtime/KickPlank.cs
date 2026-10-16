using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectX
{
    /// <summary>
    /// An upright shipwreck plank on the far bank. When Rian reaches it she kicks it down
    /// across the chasm, forming a permanent bridge that opens the chasm for the Fox.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KickPlank : MonoBehaviour
    {
        [Tooltip("Pivot at the plank's base; the plank extends along its local +Y.")]
        [SerializeField] Transform plank;
        [Tooltip("Disabled while upright; enabled once the plank lies across the chasm.")]
        [SerializeField] Collider plankCollider;
        [FormerlySerializedAs("water")] [SerializeField] FoxBarrier chasm;
        [Tooltip("World rotation when fallen. (0, 0, 90) lays a +Y plank toward -X.")]
        public Vector3 fallenEuler = new Vector3(0f, 0f, 90f);
        public float fallDuration = 0.6f;

        bool kicked;

        void OnTriggerEnter(Collider other)
        {
            if (kicked || other.GetComponent<RianController>() == null)
                return;
            kicked = true;

            Vector3 fromPosition = plank.position;
            Vector3 toPosition = fromPosition;
            Quaternion fromRotation = plank.rotation;
            Quaternion toRotation = Quaternion.Euler(fallenEuler);
            Tween.Run(this, fallDuration,
                t => plank.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, t), Quaternion.Slerp(fromRotation, toRotation, t)),
                Ease.InQuad,
                () =>
                {
                    plankCollider.enabled = true;
                    chasm.isOpen = true;
                });
        }
    }
}
