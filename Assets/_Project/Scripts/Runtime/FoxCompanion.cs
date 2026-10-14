using System;
using UnityEngine;

namespace ProjectX
{
    public enum FoxState
    {
        Follow,
        WaitAtWater,
        InteractNode,
    }

    /// <summary>
    /// The Fox: trails Rian, hops small steps and gaps, waits at unbridged deep water,
    /// and goes to / leaves a FoxWeightPlate on Rian's command.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FoxCompanion : MonoBehaviour
    {
        [SerializeField] RianController rian;
        [SerializeField] RianInput input;
        [Tooltip("Turns to face the movement direction. FoxGrowth scales this.")]
        [SerializeField] Transform body;
        [Tooltip("Child of Body; receives the sit / hop / trot squash.")]
        [SerializeField] Transform pose;

        [Header("Follow")]
        public float followDistance = 2f;
        public float arriveRadius = 0.25f;
        public float walkSpeed = 4.5f;
        public float runSpeed = 7.5f;
        public float acceleration = 40f;

        [Header("Traversal")]
        public float gravity = 30f;
        public float maxGapJump = 1.5f;
        public float maxStepUp = 1f;
        [Tooltip("Lower than this the Fox hops down instead of walking off; deeper than safeDropHeight it refuses.")]
        public float maxHopDown = 1f;
        public float safeDropHeight = 8f;
        [Tooltip("Height of a jump's apex above the higher of take-off and landing.")]
        public float jumpArc = 0.6f;
        [Tooltip("How far before the water's edge the Fox stops.")]
        public float waterStopMargin = 0.5f;

        [Header("Commands")]
        public float commandRadius = 3f;

        [Header("Pose")]
        public Vector3 sitScale = new Vector3(1.1f, 0.7f, 1f);
        public Vector3 airScale = new Vector3(0.9f, 1.12f, 0.9f);
        public float trotBobSpeed = 18f;
        public float trotBobAmount = 0.04f;
        public float turnSpeed = 900f;

        public FoxState State { get; private set; }
        public float Facing { get; private set; } = 1f;
        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => velocity;
        public event Action Jumped;

        CharacterController controller;
        FoxWeightPlate plate;
        Vector3 velocity;
        bool jumping;
        float yaw = 90f;
        float trotPhase;
        Vector3 poseBasePosition;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            Physics.IgnoreCollision(controller, rian.GetComponent<CharacterController>());
            poseBasePosition = pose.localPosition;
        }

        void Update()
        {
            HandleCommand();

            float targetX = State == FoxState.InteractNode ? plate.StandPoint.x : rian.transform.position.x - rian.Facing * followDistance;
            bool blocked = ClampByWater(ref targetX);
            if (State != FoxState.InteractNode)
            {
                bool atStop = Mathf.Abs(transform.position.x - targetX) < arriveRadius * 2f;
                State = blocked && atStop ? FoxState.WaitAtWater : FoxState.Follow;
            }

            Move(targetX);

            if (State == FoxState.InteractNode && !plate.IsPressed && IsGrounded
                && Mathf.Abs(transform.position.x - plate.StandPoint.x) < arriveRadius * 2f
                && Mathf.Abs(transform.position.y - plate.StandPoint.y) < 0.3f)
            {
                plate.Press();
            }

            UpdatePose();
        }

        void HandleCommand()
        {
            if (!input.FoxPressed)
                return;

            if (State == FoxState.InteractNode)
            {
                Recall();
                return;
            }

            var near = FoxWeightPlate.FindNear(rian.transform.position, commandRadius);
            if (near != null)
            {
                plate = near;
                State = FoxState.InteractNode;
            }
        }

        public void Recall()
        {
            if (plate != null)
                plate.Release();
            plate = null;
            State = FoxState.Follow;
        }

        public void Teleport(Vector3 position)
        {
            Recall();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            velocity = Vector3.zero;
            jumping = false;
        }

        /// <summary>Stops the Fox at the bank of any deep water that has no bridge yet.</summary>
        bool ClampByWater(ref float targetX)
        {
            float x = transform.position.x;
            foreach (var zone in DeepWaterZone.All)
            {
                if (zone.isBridgeComplete)
                    continue;
                float leftStop = zone.leftEdgeX - waterStopMargin;
                float rightStop = zone.rightEdgeX + waterStopMargin;
                if (x <= leftStop + 0.05f && targetX > leftStop)
                {
                    targetX = leftStop;
                    return true;
                }
                if (x >= rightStop - 0.05f && targetX < rightStop)
                {
                    targetX = rightStop;
                    return true;
                }
            }
            return false;
        }

