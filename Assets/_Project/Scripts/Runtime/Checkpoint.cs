using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectX
{
    /// <summary>Trigger that becomes Rian's respawn point when she passes through it.
    /// Debug: keys 1, 2, 3... warp Rian and the Fox to the checkpoints, ordered left to right.</summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        static readonly List<Checkpoint> all = new List<Checkpoint>();
        static Checkpoint current;
        static InputAction warpHotkeys;
        static RianController rian;
        static FoxCompanion fox;

        /// <summary>How many checkpoints are active, i.e. how many warp hotkeys do something.</summary>
        public static int Count => all.Count;

        /// <summary>Raised after Rian and the Fox are teleported (respawn or warp), so puzzles can snap back to rest.</summary>
        public static event Action Teleported;

        void OnEnable()
        {
            all.Add(this);
            if (all.Count == 1)
                EnableHotkeys();
        }

        void OnDisable()
        {
            all.Remove(this);
            if (all.Count == 0)
                warpHotkeys?.Disable();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<RianController>() != null)
                current = this;
        }

        // One action shared by all checkpoints: bindings 0..8 are the digit row, 9..17 the numpad.
        static void EnableHotkeys()
        {
            if (warpHotkeys == null)
            {
                warpHotkeys = new InputAction("Warp To Checkpoint", InputActionType.Button);
                for (int i = 1; i <= 9; i++)
                    warpHotkeys.AddBinding($"<Keyboard>/{i}");
                for (int i = 1; i <= 9; i++)
                    warpHotkeys.AddBinding($"<Keyboard>/numpad{i}");
                warpHotkeys.performed += context =>
                    WarpTo(context.action.GetBindingIndexForControl(context.control) % 9);
            }
            warpHotkeys.Enable();
        }

        static void WarpTo(int index)
        {
            all.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            if (index < 0 || index >= all.Count)
                return;

            if (rian == null)
                rian = FindAnyObjectByType<RianController>();
            current = all[index];
            if (rian != null)
                TeleportBoth(rian, current.transform.position);
            Debug.Log($"Warped to checkpoint {index + 1}: {current.name}");
        }

        /// <summary>Puts Rian and the Fox back at the last checkpoint (or Rian's spawn point).</summary>
        public static void Respawn(RianController rian)
        {
            TeleportBoth(rian, current != null ? current.transform.position : rian.SpawnPoint);
        }

        static void TeleportBoth(RianController rian, Vector3 position)
        {
            if (fox == null)
                fox = FindAnyObjectByType<FoxCompanion>();
            rian.Teleport(position);
            if (fox != null)
                fox.Teleport(position);
            Teleported?.Invoke();
        }
    }
}
