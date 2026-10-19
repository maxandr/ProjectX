using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Turns the CinemachineCamera's yaw to look across the <see cref="PlayPath"/> at the target's X, plus the
    /// yaw/pitch keys authored on the path. The camera has no rotation control, so the Position Composer orbits it
    /// around the target.
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

        RianController rian;
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
        }

        void LateUpdate()
        {
            Vector2 goal = TargetRotation();
            pitch = Mathf.SmoothDampAngle(pitch, goal.x, ref pitchVelocity, smoothTime);
            yaw = Mathf.SmoothDampAngle(yaw, goal.y, ref yawVelocity, smoothTime);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // (pitch, yaw). A straight path has yaw 90, which leaves the camera at its authored rotation;
        // PlayPath's camera keys add on top.
        Vector2 TargetRotation() =>
            RotationAt(target.position.x, rian != null ? rian.Facing : 1f, basePitch, baseYaw);

        /// <summary>The settled (pitch, yaw) for a target at <paramref name="targetX"/>, from the authored base angles.</summary>
        public Vector2 RotationAt(float targetX, float facing, float basePitch, float baseYaw)
        {
            float x = targetX + facing * lookAhead;
            Vector2 keyed = PlayPath.CameraRotationAt(x);
            return new Vector2(basePitch + keyed.x, baseYaw + weight * Mathf.DeltaAngle(90f, PlayPath.Yaw(x)) + keyed.y);
        }
    }
}
