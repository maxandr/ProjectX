using UnityEngine;

namespace ProjectX
{
    /// <summary>Grows the Fox's body as it crosses a span of X (the sacrifice bridge).</summary>
    public class FoxGrowth : MonoBehaviour
    {
        [SerializeField] Transform body;
        public float grownScale = 1.35f;
        [Tooltip("Max change in growth progress per second, so the scale never pops.")]
        public float growthRate = 1.2f;

        Vector3 baseScale;
        float fromX, toX;
        float progress;
        float shown;
        bool growing;

        void Awake() => baseScale = body.localScale;

        public void Begin(float startX, float endX)
        {
            fromX = startX;
            toX = endX;
            growing = true;
        }

        void LateUpdate()
        {
            if (!growing)
                return;

            progress = Mathf.Max(progress, Mathf.InverseLerp(fromX, toX, transform.position.x));
            shown = Mathf.MoveTowards(shown, progress, growthRate * Time.deltaTime);
            body.localScale = baseScale * Mathf.Lerp(1f, grownScale, Ease.InOutSine(shown));
            if (shown >= 1f)
                growing = false;
        }
    }
}
