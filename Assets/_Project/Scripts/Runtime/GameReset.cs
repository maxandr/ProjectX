using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectX
{
    /// <summary>Restarts the whole game by reloading the active scene when the "Reset" action (R / Start) fires.</summary>
    public class GameReset : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputAction reset;

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("GameReset: 'actions' is not assigned.", this);
                enabled = false;
                return;
            }
            reset = actions.FindActionMap("Player", true).FindAction("Reset", true);
        }

        void OnEnable()
        {
            if (reset == null)
                return;
            reset.performed += OnReset;
            reset.Enable();
        }

        void OnDisable()
        {
            if (reset != null)
                reset.performed -= OnReset;
        }

        void OnReset(InputAction.CallbackContext _) => Restart();

        /// <summary>Restarts the whole game from the beginning.</summary>
        public static void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
