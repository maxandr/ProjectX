using System;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Disturbs the water around this transform's pivot (a character's feet): a splash when it enters the water,
    /// and a wake of small drops while it wades. Teleports (respawns) are ignored.
    /// Strength, size and spacing are scene-wide settings on <see cref="WaterRipples"/>.
    /// </summary>
    public class WaterRippleEmitter : MonoBehaviour
    {
        [Tooltip("Multiplies the size and height of this character's ripples (e.g. smaller for the Fox).")]
        public float scale = 1f;

        /// <summary>Raised when the pivot enters the water, with the fall speed (m/s, downward positive).</summary>
        public event Action<float> Splashed;

        Vector3 lastPosition;
        float travelled;
        bool wasInWater;

        void OnEnable()
        {
            lastPosition = transform.position;
            wasInWater = false;
        }

        void LateUpdate()
        {
            var position = transform.position;
            var delta = position - lastPosition;
            lastPosition = position;
            var ripples = WaterRipples.Active;
            if (ripples == null || delta.sqrMagnitude > 4f)
            {
                wasInWater = false;
                return;
            }

            var water = WaterSurface.At(position);
            bool inWater = water != null
                && position.y < water.SurfaceY + ripples.contactHeight
                && position.y > water.SurfaceY - ripples.wadeDepth;
            if (!inWater)
            {
                wasInWater = false;
                return;
            }

            var surfacePoint = new Vector3(position.x, water.SurfaceY, position.z);
            if (!wasInWater)
            {
                float fallSpeed = Time.deltaTime > 0f ? -delta.y / Time.deltaTime : 0f;
                float strength = Mathf.Clamp(fallSpeed / ripples.splashSpeed, 0.3f, 1.5f);
                WaterRipples.AddDrop(surfacePoint, ripples.splashRadius * scale, -ripples.splashHeight * scale * strength);
                Splashed?.Invoke(fallSpeed);
                travelled = 0f;
            }
            wasInWater = true;

            travelled += new Vector2(delta.x, delta.z).magnitude;
            if (travelled >= ripples.wakeSpacing)
            {
                travelled %= ripples.wakeSpacing;
                WaterRipples.AddDrop(surfacePoint, ripples.wakeRadius * scale, -ripples.wakeHeight * scale);
            }
        }
    }
}
