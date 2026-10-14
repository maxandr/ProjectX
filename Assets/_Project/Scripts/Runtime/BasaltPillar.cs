using System;
using UnityEngine;

namespace ProjectX
{
    /// <summary>A hex column whose root sits at its top face; moves vertically with a rumble.</summary>
    public class BasaltPillar : MonoBehaviour
    {
        public float rumbleAmplitude = 0.04f;
        public float rumbleFrequency = 45f;

        Vector3 restPosition;
        Coroutine routine;

        void Awake() => restPosition = transform.position;

        public void MoveTo(float topY, float duration, float delay = 0f, Action onComplete = null)
        {
            if (routine != null)
                StopCoroutine(routine);

            float fromY = transform.position.y;
            routine = Tween.Run(this, duration, t =>
            {
                float shake = Mathf.Sin(Time.time * rumbleFrequency) * rumbleAmplitude * Mathf.Sin(t * Mathf.PI);
                transform.position = new Vector3(restPosition.x + shake, Mathf.Lerp(fromY, topY, t), restPosition.z);
            }, Ease.InOutSine, onComplete, delay);
        }
    }
}
