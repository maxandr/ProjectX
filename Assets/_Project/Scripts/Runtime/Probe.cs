using UnityEngine;

namespace ProjectX
{
    public static class Probe
    {
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            hit = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider is CharacterController || hits[i].distance >= best)
                    continue;
                best = hits[i].distance;
                hit = hits[i];
            }
            return best < float.MaxValue;
        }
    }
}
