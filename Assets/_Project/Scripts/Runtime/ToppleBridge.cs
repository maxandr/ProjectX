using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectX
{
    /// <summary>
    /// Something tall standing at the edge of a chasm (the shipwreck mast) that topples across it to form a
    /// permanent bridge, opening the chasm for the Fox. It strains toward the fall while pushed
    /// (<see cref="Strain"/>), sways when nudged (<see cref="Nudge"/>) and falls on <see cref="Topple"/>.
    /// Knows nothing about who pushes it.
    /// </summary>
    public class ToppleBridge : MonoBehaviour
    {
        [Tooltip("Pivot at the base; it extends along its local +Y.")]
        [SerializeField] Transform pivot;
        [Tooltip("Disabled while standing; enabled once it lies across the chasm.")]
        [SerializeField] Collider bridgeCollider;
        [SerializeField] FoxBarrier chasm;
        [Tooltip("World rotation when fallen. (0, 0, 90) lays a +Y pivot toward -X.")]
        public Vector3 fallenEuler = new Vector3(0f, 0f, 90f);
        [Tooltip("Fall time in milliseconds.")]
        public float fallDurationMs = 900f;

        [Header("Strain")]
        [Tooltip("Degrees it leans toward the fall at full push.")]
        public float strainAngle = 5f;
        [Tooltip("Degrees of the creaking wobble at full push.")]
        public float wobbleAngle = 1.5f;
        [Tooltip("Wobbles per second.")]
        public float wobbleRate = 7f;
        [Tooltip("Degrees of the sway when nudged.")]
        public float nudgeAngle = 2.5f;
        [Tooltip("How long a nudge takes to settle, in milliseconds.")]
        public float nudgeTimeMs = 1600f;
        [Tooltip("Back-and-forth swings in one nudge.")]
        public float nudgeSwings = 2f;

        [Header("Splash")]
        [Tooltip("Ripple drops spread along the length when it lands.")]
        public int splashDrops = 6;
        [Tooltip("Radius of each drop in centimetres.")]
        public float splashRadiusCm = 60f;
        [Tooltip("Height of each drop in centimetres.")]
        public float splashHeightCm = 8f;

        [Tooltip("Raised as it starts to fall (e.g. snap the rope, dust).")]
        public UnityEvent onToppleStart;
        [Tooltip("Raised once it lies across the chasm (e.g. to call the Fox back to Rian).")]
        public UnityEvent onFallen;

        /// <summary>Raised when <see cref="Nudge"/> starts a sway.</summary>
        public event Action Nudged;

        Quaternion standing;
        Vector3 fallAxis;
        float strain;
        float targetStrain;
        float nudge;
        float wobbleClock;
        Coroutine straining;
        bool toppled;

        void Awake()
        {
            standing = pivot.rotation;
            // The axis that tips the standing pose toward the fallen one.
            (Quaternion.Euler(fallenEuler) * Quaternion.Inverse(standing)).ToAngleAxis(out _, out fallAxis);
        }

        /// <summary>Leans it toward the fall by <paramref name="amount"/> (0..1) with a creaking wobble.</summary>
        public void Strain(float amount)
        {
            targetStrain = Mathf.Clamp01(amount);
            StartStraining();
        }

        /// <summary>A short sway, as if brushed against.</summary>
        public void Nudge()
        {
            if (toppled)
                return;
            nudge = 1f;
            StartStraining();
            Nudged?.Invoke();
        }

        public void Topple()
        {
            if (toppled)
                return;
            toppled = true;
            if (straining != null)
                StopCoroutine(straining);
            onToppleStart.Invoke();

            Quaternion from = pivot.rotation;
            Quaternion to = Quaternion.Euler(fallenEuler);
            Tween.Run(this, fallDurationMs / 1000f, t => pivot.rotation = Quaternion.Slerp(from, to, t), Ease.InQuad, Land);
        }

        void Land()
        {
            bridgeCollider.enabled = true;
            chasm.isOpen = true;
            Splash();
            onFallen.Invoke();
        }

        void Splash()
        {
            Bounds span = bridgeCollider.bounds;
            for (int i = 0; i < splashDrops; i++)
            {
                float x = Mathf.Lerp(span.min.x, span.max.x, (i + 0.5f) / splashDrops);
                WaterRipples.AddDrop(new Vector3(x, 0f, span.center.z), splashRadiusCm / 100f, splashHeightCm / 100f);
            }
        }

        void StartStraining()
        {
            if (!toppled && straining == null)
                straining = StartCoroutine(Straining());
        }

        IEnumerator Straining()
        {
            float nudgeDecay = 1000f / nudgeTimeMs;
            while (strain > 0.001f || targetStrain > 0f || nudge > 0f)
            {
                float dt = Time.deltaTime;
                strain = Mathf.MoveTowards(strain, targetStrain, 4f * dt);
                nudge = Mathf.MoveTowards(nudge, 0f, nudgeDecay * dt);
                wobbleClock += dt * wobbleRate * 2f * Mathf.PI;

                float sway = Mathf.Sin((1f - nudge) * nudgeSwings * 2f * Mathf.PI) * nudge * nudgeAngle;
                float angle = strain * strainAngle + Mathf.Sin(wobbleClock) * strain * wobbleAngle + sway;
                pivot.rotation = Quaternion.AngleAxis(angle, fallAxis) * standing;
                yield return null;
            }
            strain = 0f;
            pivot.rotation = standing;
            straining = null;
        }
    }
}
