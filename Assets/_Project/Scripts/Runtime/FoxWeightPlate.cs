using System.Collections.Generic;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Pressure plate on top of a counterweight column. While the Fox sits on it the column
    /// sinks and the linked stepping stones rise out of the water.
    /// </summary>
    public class FoxWeightPlate : MonoBehaviour
    {
        static readonly List<FoxWeightPlate> all = new List<FoxWeightPlate>();

        [SerializeField] BasaltPillar counterweight;
        public float pressedDrop = 0.5f;
        [SerializeField] BasaltPillar[] linkedPillars;
        public float raisedTopY = 0f;
        public float moveDuration = 0.8f;
        [Tooltip("Lets the Fox step off before the column rises back up.")]
        public float releaseDelay = 0.4f;

        [Header("Glow")]
        [SerializeField] Renderer glowRenderer;
        [ColorUsage(false, true)] public Color idleGlow = new Color(0.4f, 0.9f, 0.8f) * 1.5f;
        [ColorUsage(false, true)] public Color pressedGlow = new Color(1f, 0.8f, 0.4f) * 4f;

        public bool IsPressed { get; private set; }
        public Vector3 StandPoint => transform.position;

        float counterweightRestY;
        float[] linkedRestY;
        Material glowMaterial;

        void Awake()
        {
            counterweightRestY = counterweight.transform.position.y;
            linkedRestY = new float[linkedPillars.Length];
            for (int i = 0; i < linkedPillars.Length; i++)
                linkedRestY[i] = linkedPillars[i].transform.position.y;
            if (glowRenderer != null)
            {
                glowMaterial = glowRenderer.material;
                glowMaterial.SetColor("_EmissionColor", idleGlow);
            }
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static FoxWeightPlate FindNear(Vector3 position, float radius)
        {
            FoxWeightPlate nearest = null;
            float best = radius;
            foreach (var plate in all)
            {
                float distance = Vector3.Distance(position, plate.StandPoint);
                if (distance <= best)
                {
                    best = distance;
                    nearest = plate;
                }
            }
            return nearest;
        }

        public void Press()
        {
            if (IsPressed)
                return;
            IsPressed = true;
            counterweight.MoveTo(counterweightRestY - pressedDrop, moveDuration);
            foreach (var pillar in linkedPillars)
                pillar.MoveTo(raisedTopY, moveDuration);
            SetGlow(pressedGlow);
        }

        public void Release()
        {
            if (!IsPressed)
                return;
            IsPressed = false;
            counterweight.MoveTo(counterweightRestY, moveDuration, releaseDelay);
            for (int i = 0; i < linkedPillars.Length; i++)
                linkedPillars[i].MoveTo(linkedRestY[i], moveDuration, releaseDelay);
            SetGlow(idleGlow);
        }

        void SetGlow(Color target)
        {
            if (glowMaterial == null)
                return;
            Color from = glowMaterial.GetColor("_EmissionColor");
            Tween.Run(this, moveDuration, t => glowMaterial.SetColor("_EmissionColor", Color.Lerp(from, target, t)));
        }
    }
}
