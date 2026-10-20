using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Drives the Fox Animator (Speed, MotionSpeed, Grounded, Jump, FreeFall) from <see cref="FoxCompanion"/>.
    /// Facing is handled by FoxCompanion turning Fox/Body.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FoxAnimator : MonoBehaviour
    {
        [SerializeField] FoxCompanion fox;

        [Tooltip("Time in the air before the falling animation starts; it waits for the Fox to be going down, so a jump falls after its apex.")]
        public float fallTimeout = 0.05f;
        [Tooltip("Drop (m) from the highest point of the flight to the ground that plays the landing animation; softer landings run straight on.")]
        public float hardLandingHeight = 1.5f;
        [Tooltip("Ground speed of the run clip at scale 1; the model is small, so playback speeds up to keep the feet planted.")]
        public float runClipSpeed = 6f;
        public float maxPlaybackSpeed = 2f;
        [Tooltip("Length (ms) of the push-off at the start of the jump clip; playback is timed so it ends as the Fox leaves the ground.")]
        public float jumpPushOffMs = 500f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int FreeFallId = Animator.StringToHash("FreeFall");
        static readonly int HardLandingId = Animator.StringToHash("HardLanding");
        static readonly int JumpSpeedId = Animator.StringToHash("JumpSpeed");

        Animator animator;
        float airTime;
        float peakY;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
        }

        void OnEnable()
        {
            fox.Jumped += OnJumped;
            peakY = fox.transform.position.y;
        }

        void OnDisable() => fox.Jumped -= OnJumped;

        void LateUpdate()
        {
            // Pushing off for a jump already counts as the jump, though the Fox has not left the ground yet.
            bool grounded = fox.IsGrounded && !fox.IsTakingOff;
            airTime = fox.IsGrounded ? 0f : airTime + Time.deltaTime;

            Vector3 velocity = fox.Velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            float scale = Mathf.Max(transform.lossyScale.y, 0.01f);
            animator.SetBool(GroundedId, grounded);
            animator.SetFloat(SpeedId, speed);
            animator.SetFloat(MotionSpeedId, Mathf.Clamp(speed / (runClipSpeed * scale), 1f, maxPlaybackSpeed));
            animator.SetBool(FreeFallId, airTime > fallTimeout && velocity.y < 0f);

            // Read on the landing frame, before the peak resets to the ground.
            float y = fox.transform.position.y;
            if (!grounded)
                peakY = Mathf.Max(peakY, y);
            animator.SetBool(HardLandingId, peakY - y >= hardLandingHeight);
            if (grounded)
            {
                peakY = y;
                animator.SetBool(JumpId, false);
            }
        }

        void OnJumped()
        {
            animator.SetFloat(JumpSpeedId, fox.takeOffDelayMs > 0f ? Mathf.Max(jumpPushOffMs / fox.takeOffDelayMs, 1f) : 1f);
            animator.SetBool(JumpId, true);
        }

        // Receivers for the animation events baked into the Starter Assets clips (no audio here).
        void OnFootstep(AnimationEvent animationEvent) { }
        void OnLand(AnimationEvent animationEvent) { }
    }
}
