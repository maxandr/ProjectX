using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectX
{
    /// <summary>Debug: mouse wheel speeds up / slows down the whole game, wheel click resets to 1x.</summary>
    public class TimeScaleControl : MonoBehaviour
    {
        [Tooltip("Each wheel notch multiplies or divides the time scale by this.")]
        public float step = 1.25f;
        public float minScale = 0.1f;
        public float maxScale = 4f;

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                SetScale(1f);
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                SetScale(Time.timeScale * (scroll > 0f ? step : 1f / step));
        }

        void SetScale(float scale)
        {
            Time.timeScale = Mathf.Clamp(scale, minScale, maxScale);
            Debug.Log($"Time scale: {Time.timeScale:0.00}x");
        }

        void OnDestroy() => Time.timeScale = 1f;
    }
}