        void Move(float targetX)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            if (!jumping)
            {
                float dx = targetX - transform.position.x;
                float dir = Mathf.Abs(dx) > arriveRadius ? Mathf.Sign(dx) : 0f;
                if (dir != 0f)
                    Facing = dir;

                float speed = Mathf.Lerp(walkSpeed, runSpeed, Mathf.InverseLerp(2f, 6f, Mathf.Abs(dx)));
                speed = Mathf.Min(speed, Mathf.Abs(dx) * 5f); // ease into the stop point
                velocity.x = Mathf.MoveTowards(velocity.x, dir * speed, acceleration * dt);

                if (IsGrounded && dir != 0f)
                    Traverse(dir);
            }

            velocity.y -= gravity * dt;
            Vector3 delta = velocity * dt;
            delta.z = -transform.position.z;
            CollisionFlags flags = controller.Move(delta);

            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
                velocity.y = 0f;

            IsGrounded = controller.isGrounded;
            if (IsGrounded && velocity.y <= 0f)
            {
                velocity.y = -2f;
                jumping = false;
            }
        }

        /// <summary>Looks ahead for steps, gaps and ledges and decides to hop, walk on or stop.</summary>
        void Traverse(float dir)
        {
            Vector3 p = transform.position;
            Vector3 forward = Vector3.right * dir;
            float ahead = controller.radius + 0.15f;

            // Step up: a wall in front whose top is reachable.
            if (Probe.Raycast(p + Vector3.up * 0.15f, forward, ahead + 0.1f, out var wall))
            {
                var top = new Vector3(wall.point.x + dir * 0.35f, p.y + maxStepUp + 0.2f, 0f);
                if (Probe.Raycast(top, Vector3.down, maxStepUp + 0.2f, out var step))
                {
                    if (step.point.y - p.y > controller.stepOffset)
                        JumpTo(step.point);
                }
                else
                {
                    velocity.x = 0f; // too tall
                }
                return;
            }

            // Ground continues (including small step-downs the controller handles itself).
            if (Probe.Raycast(p + forward * ahead + Vector3.up * 0.3f, Vector3.down, 0.3f + 0.6f, out _))
                return;

            // Gap or step-down: find the nearest landing within jump range.
            float scanTop = p.y + maxStepUp + 0.2f;
            for (float d = ahead + 0.25f; d <= ahead + maxGapJump + 0.3f; d += 0.2f)
            {
                var origin = new Vector3(p.x + dir * d, scanTop, 0f);
                if (Probe.Raycast(origin, Vector3.down, maxStepUp + 0.2f + maxHopDown, out var landing))
                {
                    JumpTo(landing.point + forward * 0.3f);
                    return;
                }
            }

            // Nothing to jump to: walk off only if there is ground within a safe drop.
            if (!Probe.Raycast(p + forward * (ahead + 0.3f) + Vector3.up * 0.3f, Vector3.down, safeDropHeight, out _))
                velocity.x = 0f;
        }

        void JumpTo(Vector3 landing)
        {
            Vector3 p = transform.position;
            float apex = Mathf.Max(p.y, landing.y) + jumpArc;
            float vy = Mathf.Sqrt(2f * gravity * (apex - p.y));
            float flightTime = vy / gravity + Mathf.Sqrt(2f * (apex - landing.y) / gravity);
            velocity = new Vector3((landing.x - p.x) / flightTime, vy, 0f);
            jumping = true;
            IsGrounded = false;
            Jumped?.Invoke();
        }

        void UpdatePose()
        {
            float dt = Time.deltaTime;
            float targetYaw = Facing > 0f ? 90f : 270f;
            yaw = Mathf.MoveTowards(yaw, targetYaw, turnSpeed * dt);
            body.localRotation = Quaternion.Euler(0f, yaw, 0f);

            bool moving = Mathf.Abs(velocity.x) > 0.2f;
            Vector3 targetScale = !IsGrounded ? airScale
                : State == FoxState.WaitAtWater && !moving ? sitScale
                : Vector3.one;
            pose.localScale = Vector3.Lerp(pose.localScale, targetScale, 1f - Mathf.Exp(-12f * dt));

            if (IsGrounded && moving)
                trotPhase += dt * trotBobSpeed;
            else
                trotPhase = 0f;
            pose.localPosition = poseBasePosition + Vector3.up * (Mathf.Abs(Mathf.Sin(trotPhase)) * trotBobAmount);
        }
    }
}
