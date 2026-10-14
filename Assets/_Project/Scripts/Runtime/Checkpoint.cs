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
        static int lastHotkeyFrame = -1;
        static RianController rian;
        static FoxCompanion fox;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<RianController>() != null)
                current = this;
        }

        void Update()
        {
            // Every checkpoint runs Update; only the first one each frame polls the keys.
            if (lastHotkeyFrame == Time.frameCount)
                return;
            lastHotkeyFrame = Time.frameCount;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            for (int i = 0; i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    WarpTo(i);
                    return;
                }
            }
        }

        static void WarpTo(int index)
        {
            all.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            if (index >= all.Count)
                return;

            if (rian == null)
                rian = FindAnyObjectByType<RianController>();
            if (fox == null)
                fox = FindAnyObjectByType<FoxCompanion>();

            current = all[index];
            Vector3 position = current.transform.position;
            if (rian != null)
                rian.Teleport(position);
            if (fox != null)
                fox.Teleport(position);
            Debug.Log($"Warped to checkpoint {index + 1}: {current.name}");
        }

        public static void Respawn(RianController rian)
        {
            rian.Teleport(current != null ? current.transform.position : rian.SpawnPoint);
        }
    }
}
