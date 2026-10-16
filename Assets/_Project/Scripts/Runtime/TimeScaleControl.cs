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

        InputAction scroll, resetScale;

        void Awake()
        {
            scroll = new InputAction("Time Scale", InputActionType.PassThrough, "<Mouse>/scroll/y");
            resetScale = new InputAction("Reset Time Scale", InputActionType.Button, "<Mouse>/middleButton");
        }

        void OnEnable()
        {
            scroll.performed += OnScroll;
            resetScale.performed += OnResetScale;
            scroll.Enable();
            resetScale.Enable();
        }

        void OnDisable()
        {
            scroll.performed -= OnScroll;
            resetScale.performed -= OnResetScale;
            scroll.Disable();
            resetScale.Disable();
        }

        void OnDestroy()
        {
            scroll.Dispose();
            resetScale.Dispose();
            Time.timeScale = 1f;
        }

        void OnScroll(InputAction.CallbackContext context)
        {
            float delta = context.ReadValue<float>();
            if (delta != 0f)
                SetScale(Time.timeScale * (delta > 0f ? step : 1f / step));
        }

        void OnResetScale(InputAction.CallbackContext _) => SetScale(1f);

        void SetScale(float scale)
        {
            Time.timeScale = Mathf.Clamp(scale, minScale, maxScale);
            Debug.Log($"Time scale: {Time.timeScale:0.00}x");
        }
    }
}
