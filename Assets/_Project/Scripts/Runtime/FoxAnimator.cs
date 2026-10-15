using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Drives the Starter Assets third-person Animator (Speed, MotionSpeed, Grounded, Jump, FreeFall)
    /// from <see cref="FoxCompanion"/>. Facing is handled by FoxCompanion turning Fox/Body.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FoxAnimator : MonoBehaviour
    {
        [SerializeField] FoxCompanion fox;

        [Tooltip("Time in the air before the falling animation starts.")]
        public float fallTimeout = 0.15f;
        [Tooltip("Ground speed of the run clip at scale 1; the model is small, so playback speeds up to keep the feet planted.")]
        public float runClipSpeed = 6f;
        public float maxPlaybackSpeed = 2f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int FreeFallId = Animator.StringToHash("FreeFall");

        Animator animator;
        float airTime;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
        }

        void OnEnable() => fox.Jumped += OnJumped;
        void OnDisable() => fox.Jumped -= OnJumped;

        void LateUpdate()
        {
            bool grounded = fox.IsGrounded;
            airTime = grounded ? 0f : airTime + Time.deltaTime;

            Vector3 velocity = fox.Velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            float scale = Mathf.Max(transform.lossyScale.y, 0.01f);
            animator.SetBool(GroundedId, grounded);
            animator.SetFloat(SpeedId, speed);
            animator.SetFloat(MotionSpeedId, Mathf.Clamp(speed / (runClipSpeed * scale), 1f, maxPlaybackSpeed));
            animator.SetBool(FreeFallId, airTime > fallTimeout);
            if (grounded)
                animator.SetBool(JumpId, false);
        }

        void OnJumped() => animator.SetBool(JumpId, true);

        // Receivers for the animation events baked into the Starter Assets clips (no audio here).
        void OnFootstep(AnimationEvent animationEvent) { }
        void OnLand(AnimationEvent animationEvent) { }
    }
}
