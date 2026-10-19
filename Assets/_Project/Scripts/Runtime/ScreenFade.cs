using System;
using System.Collections;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Full-screen fade for transitions such as the respawn: fades to <see cref="color"/>, runs a callback
    /// while the screen is covered, holds, then fades back. Draws only while a fade is running.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        public Color color = Color.black;
        [Min(0f)] public float fadeOutTime = 0.4f;
        [Tooltip("Seconds the screen stays covered after the callback, e.g. while the camera catches up.")]
        [Min(0f)] public float holdTime = 0.3f;
        [Min(0f)] public float fadeInTime = 0.5f;

        static ScreenFade instance;
        static bool coveredAcrossLoad; // set by FadeOut: the next ScreenFade to wake starts covered and fades in

        float alpha;
        Coroutine running;

        void Awake()
        {
            instance = this;
            if (coveredAcrossLoad)
            {
                coveredAcrossLoad = false;
                alpha = 1f;
                running = StartCoroutine(Reveal());
                return;
            }
            enabled = false; // OnGUI runs only while fading
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// Fades out, calls <paramref name="covered"/> once the screen is fully covered, then fades back in,
        /// calling <paramref name="revealing"/> as the fade-in starts. Without a ScreenFade in the scene both run at once.
        /// </summary>
        public static void Cover(Action covered, Action revealing = null)
        {
            if (instance == null)
            {
                covered?.Invoke();
                revealing?.Invoke();
                return;
            }

            instance.enabled = true;
            if (instance.running != null)
                instance.StopCoroutine(instance.running);
            instance.running = instance.StartCoroutine(instance.Sequence(covered, revealing));
        }

        /// <summary>
        /// Fades out over <paramref name="duration"/> seconds, calls <paramref name="covered"/> and stays covered,
        /// e.g. before a scene load; the ScreenFade of the next loaded scene then fades in from black.
        /// Without a ScreenFade in the scene the callback runs at once.
        /// </summary>
        public static void FadeOut(float duration, Action covered)
        {
            if (instance == null)
            {
                covered?.Invoke();
                return;
            }

            instance.enabled = true;
            if (instance.running != null)
                instance.StopCoroutine(instance.running);
            instance.running = instance.StartCoroutine(instance.FadeOutSequence(duration, covered));
        }

        IEnumerator Sequence(Action covered, Action revealing)
        {
            yield return FadeTo(1f, fadeOutTime);
            covered?.Invoke();
            if (holdTime > 0f)
                yield return new WaitForSeconds(holdTime);
            revealing?.Invoke();
            yield return Reveal();
        }

        IEnumerator FadeOutSequence(float duration, Action covered)
        {
            yield return FadeTo(1f, duration);
            coveredAcrossLoad = true;
            covered?.Invoke();
        }

        IEnumerator Reveal()
        {
            yield return FadeTo(0f, fadeInTime);
            running = null;
            enabled = false;
        }

        IEnumerator FadeTo(float target, float duration)
        {
            float start = alpha;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                alpha = Mathf.Lerp(start, target, Ease.InOutSine(t / duration));
                yield return null;
            }
            alpha = target;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || alpha <= 0f)
                return;

            GUI.depth = -1000; // above other IMGUI overlays
            var tint = color;
            tint.a *= alpha;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
