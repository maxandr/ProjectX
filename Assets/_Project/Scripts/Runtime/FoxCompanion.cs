using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectX
{
    public enum FoxState
    {
        Follow,
        WaitAtBarrier,
        InteractNode,
    }

    /// <summary>
    /// The Fox: trails Rian, hops small steps and gaps, waits at closed FoxBarriers,
    /// and goes to / leaves an IFoxNode when told to (see FoxCommander).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FoxCompanion : MonoBehaviour
    {
        [SerializeField] RianController rian;
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
        [Tooltip("Cracks in the ground up to this wide (cm) are walked over, not jumped.")]
        public float crackWidthCm = 20f;
        [Tooltip("Step-downs up to this deep (cm) are walked off, not jumped.")]
        public float stairStepCm = 80f;
        [Tooltip("How far before a step (cm, from the collider's front) the Fox takes off, so it hops up without touching the wall.")]
        public float stepTakeOffCm = 100f;
        [Tooltip("Height of a jump's apex above the higher of take-off and landing.")]
        public float jumpArc = 0.6f;
        [Tooltip("How far before a closed barrier's edge the Fox stops.")]
        [FormerlySerializedAs("waterStopMargin")]
        public float barrierStopMargin = 0.5f;

        [Header("Pose")]
        public Vector3 sitScale = new Vector3(1.1f, 0.7f, 1f);
        public Vector3 airScale = new Vector3(0.9f, 1.12f, 0.9f);
        public float trotBobSpeed = 18f;
        public float trotBobAmount = 0.04f;
        public float turnSpeed = 900f;

        public FoxState State { get; private set; }
        public bool IsBusy => State == FoxState.InteractNode;
        public RianController Leader => rian;
        public float Facing { get; private set; } = 1f;
        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => velocity;
        public event Action Jumped;

        CharacterController controller;
        IFoxNode node;
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
            float targetX = IsBusy ? node.StandPoint.x : rian.transform.position.x - rian.Facing * followDistance;
            bool blocked = ClampByBarriers(ref targetX);
            if (!IsBusy)
            {
                bool atStop = Mathf.Abs(transform.position.x - targetX) < arriveRadius * 2f;
                State = blocked && atStop ? FoxState.WaitAtBarrier : FoxState.Follow;
            }

            Move(targetX);

            if (IsBusy && !node.IsOccupied && IsGrounded
                && Mathf.Abs(transform.position.x - node.StandPoint.x) < arriveRadius * 2f
                && Mathf.Abs(transform.position.y - node.StandPoint.y) < 0.3f)
            {
                node.Occupy();
            }

            UpdatePose();
        }

        /// <summary>Leaves any current node and heads for <paramref name="target"/>.</summary>
        public void SendTo(IFoxNode target)
        {
            Recall();
            node = target;
            State = FoxState.InteractNode;
        }

        /// <summary>Leaves the current node, if any, and goes back to following Rian.</summary>
        public void Recall()
        {
            node?.Release();
            node = null;
            State = FoxState.Follow;
        }

        public void Teleport(Vector3 position)
        {
            Recall();
            controller.enabled = false;
            transform.position = PlayPath.Snap(position);
            controller.enabled = true;
            velocity = Vector3.zero;
            jumping = false;
        }

        /// <summary>Stops the Fox at the edge of any closed barrier.</summary>
        bool ClampByBarriers(ref float targetX)
        {
            foreach (var barrier in FoxBarrier.All)
                if (barrier.Clamp(transform.position.x, ref targetX, barrierStopMargin))
                    return true;
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
                // velocity.x is world X; scaling by the path direction keeps speed the speed along the path.
                velocity.x = Mathf.MoveTowards(velocity.x, dir * speed * PlayPath.Forward(transform.position.x).x, acceleration * dt);

                if (IsGrounded && dir != 0f)
                    Traverse(dir, Mathf.Abs(dx));
            }

            velocity.y -= gravity * dt;
            Vector3 forward = PlayPath.Forward(transform.position.x);
            velocity.z = velocity.x * forward.z / forward.x;
            Vector3 delta = velocity * dt;
            delta.z = PlayPath.ZAt(transform.position.x + delta.x) - transform.position.z; // stay on the path
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
        void Traverse(float dir, float distance)
        {
            Vector3 p = transform.position;
            Vector3 forward = PlayPath.Forward(p.x) * dir;
            float ahead = controller.radius + 0.15f;

            // Step up: a wall in front whose top is reachable. A walkable slope is left to the controller.
            // The wall is spotted stepTakeOffCm early so the hop starts before the Fox reaches it.
            float wallAhead = Mathf.Max(ahead + 0.1f, controller.radius + stepTakeOffCm / 100f);
            if (Probe.Raycast(p + Vector3.up * 0.15f, forward, wallAhead, out var wall))
            {
                if (Vector3.Angle(wall.normal, Vector3.up) <= controller.slopeLimit)
                    return;
                // The Fox stops before the wall anyway: walk on and leave it there.
                if (wall.distance > distance + controller.radius)
                    return;

                var top = PlayPath.Snap(new Vector3(wall.point.x + forward.x * 0.35f, p.y + maxStepUp + 0.2f, 0f));
                if (Probe.Raycast(top, Vector3.down, maxStepUp + 0.2f, out var step))
                {
                    if (step.point.y - p.y > controller.stepOffset)
                        JumpTo(step.point);
                }
                else if (wall.distance <= ahead + 0.1f)
                {
                    velocity.x = 0f; // too tall
                }
                return;
            }

            // Ground continues (including stair steps to walk off, and narrow cracks).
            if (Probe.SphereCast(p + forward * ahead + Vector3.up * 0.3f, crackWidthCm / 200f, Vector3.down, 0.3f + stairStepCm / 100f, out _))
                return;

            // Gap or step-down: find the nearest landing within jump range.
            float scanTop = p.y + maxStepUp + 0.2f;
            for (float d = ahead + 0.25f; d <= ahead + maxGapJump + 0.3f; d += 0.2f)
            {
                var origin = PlayPath.Snap(new Vector3(p.x + forward.x * d, scanTop, 0f));
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

        /// <summary>Hops in an arc that lands on <paramref name="landing"/>.</summary>
        public void JumpTo(Vector3 landing)
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
            float pathYaw = PlayPath.Yaw(transform.position.x);
            float targetYaw = Facing > 0f ? pathYaw : pathYaw + 180f;
            yaw = Mathf.MoveTowards(yaw, targetYaw, turnSpeed * dt);
            body.localRotation = Quaternion.Euler(0f, yaw, 0f);

            bool moving = Mathf.Abs(velocity.x) > 0.2f;
            Vector3 targetScale = !IsGrounded ? airScale
                : State == FoxState.WaitAtBarrier && !moving ? sitScale
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
