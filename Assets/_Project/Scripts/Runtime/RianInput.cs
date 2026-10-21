using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectX
{
    /// <summary>Wraps the "Player" map of ProjectX.inputactions for Rian and the Fox command.
    /// Movement is polled every frame; button presses are also raised as events.</summary>
    public class RianInput : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap map;
        InputAction move, jump, sprint, sacrifice, fox;

        public float MoveX => move?.ReadValue<float>() ?? 0f;
        public bool SprintHeld => sprint?.IsPressed() ?? false;
        public bool JumpPressed => jump?.WasPressedThisFrame() ?? false;
        public bool JumpHeld => jump?.IsPressed() ?? false;
        public bool SacrificeHeld => sacrifice?.IsPressed() ?? false;

        public event Action SacrificePressed;
        public event Action SacrificeReleased;
        public event Action FoxPressed;

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("RianInput: 'actions' is not assigned. Rebuild the scene via ProjectX > Build Proto Blockout.", this);
                enabled = false;
                return;
            }
            map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            jump = map.FindAction("Jump", true);
            sprint = map.FindAction("Sprint", true);
            sacrifice = map.FindAction("Sacrifice", true);
            fox = map.FindAction("Fox", true);
        }

        void OnEnable()
        {
            if (map == null)
                return;
            sacrifice.performed += OnSacrificePerformed;
            sacrifice.canceled += OnSacrificeCanceled;
            fox.performed += OnFoxPerformed;
            map.Enable();
        }

        void OnDisable()
        {
            if (map == null)
                return;
            sacrifice.performed -= OnSacrificePerformed;
            sacrifice.canceled -= OnSacrificeCanceled;
            fox.performed -= OnFoxPerformed;
            map.Disable();
        }

        void OnSacrificePerformed(InputAction.CallbackContext _) => SacrificePressed?.Invoke();
        void OnSacrificeCanceled(InputAction.CallbackContext _) => SacrificeReleased?.Invoke();
        void OnFoxPerformed(InputAction.CallbackContext _) => FoxPressed?.Invoke();
    }
}
