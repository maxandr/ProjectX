using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("water")] [SerializeField] FoxBarrier chasm;

        [Header("Consequences")]
        [SerializeField] RianVitality vitality;
        [SerializeField] FoxGrowth foxGrowth;
        [Tooltip("Rian's jump force is multiplied by this.")]
        [Range(0f, 1f)] public float jumpForceMultiplier = 1f;
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
        Coroutine holdRoutine, glowRoutine;
        float glow;
        bool fired;

        void Awake()
        {
            if (glowRenderer != null)
            {
                glowMaterial = glowRenderer.material;
                glowMaterial.EnableKeyword("_EMISSION");
            }
            SetGlow(0f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (fired || rianInput != null || !other.TryGetComponent(out RianInput input))
                return;

            rianInput = input;
            rianInput.SacrificePressed += BeginHold;
            rianInput.SacrificeReleased += CancelHold;
            if (rianInput.SacrificeHeld)
                BeginHold();
        }

        void OnTriggerExit(Collider other)
        {
            if (rianInput != null && other.GetComponent<RianInput>() == rianInput)
                Release();
        }

        void OnDisable() => Release();

        void Release()
        {
            if (rianInput == null)
                return;
            rianInput.SacrificePressed -= BeginHold;
            rianInput.SacrificeReleased -= CancelHold;
            rianInput = null;
            CancelHold();
        }

        void BeginHold()
        {
            if (fired || holdRoutine != null)
                return;
            StopGlow();
            holdRoutine = StartCoroutine(Hold());
        }

        void CancelHold()
        {
            if (holdRoutine == null)
                return;
            StopCoroutine(holdRoutine);
            holdRoutine = null;
            if (isActiveAndEnabled)
                glowRoutine = StartCoroutine(GlowTo(0f, 3f));
        }

        IEnumerator Hold()
        {
            for (float timer = 0f; timer < holdDuration; timer += Time.deltaTime)
            {
                SetGlow(Mathf.MoveTowards(glow, timer / holdDuration, Time.deltaTime * 3f));
                yield return null;
            }
            holdRoutine = null;
            Release();
            Fire();
        }

        IEnumerator GlowTo(float target, float speed)
        {
            while (!Mathf.Approximately(glow, target))
            {
                SetGlow(Mathf.MoveTowards(glow, target, Time.deltaTime * speed));
                yield return null;
            }
            glowRoutine = null;
        }

        void StopGlow()
        {
            if (glowRoutine == null)
                return;
            StopCoroutine(glowRoutine);
            glowRoutine = null;
        }

        void SetGlow(float value)
        {
            glow = value;
            if (glowMaterial != null)
                glowMaterial.SetColor("_EmissionColor", glowColor * glow);
        }
        void Fire()
        {
            fired = true;
            SetGlow(1.5f); // flash
            glowRoutine = StartCoroutine(GlowTo(0.15f, 0.5f));

            int remaining = bridgePillars.Length;
            foreach (var pillar in bridgePillars)
            {
                pillar.MoveTo(bridgeTopY, bridgeRiseDuration, 0f, () =>
                {
                    if (--remaining == 0)
                        chasm.isOpen = true;
                });
            }

            vitality.Drain(jumpForceMultiplier);
            foxGrowth.Begin(chasm.LeftEdgeX, chasm.RightEdgeX);
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
