using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Splines.Interpolators;

namespace ProjectX
{
    /// <summary>
    /// The line Rian and the Fox walk along: a spline that must keep moving toward +X. Gameplay still uses world X
    /// as progress; the path only supplies the Z at each X and the walk direction there. Without a path the line is Z = 0.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [DefaultExecutionOrder(-100)]
    public class PlayPath : MonoBehaviour
    {
        [Tooltip("Lookup samples per metre of spline length.")]
        public float samplesPerMeter = 4f;

        [Header("Camera")]
        [Tooltip("Degrees added to PathCamera's rotation, like Transform rotation: X = pitch (positive looks down), " +
                 "Y = yaw (positive turns right). Index is the knot number; 1.5 is halfway between knots 1 and 2.")]
        public SplineData<float2> cameraRotation = new SplineData<float2>();

        static PlayPath active;

        SplineContainer container;
        float[] xs;
        float[] zs;
        float[] ts; // normalized spline t of each sample
        int count;

        void OnEnable()
        {
            container = GetComponent<SplineContainer>();
            active = this;
            Bake();
            Spline.Changed += OnSplineChanged;
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            if (active == this)
                active = null;
        }

        void OnSplineChanged(Spline spline, int knot, SplineModification modification)
        {
            if (container != null && spline == container.Spline)
                Bake();
        }

        /// <summary>Samples the spline into an X-sorted Z table; samples that turn back in X are skipped.</summary>
        public void Bake()
        {
            int n = Mathf.Max(2, Mathf.CeilToInt(container.CalculateLength() * samplesPerMeter) + 1);
            xs = new float[n];
            zs = new float[n];
            ts = new float[n];
            count = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                Vector3 p = container.EvaluatePosition(t);
                if (count > 0 && p.x <= xs[count - 1])
                    continue;
                xs[count] = p.x;
                zs[count] = p.z;
                ts[count] = t;
                count++;
            }
            if (count < n / 2)
                Debug.LogWarning($"{name}: the spline turns back in X, so parts of it are ignored.", this);
        }

        /// <summary>Z of the path at world X (held flat past either end).</summary>
        public static float ZAt(float x) => active == null || active.count < 2 ? 0f : active.Lookup(active.zs, x);

        /// <summary>Camera rotation offset in degrees (x = pitch, y = yaw) keyed on the spline at world X;
        /// zero with no path or no keys.</summary>
        public static Vector2 CameraRotationAt(float x)
        {
            if (active == null || active.count < 2 || active.cameraRotation == null || active.cameraRotation.Count == 0)
                return Vector2.zero;
            return active.cameraRotation.Evaluate(active.container.Spline, active.Lookup(active.ts, x),
                PathIndexUnit.Normalized, new SmoothStepFloat2());
        }

        /// <summary>Interpolates a baked per-sample table at world X (held flat past either end).</summary>
        float Lookup(float[] values, float x)
        {
            int last = count - 1;
            if (x <= xs[0])
                return values[0];
            if (x >= xs[last])
                return values[last];

            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (xs[mid] <= x)
                    lo = mid;
                else
                    hi = mid;
            }
            return Mathf.Lerp(values[lo], values[hi], (x - xs[lo]) / (xs[hi] - xs[lo]));
        }

        /// <summary>Unit horizontal walk direction (toward +X) at world X.</summary>
        public static Vector3 Forward(float x)
        {
            const float h = 0.1f;
            float slope = (ZAt(x + h) - ZAt(x - h)) / (2f * h);
            return new Vector3(1f, 0f, slope).normalized;
        }

        /// <summary>Yaw (degrees) that faces along the path toward +X; 90 on a straight Z = 0 path.</summary>
        public static float Yaw(float x)
        {
            Vector3 f = Forward(x);
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        /// <summary>The point at the same X and Y, moved onto the path.</summary>
        public static Vector3 Snap(Vector3 position) => new Vector3(position.x, position.y, ZAt(position.x));
    }
}
