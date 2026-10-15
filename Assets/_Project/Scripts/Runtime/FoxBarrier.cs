using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectX
{
    /// <summary>
    /// A stretch of world X, spanned by the collider's bounds, that the Fox will not cross
    /// until <see cref="isOpen"/> is set. The Fox waits at whichever edge it reaches first.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FoxBarrier : MonoBehaviour
    {
        static readonly List<FoxBarrier> all = new List<FoxBarrier>();
        public static IReadOnlyList<FoxBarrier> All => all;

        [FormerlySerializedAs("isBridgeComplete")]
        public bool isOpen;

        Collider span;

        public float LeftEdgeX => span.bounds.min.x;
        public float RightEdgeX => span.bounds.max.x;

        protected virtual void Awake() => span = GetComponent<Collider>();
        protected virtual void OnEnable() => all.Add(this);
        protected virtual void OnDisable() => all.Remove(this);

        /// <summary>
        /// Keeps a mover standing at <paramref name="x"/> on its own side, <paramref name="margin"/> short
        /// of the edge. Returns true when it moved <paramref name="targetX"/>.
        /// </summary>
        public virtual bool Clamp(float x, ref float targetX, float margin)
        {
            if (isOpen)
                return false;
            float leftStop = LeftEdgeX - margin;
            float rightStop = RightEdgeX + margin;
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
            return false;
        }
    }
}
