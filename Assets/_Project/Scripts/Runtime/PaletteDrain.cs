using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectX
{
    /// <summary>Drains world colour by lowering ColorAdjustments.saturation on a global Volume.</summary>
    public class PaletteDrain : MonoBehaviour
    {
        [SerializeField] Volume volume;
        public float duration = 3f;

        ColorAdjustments adjustments;

        void Awake()
        {
            // Volume.profile returns a runtime copy, so the profile asset is never modified.
            var profile = volume.profile;
            if (!profile.TryGet(out adjustments))
                adjustments = profile.Add<ColorAdjustments>();
            adjustments.saturation.overrideState = true;
        }

        public void Drain(float delta)
        {
            float from = adjustments.saturation.value;
            float to = Mathf.Clamp(from + delta, -100f, 100f);
            Tween.Run(this, duration, t => adjustments.saturation.value = Mathf.Lerp(from, to, t), Ease.InOutSine);
        }
    }
}
