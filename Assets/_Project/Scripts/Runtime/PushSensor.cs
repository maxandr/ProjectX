using UnityEngine;
using UnityEngine.Events;

namespace ProjectX
{
    /// <summary>
    /// A trigger zone where Rian can push something over: standing on the far side of <see cref="pushBlocker"/>
    /// within <see cref="reachCm"/> of it, facing it, grounded, and holding the push button (Sacrifice, E) for
    /// <see cref="holdTimeMs"/>. Knows nothing about what is pushed; it reports progress and the finished push
    /// through events. Only updates while Rian is inside the zone.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PushSensor : MonoBehaviour
    {
        [Tooltip("World X direction she pushes in: -1 left, 1 right. She has to stand on the opposite side.")]
        public float pushDirection = -1f;
        [Tooltip("How close to the blocker she has to stand, in centimetres (she has to be up against it).")]
        public float reachCm = 15f;
        [Tooltip("How long Rian has to hold the button, in milliseconds.")]
        public float holdTimeMs = 800f;
        [Tooltip("How long the progress takes to drain back to zero once she lets go, in milliseconds.")]
        public float releaseTimeMs = 500f;
        [Tooltip("Solid collider in front of what she pushes. It turns on only once she is fully on the pushing " +
                 "side, so she can still walk past it from the other side, and stays up until ReleaseBlocker.")]
        [SerializeField] Collider pushBlocker;

        [Tooltip("Raised once when Rian first steps into the zone.")]
        public UnityEvent onFirstEnter;
        [Tooltip("Raised when Rian starts (true) or stops (false) pushing, e.g. to play her push animation.")]
        public UnityEvent<bool> onPushingChanged;
        [Tooltip("Push progress 0..1, raised every frame it changes.")]
        public UnityEvent<float> onProgress;
        [Tooltip("Raised once the push is complete; the sensor then switches off for good.")]
        public UnityEvent onPushed;

        RianController rian;
        RianInput input;
        Collider rianCollider;
        float progress;
        bool held;
        bool pushing;
        bool entered;
        bool pushed;

        void Awake()
        {
            enabled = false;
            pushBlocker.enabled = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (pushed || !other.TryGetComponent(out RianController controller))
                return;
            rian = controller;
            rianCollider = other;
            input = controller.GetComponent<RianInput>();
            input.SacrificePressed += OnPressed;
            input.SacrificeReleased += OnReleased;
            held = input.SacrificeHeld;
            enabled = true;
            if (entered)
                return;
            entered = true;
            onFirstEnter.Invoke();
        }

        void OnTriggerExit(Collider other)
        {
            if (other == rianCollider)
                Release();
        }

        void OnDisable() => Unsubscribe();

        void OnPressed() => held = true;
        void OnReleased() => held = false;

        void Update()
        {
            if (!pushBlocker.enabled)
                pushBlocker.enabled = GapToBlocker() > 0f;

            SetPushing(held
                && pushBlocker.enabled
                && rian.IsGrounded
                && Mathf.Approximately(rian.Facing, Mathf.Sign(pushDirection))
                && GapToBlocker() < reachCm / 100f);
            float rate = pushing ? 1000f / holdTimeMs : -1000f / releaseTimeMs;
            float next = Mathf.Clamp01(progress + rate * Time.deltaTime);
            if (Mathf.Approximately(next, progress))
                return;
            progress = next;
            onProgress.Invoke(progress);
            if (progress < 1f)
                return;
            // The blocker stays up until ReleaseBlocker, so she can't walk on into whatever she just pushed.
            pushed = true;
            SetPushing(false);
            enabled = false;
            onPushed.Invoke();
        }

        void SetPushing(bool value)
        {
            if (value == pushing)
                return;
            pushing = value;
            onPushingChanged.Invoke(value);
        }

        /// <summary>Drops the blocker after a finished push (e.g. once the pushed thing has landed).</summary>
        public void ReleaseBlocker() => pushBlocker.enabled = false;

        // Distance from Rian's collider to the blocker on the side she pushes from; negative while she overlaps it
        // or stands on the other side.
        float GapToBlocker()
        {
            Bounds self = rianCollider.bounds;
            Bounds block = pushBlocker.bounds;
            return pushDirection < 0f ? self.min.x - block.max.x : block.min.x - self.max.x;
        }

        void Release()
        {
            Unsubscribe();
            SetPushing(false);
            rian = null;
            rianCollider = null;
            held = false;
            enabled = false;
            if (pushed)
                return;
            pushBlocker.enabled = false;
            if (progress <= 0f)
                return;
            progress = 0f;
            onProgress.Invoke(0f);
        }

        void Unsubscribe()
        {
            if (input == null)
                return;
            input.SacrificePressed -= OnPressed;
            input.SacrificeReleased -= OnReleased;
            input = null;
        }
    }
}
