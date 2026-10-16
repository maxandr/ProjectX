using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Unreal-style "End": drops each selected object straight down until the bottom of its
    /// renderers (or colliders) rests on the first collider below it. Undoable.
    /// </summary>
    static class DropToGround
    {
        const float MaxDistance = 1000f;

        [MenuItem("Tools/Drop To Ground _END")]
        static void Drop()
        {
            Physics.SyncTransforms();
            Transform[] targets = Selection.transforms;
            Undo.RecordObjects(targets, "Drop To Ground");

            foreach (Transform target in targets)
            {
                if (!TryGetBounds(target, out Bounds bounds))
                    bounds = new Bounds(target.position, Vector3.zero);

                var own = new HashSet<Collider>(target.GetComponentsInChildren<Collider>());
                RaycastHit[] hits = Physics.RaycastAll(bounds.center,Vector3.down, MaxDistance, ~0, QueryTriggerInteraction.Ignore);

                RaycastHit? ground = hits
                    .Where(h => !own.Contains(h.collider))
                    .OrderBy(h => h.distance)
                    .Select(h => (RaycastHit?)h)
                    .FirstOrDefault();

                if (ground == null)
                {
                    Debug.LogWarning($"Drop To Ground: nothing below {target.name}", target);
                    continue;
                }

                target.position += Vector3.up * (ground.Value.point.y - bounds.min.y);
            }
        }

        [MenuItem("Tools/Drop To Ground _END", true)]
        static bool CanDrop() => Selection.transforms.Length > 0;

        static bool TryGetBounds(Transform target, out Bounds bounds)
        {
            var renderers = target.GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && !(r is ParticleSystemRenderer))
                .Select(r => r.bounds);
            var colliders = target.GetComponentsInChildren<Collider>()
                .Where(c => c.enabled && !c.isTrigger)
                .Select(c => c.bounds);

            Bounds[] all = renderers.Any() ? renderers.ToArray() : colliders.ToArray();
            bounds = default;
            if (all.Length == 0)
                return false;

            bounds = all[0];
            foreach (Bounds b in all.Skip(1))
                bounds.Encapsulate(b);
            return true;
        }
    }
}
