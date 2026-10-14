using System;
using System.Collections;
using UnityEngine;

namespace ProjectX
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float InQuad(float t) => t * t;
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
    }

    /// <summary>Minimal coroutine tweening; step receives the eased 0..1 progress.</summary>
    public static class Tween
    {
        public static Coroutine Run(MonoBehaviour host, float duration, Action<float> step,
            Func<float, float> ease = null, Action onComplete = null, float delay = 0f)
        {
            return host.StartCoroutine(Routine(duration, step, ease ?? Ease.Linear, onComplete, delay));
        }

        static IEnumerator Routine(float duration, Action<float> step, Func<float, float> ease, Action onComplete, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                step(ease(t / duration));
                yield return null;
            }

            step(1f);
            onComplete?.Invoke();
        }
    }
}
