using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A chasm or other fall hazard: Rian entering the trigger volume stops, the screen fades out,
    /// and she respawns at the last checkpoint behind the fade (<see cref="ScreenFade"/>).
    /// Blocking the Fox is a separate <see cref="FoxBarrier"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DeathZone : MonoBehaviour
    {
        bool respawning;

        void OnTriggerEnter(Collider other)
        {
            if (respawning || !other.TryGetComponent(out RianController rian))
                return;

            respawning = true;
            rian.enabled = false; // freeze her where she fell
            ScreenFade.Cover(
                covered: () => Checkpoint.Respawn(rian),
                revealing: () =>
                {
                    rian.enabled = true;
                    respawning = false;
                });
        }
    }
}
