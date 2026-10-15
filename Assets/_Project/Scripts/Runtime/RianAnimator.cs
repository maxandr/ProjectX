using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Drives the Starter Assets third-person Animator (Speed, MotionSpeed, Grounded, Jump, FreeFall)
    /// from <see cref="RianController"/> and turns the model to face the move direction.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class RianAnimator : MonoBehaviour
    {
        [SerializeField] RianController controller;

        [Tooltip("Degrees per second when turning around (turns through facing the camera).")]
        public float turnSpeed = 900f;
        [Tooltip("Time in the air before the falling animation starts.")]
        public float fallTimeout = 0.15f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int FreeFallId = Animator.StringToHash("FreeFall");

        Animator animator;
        float yaw = 90f;
        float airTime;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
        }

        void OnEnable() => controller.Jumped += OnJumped;
        void OnDisable() => controller.Jumped -= OnJumped;

        void LateUpdate()
        {
            bool grounded = controller.IsGrounded;
            airTime = grounded ? 0f : airTime + Time.deltaTime;

            animator.SetBool(GroundedId, grounded);
            Vector3 velocity = controller.Velocity;
            animator.SetFloat(SpeedId, new Vector2(velocity.x, velocity.z).magnitude);
            animator.SetFloat(MotionSpeedId, 1f);
            animator.SetBool(FreeFallId, airTime > fallTimeout);
            if (grounded)
                animator.SetBool(JumpId, false);

            // Path yaw (90 when the path runs along +X) or that + 180; MoveTowards (not MoveTowardsAngle) so she
            // turns via 180, toward the camera.
            float pathYaw = PlayPath.Yaw(controller.transform.position.x);
            float targetYaw = controller.Facing > 0f ? pathYaw : pathYaw + 180f;
            yaw = Mathf.MoveTowards(yaw, targetYaw, turnSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void OnJumped() => animator.SetBool(JumpId, true);

        // Receivers for the animation events baked into the Starter Assets clips (no audio here).
        void OnFootstep(AnimationEvent animationEvent) { }
        void OnLand(AnimationEvent animationEvent) { }
    }
}
