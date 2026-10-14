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
            animator.SetFloat(SpeedId, Mathf.Abs(controller.Velocity.x));
            animator.SetFloat(MotionSpeedId, 1f);
            animator.SetBool(FreeFallId, airTime > fallTimeout);
            if (grounded)
                animator.SetBool(JumpId, false);

            // 90 = facing +X, 270 = facing -X; MoveTowards (not MoveTowardsAngle) so she turns via 180, toward the camera.
            float targetYaw = controller.Facing > 0f ? 90f : 270f;
            yaw = Mathf.MoveTowards(yaw, targetYaw, turnSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void OnJumped() => animator.SetBool(JumpId, true);

        // Receivers for the animation events baked into the Starter Assets clips (no audio here).
        void OnFootstep(AnimationEvent animationEvent) { }
        void OnLand(AnimationEvent animationEvent) { }
    }
}
