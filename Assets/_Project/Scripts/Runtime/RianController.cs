using System;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Side-on platformer movement along the <see cref="PlayPath"/>: acceleration, variable jump,
    /// coyote time and jump buffering. Animation is driven by <see cref="RianAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(RianInput))]
    public class RianController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float groundAcceleration = 60f;
        public float groundDeceleration = 70f;
        public float airAcceleration = 30f;

        [Header("Jump")]
        [Tooltip("Initial upward velocity of a full jump. The Vitality Sacrifice scales this down.")]
        public float maxJumpForce = 11f;
        public float gravity = 30f;
        public float fallGravityMultiplier = 1.6f;
        [Tooltip("Upward velocity is multiplied by this when the jump button is released early.")]
        [Range(0f, 1f)] public float jumpCutMultiplier = 0.45f;
        public float maxFallSpeed = 25f;
        public float coyoteTime = 0.15f;
        public float jumpBufferTime = 0.15f;

        public event Action Jumped;

        public Vector3 Velocity => velocity;
        public bool IsGrounded { get; private set; }
        public float Facing { get; private set; } = 1f;
        public Vector3 SpawnPoint { get; private set; }

        CharacterController controller;
        RianInput input;
        Vector3 velocity;
        float coyoteTimer;
        float jumpBufferTimer;
        bool jumpCutAvailable;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<RianInput>();
            SpawnPoint = transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            float moveX = input.MoveX;
            if (Mathf.Abs(moveX) > 0.1f)
                Facing = Mathf.Sign(moveX);

            coyoteTimer = IsGrounded ? coyoteTime : coyoteTimer - dt;
            jumpBufferTimer = input.JumpPressed ? jumpBufferTime : jumpBufferTimer - dt;

            // velocity.x is world X; scaling by the path direction keeps moveSpeed the speed along the path.
            Vector3 forward = PlayPath.Forward(transform.position.x);
            float targetVx = moveX * moveSpeed * forward.x;
            float accel = IsGrounded
                ? (Mathf.Abs(targetVx) > 0.01f ? groundAcceleration : groundDeceleration)
                : airAcceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, targetVx, accel * dt);

            if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                velocity.y = maxJumpForce;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                jumpCutAvailable = true;
                IsGrounded = false;
                Jumped?.Invoke();
            }

            if (jumpCutAvailable && !input.JumpHeld && velocity.y > 0f)
            {
                velocity.y *= jumpCutMultiplier;
                jumpCutAvailable = false;
            }
            if (velocity.y <= 0f)
                jumpCutAvailable = false;

            float g = velocity.y < 0f ? gravity * fallGravityMultiplier : gravity;
            velocity.y = Mathf.Max(velocity.y - g * dt, -maxFallSpeed);

            velocity.z = velocity.x * forward.z / forward.x;
            Vector3 delta = velocity * dt;
            delta.z = PlayPath.ZAt(transform.position.x + delta.x) - transform.position.z; // stay on the path
            CollisionFlags flags = controller.Move(delta);

            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
                velocity.y = 0f;
            if ((flags & CollisionFlags.Sides) != 0 && controller.velocity.x * velocity.x <= 0f)
                velocity.x = 0f;

            IsGrounded = controller.isGrounded;
            if (IsGrounded)
                velocity.y = -2f; // keep the controller snapped to the ground
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = PlayPath.Snap(position);
            controller.enabled = true;
            velocity = Vector3.zero;
        }
    }
}
