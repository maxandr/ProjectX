using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A capsule (or a sphere, without <see cref="end"/>) that <see cref="SpringBones"/> keep their tips out of.
    /// It is not a physics collider, so it never touches the CharacterController or the trigger zones.
    /// </summary>
    public class SpringBoneCollider : SpringBoneShape
    {
        [Tooltip("Other end of the capsule (e.g. the next bone). Empty makes a sphere.")]
        [SerializeField] Transform end;
        [Tooltip("In cm.")]
        public float radiusCm = 4f;

        float Radius => radiusCm / 100f;

        public override Vector3 PushOut(Vector3 point, float margin)
        {
            Vector3 a = transform.position;
            Vector3 closest = a;
            if (end != null)
            {
                Vector3 ab = end.position - a;
                float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                closest = a + ab * t;
            }

            Vector3 offset = point - closest;
            float minDistance = Radius + margin;
            float distance = offset.magnitude;
            if (distance >= minDistance || distance < 1e-6f)
                return point;
            return closest + offset * (minDistance / distance);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, Radius);
            if (end == null)
                return;
            Gizmos.DrawWireSphere(end.position, Radius);
            Gizmos.DrawLine(transform.position, end.position);
        }
    }
}
