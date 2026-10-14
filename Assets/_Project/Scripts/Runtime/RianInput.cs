using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectX
{
    /// <summary>Polls the "Player" map of ProjectX.inputactions for Rian and the Fox command.</summary>
    public class RianInput : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap map;
        InputAction move, jump, sacrifice, fox;

        public float MoveX => move?.ReadValue<float>() ?? 0f;
        public bool JumpPressed => jump?.WasPressedThisFrame() ?? false;
        public bool JumpHeld => jump?.IsPressed() ?? false;
        public bool SacrificeHeld => sacrifice?.IsPressed() ?? false;
        public bool FoxPressed => fox?.WasPressedThisFrame() ?? false;

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
            sacrifice = map.FindAction("Sacrifice", true);
            fox = map.FindAction("Fox", true);
        }

        void OnEnable() => map?.Enable();
        void OnDisable() => map?.Disable();
    }
}
