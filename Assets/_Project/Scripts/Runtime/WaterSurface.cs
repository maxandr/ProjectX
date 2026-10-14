using System.Collections.Generic;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Marks a water renderer so <see cref="WaterRippleEmitter"/>s know where the surface is.
    /// The surface is the top of the renderer's bounds; water is assumed not to move.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterSurface : MonoBehaviour
    {
        static readonly List<WaterSurface> all = new List<WaterSurface>();

        Bounds bounds;

        public float SurfaceY => bounds.max.y;

        void OnEnable()
        {
            bounds = GetComponent<Renderer>().bounds;
            all.Add(this);
        }

        void OnDisable() => all.Remove(this);

        /// <summary>The surface whose XZ footprint contains <paramref name="position"/>; the highest one if several overlap.</summary>
        public static WaterSurface At(Vector3 position)
        {
            WaterSurface best = null;
            foreach (var water in all)
            {
                var b = water.bounds;
                if (position.x < b.min.x || position.x > b.max.x || position.z < b.min.z || position.z > b.max.z)
                    continue;
                if (best == null || water.SurfaceY > best.SurfaceY)
                    best = water;
            }
            return best;
        }
    }
}
