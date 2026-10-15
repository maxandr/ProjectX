using Unity.Cinemachine;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Turns the CinemachineCamera's yaw to look across the <see cref="PlayPath"/> at the target's X, plus the
    /// yaw/pitch keys authored on the path. The camera has no rotation control, so the Position Composer orbits it
    /// around the target.
    /// At runtime the camera tracks a hidden anchor instead of the target: it follows the target's X/Z but only
    /// takes its height while grounded, so jumps don't move the camera. It drops at once when the target falls below
    /// it and rises smoothly (after landing higher, or once the target is <see cref="maxRiseInAir"/> above it).
    /// </summary>
    [DefaultExecutionOrder(-50)] // before CinemachineBrain's LateUpdate
    public class PathCamera : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Tooltip("How much of the path's turn the camera follows (1 = always square on to the path).")]
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("Metres ahead of the target (in its facing direction) where the path direction is read.")]
        public float lookAhead = 2f;
        [Tooltip("Time to catch up with the path's turn.")]
        public float smoothTime = 0.6f;

        [Header("Height")]
        [Tooltip("How far the target can rise above the camera's tracked height in the air before the camera follows.")]
        public float maxRiseInAir = 3f;
        [Tooltip("Time to rise to a new, higher ground height (e.g. after landing on a ledge). Drops are followed at once.")]
        public float riseSmoothTime = 0.6f;

        RianController rian;
        Transform anchor;
        float riseVelocity;
        float basePitch;
        float baseYaw;
        float pitch;
        float yaw;
        float pitchVelocity;
        float yawVelocity;

        void Awake()
        {
            rian = target.GetComponent<RianController>();
            Vector3 euler = transform.eulerAngles;
            basePitch = euler.x;
            baseYaw = euler.y;
            Vector2 rotation = TargetRotation();
            pitch = rotation.x;
            yaw = rotation.y;

            anchor = new GameObject("Camera Anchor") { hideFlags = HideFlags.HideAndDontSave }.transform;
            anchor.position = target.position;
            GetComponent<CinemachineCamera>().Target.TrackingTarget = anchor;
        }

        void OnDestroy()
        {
            if (anchor != null)
                Destroy(anchor.gameObject);
        }

        void LateUpdate()
        {
            UpdateAnchor();

            Vector2 goal = TargetRotation();
            pitch = Mathf.SmoothDampAngle(pitch, goal.x, ref pitchVelocity, smoothTime);
            yaw = Mathf.SmoothDampAngle(yaw, goal.y, ref yawVelocity, smoothTime);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        void UpdateAnchor()
        {
            Vector3 p = target.position;
            float y = anchor.position.y;
            bool grounded = rian == null || rian.IsGrounded;
            float goal = grounded ? p.y : Mathf.Clamp(y, p.y - maxRiseInAir, p.y);
            if (goal > y)
            {
                y = Mathf.SmoothDamp(y, goal, ref riseVelocity, riseSmoothTime);
            }
            else
            {
                y = goal;
                riseVelocity = 0f;
            }
            anchor.position = new Vector3(p.x, y, p.z);
        }

        // (pitch, yaw). A straight path has yaw 90, which leaves the camera at its authored rotation;
        // PlayPath's camera keys add on top.
        Vector2 TargetRotation()
        {
            float facing = rian != null ? rian.Facing : 1f;
            float x = target.position.x + facing * lookAhead;
            Vector2 keyed = PlayPath.CameraRotationAt(x);
            return new Vector2(basePitch + keyed.x, baseYaw + weight * Mathf.DeltaAngle(90f, PlayPath.Yaw(x)) + keyed.y);
        }
    }
}
