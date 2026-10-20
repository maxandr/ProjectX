using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectX
{
    /// <summary>
    /// Debug: F hands control to the Fox. A/D (arrows, left stick) walk it while Rian stands still and the camera
    /// follows the Fox; Space jumps, and it still hops gaps and steps on its own. F again gives control back to Rian.
    /// Works as an <see cref="IFoxNode"/> that stays a few metres ahead of the Fox in the held direction.
    /// </summary>
    [RequireComponent(typeof(FoxCompanion))]
    public class FoxPossession : MonoBehaviour, IFoxNode
    {
        [Tooltip("How far ahead of the Fox the target sits while a direction is held; beyond ~6 m it runs.")]
        public float leadDistance = 6f;
        [Tooltip("How far a jump carries the Fox while a direction is held; standing still it hops in place.")]
        public float jumpDistance = 2f;

        FoxCompanion fox;
        RianInput rianInput;
        CinemachineCamera cam;
        Transform rianTarget;
        InputAction toggle, move, jump;
        bool possessed;

        public Vector3 StandPoint => fox.transform.position + Vector3.right * (move.ReadValue<float>() * leadDistance);
        public bool IsOccupied => false;
        public void Occupy() { }
        public void Release() { }

        void Awake()
        {
            fox = GetComponent<FoxCompanion>();
            rianInput = fox.Leader.GetComponent<RianInput>();
            toggle = new InputAction("Possess Fox", InputActionType.Button, "<Keyboard>/f");
            move = new InputAction("Fox Move", InputActionType.Value);
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick/x");
            jump = new InputAction("Fox Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable()
        {
            toggle.performed += OnToggle;
            move.performed += OnMove;
            jump.performed += OnJump;
            toggle.Enable();
        }

        void OnDisable()
        {
            toggle.performed -= OnToggle;
            move.performed -= OnMove;
            jump.performed -= OnJump;
            toggle.Disable();
            if (possessed)
                SetPossessed(false);
        }

        void OnDestroy()
        {
            toggle.Dispose();
            move.Dispose();
            jump.Dispose();
        }

        void OnToggle(InputAction.CallbackContext _) => SetPossessed(!possessed);

        // A respawn teleport recalls the Fox; take it back on the next step.
        void OnMove(InputAction.CallbackContext _)
        {
            if (!fox.IsBusy)
                fox.SendTo(this);
        }

        // Lands jumpDistance ahead on whatever ground is there, or at take-off height over a gap.
        void OnJump(InputAction.CallbackContext _)
        {
            if (!fox.IsGrounded)
                return;
            Vector3 p = fox.transform.position;
            float dir = move.ReadValue<float>();
            float x = p.x + (Mathf.Abs(dir) > 0.1f ? Mathf.Sign(dir) * jumpDistance : 0f);
            Vector3 landing = PlayPath.Snap(new Vector3(x, p.y, 0f));
            float scan = fox.maxStepUp + 0.2f;
            if (Probe.Raycast(landing + Vector3.up * scan, Vector3.down, scan + fox.maxHopDown, out var ground))
                landing.y = ground.point.y;
            fox.JumpTo(landing);
        }

        void SetPossessed(bool on)
        {
            possessed = on;
            rianInput.enabled = !on;
            if (cam == null)
                cam = FindAnyObjectByType<CinemachineCamera>();

            if (on)
            {
                move.Enable();
                jump.Enable();
                fox.SendTo(this);
                if (cam != null)
                {
                    rianTarget = cam.Target.TrackingTarget;
                    cam.Target.TrackingTarget = fox.transform;
                }
            }
            else
            {
                move.Disable();
                jump.Disable();
                fox.Recall();
                if (cam != null && rianTarget != null)
                    cam.Target.TrackingTarget = rianTarget;
            }
        }
    }
}
