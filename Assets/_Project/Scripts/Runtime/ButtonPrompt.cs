using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A glowing key hint over something Rian can use (the key icon on a line down to the object). Fades in on
    /// <see cref="Show"/> and out on <see cref="Hide"/>; it doesn't know when, a <see cref="PromptZone"/> tells it.
    /// The cards use <c>ProjectX/PromptGlow</c>, which turns them to the camera and pulses the glow.
    /// </summary>
    public class ButtonPrompt : MonoBehaviour
    {
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        [SerializeField] Renderer[] cards;
        [Tooltip("How long the fade in or out takes, in milliseconds.")]
        public float fadeTimeMs = 400f;

        Material[] materials;
        Coroutine fade;
        float alpha;

        void Awake()
        {
            materials = new Material[cards.Length];
            for (int i = 0; i < cards.Length; i++)
                materials[i] = cards[i].material;
            SetAlpha(0f);
        }

        public void Show() => FadeTo(1f);
        public void Hide() => FadeTo(0f);

        void FadeTo(float target)
        {
            if (fade != null)
                StopCoroutine(fade);
            fade = null;
            if (!isActiveAndEnabled)
            {
                SetAlpha(target);
                return;
            }
            float from = alpha;
            float duration = fadeTimeMs / 1000f * Mathf.Abs(target - from);
            fade = Tween.Run(this, duration, t => SetAlpha(Mathf.Lerp(from, target, t)), Ease.InOutSine, () => fade = null);
        }

        void SetAlpha(float value)
        {
            alpha = value;
            foreach (var material in materials)
                material.SetFloat(AlphaId, value);
            foreach (var card in cards)
                card.enabled = value > 0f;
        }
    }
}
