using UnityEngine;
using UnityEngine.Events;

namespace ProjectX
{
    /// <summary>
    /// The Vitality Sacrifice. Rian holds Sacrifice inside the trigger for holdDuration, then
    /// simultaneously: the bridge rises, Rian is drained, the Fox grows and the world desaturates.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SacrificeNode : MonoBehaviour
    {
        public float holdDuration = 1.5f;

        [Header("Bridge")]
        [SerializeField] BasaltPillar[] bridgePillars;
        public float bridgeTopY = 0f;
        public float bridgeRiseDuration = 2f;
        [SerializeField] DeepWaterZone water;

        [Header("Consequences")]
        [SerializeField] RianVitality vitality;
        [SerializeField] FoxGrowth foxGrowth;
        [SerializeField] PaletteDrain paletteDrain;
        public float saturationDelta = -40f;

        [Header("Feedback")]
        [SerializeField] Renderer glowRenderer;
        [ColorUsage(false, true)] public Color glowColor = new Color(1f, 0.55f, 0.2f) * 6f;
        [SerializeField] Light beamLight;
        public float beamLightBoost = 2.5f;

        public UnityEvent onSacrifice;

        RianInput rianInput;
        Material glowMaterial;
        float holdTimer;
        float glow;
        bool fired;

        void Awake()
        {
            if (glowRenderer != null)
            {
                glowMaterial = glowRenderer.material;
                glowMaterial.EnableKeyword("_EMISSION");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RianInput input))
                rianInput = input;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<RianInput>() == rianInput)
                rianInput = null;
        }

        void Update()
        {
            if (!fired)
            {
                bool holding = rianInput != null && rianInput.SacrificeHeld;
                holdTimer = holding ? holdTimer + Time.deltaTime : 0f;
                if (holdTimer >= holdDuration)
                    Fire();
            }

            float targetGlow = fired ? 0.15f : holdTimer / holdDuration;
            glow = Mathf.MoveTowards(glow, targetGlow, Time.deltaTime * (fired ? 0.5f : 3f));
            if (glowMaterial != null)
                glowMaterial.SetColor("_EmissionColor", glowColor * glow);
        }

        void Fire()
        {
            fired = true;
            glow = 1.5f; // flash

            int remaining = bridgePillars.Length;
            foreach (var pillar in bridgePillars)
            {
                pillar.MoveTo(bridgeTopY, bridgeRiseDuration, 0f, () =>
                {
                    if (--remaining == 0)
                        water.isOpen = true;
                });
            }

            vitality.Drain();
            foxGrowth.Begin(water.LeftEdgeX, water.RightEdgeX);
            paletteDrain.Drain(saturationDelta);

            if (beamLight != null)
            {
                float from = beamLight.intensity;
                Tween.Run(this, bridgeRiseDuration, t => beamLight.intensity = from * (1f + (beamLightBoost - 1f) * Mathf.Sin(t * Mathf.PI)));
            }

            onSacrifice?.Invoke();
        }
    }
}
