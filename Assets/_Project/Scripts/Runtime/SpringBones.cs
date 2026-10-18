using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Swings chains of bones that the Animator does not drive (poncho, skirt, hair): each bone's tip is a
    /// verlet point that springs back toward the bone's rest pose, falls under gravity and is kept out of the
    /// <see cref="SpringBoneShape"/>s. Linked chains also keep neighbouring tips together, so the cloth between
    /// them doesn't stretch. Runs after the Animator and after <see cref="RianAnimator"/> turns the body.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class SpringBones : MonoBehaviour
    {
        [System.Serializable]
        class Chain
        {
            [Tooltip("Only a label for the list.")]
            public string name;
            [Tooltip("First bone; the chain follows the first child down to a bone without children.")]
            public Transform root;
            [Tooltip("Percent of the way back to the rest pose per 1/60 s (0 = none, 100 = rigid).")]
            [Range(0f, 100f)] public float stiffnessPercent = 8f;
            [Tooltip("Percent of the velocity lost per 1/60 s.")]
            [Range(0f, 100f)] public float dampingPercent = 15f;
        }

        [Tooltip("With Link Chains, list them in order around the cloth.")]
        [SerializeField] Chain[] chains;
        [SerializeField] SpringBoneShape[] colliders;

        [Tooltip("m/s².")]
        public Vector3 gravity = new(0f, -2f, 0f);
        [Tooltip("Gap the tips keep from the colliders, in cm.")]
        public float gapCm = 2f;
        [Tooltip("A tip further than this many bone lengths from its rest spot snaps back (respawn teleport).")]
        public float snapDistance = 3f;

        [Tooltip("Ties each chain to the next one in Chains (the last to the first), like one piece of cloth.")]
        public bool linkChains;
        [Tooltip("How far, in percent, the cloth between linked chains may stretch.")]
        public float linkSlackPercent = 5f;

        class Bone
        {
            public Transform transform;
            public Quaternion restLocalRotation;
            public Vector3 localTip;
            public float length;
            public float linkLength;
            public Vector3 tip, previousTip;
            public Vector3 origin, restTip;
        }

        readonly List<Bone[]> bones = new();
        int depth;

        void Awake()
        {
            foreach (Chain settings in chains)
            {
                var chain = new List<Bone>();
                for (Transform bone = settings.root; bone.childCount > 0; bone = bone.GetChild(0))
                {
                    Transform child = bone.GetChild(0);
                    chain.Add(new Bone
                    {
                        transform = bone,
                        restLocalRotation = bone.localRotation,
                        localTip = child.localPosition,
                        length = Vector3.Distance(bone.position, child.position),
                    });
                }
                bones.Add(chain.ToArray());
            }
            depth = bones.Select(chain => chain.Length).DefaultIfEmpty().Max();

            for (int i = 0; i < bones.Count; i++)
                for (int d = 0; d < depth; d++)
                    if (TryGetLinked(i, d, out Bone a, out Bone b))
                        a.linkLength = Vector3.Distance(a.transform.GetChild(0).position, b.transform.GetChild(0).position);
        }

        void OnEnable()
        {
            foreach (Bone bone in bones.SelectMany(chain => chain))
            {
                bone.transform.localRotation = bone.restLocalRotation;
                bone.tip = bone.previousTip = bone.transform.TransformPoint(bone.localTip);
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            float frames = dt * 60f;
            float gap = gapCm / 100f;
            Vector3 fall = gravity * (dt * dt);

            // One depth at a time, so each bone starts from its parent's new pose and neighbours can be linked.
            for (int d = 0; d < depth; d++)
            {
                for (int i = 0; i < bones.Count; i++)
                    if (d < bones[i].Length)
                    {
                        float keep = Mathf.Pow(1f - chains[i].dampingPercent / 100f, frames);
                        float pull = 1f - Mathf.Pow(1f - chains[i].stiffnessPercent / 100f, frames);
                        Move(bones[i][d], keep, pull, fall, gap);
                    }

                if (linkChains)
                    for (int i = 0; i < bones.Count; i++)
                        if (TryGetLinked(i, d, out Bone a, out Bone b))
                            Link(a, b);

                foreach (Bone[] chain in bones)
                    if (d < chain.Length)
                        Turn(chain[d]);
            }
        }

        void Move(Bone bone, float keep, float pull, Vector3 fall, float gap)
        {
            Transform t = bone.transform;
            t.localRotation = bone.restLocalRotation;
            bone.origin = t.position;
            bone.restTip = t.TransformPoint(bone.localTip);

            if ((bone.restTip - bone.tip).sqrMagnitude > Sqr(bone.length * snapDistance))
                bone.tip = bone.previousTip = bone.restTip;

            Vector3 next = bone.tip + (bone.tip - bone.previousTip) * keep + (bone.restTip - bone.tip) * pull + fall;
            next = OnLength(bone, next);
            foreach (SpringBoneShape collider in colliders)
                next = collider.PushOut(next, gap);

            bone.previousTip = bone.tip;
            bone.tip = next;
        }

        // Pulls two neighbouring tips together when the cloth between them would stretch past the slack.
        void Link(Bone a, Bone b)
        {
            Vector3 between = b.tip - a.tip;
            float distance = between.magnitude;
            float limit = a.linkLength * (1f + linkSlackPercent / 100f);
            if (distance <= limit)
                return;
            Vector3 fix = between * ((distance - limit) / distance * 0.5f);
            a.tip += fix;
            b.tip -= fix;
        }

        void Turn(Bone bone)
        {
            bone.tip = OnLength(bone, bone.tip);
            bone.transform.rotation = Quaternion.FromToRotation(bone.restTip - bone.origin, bone.tip - bone.origin) * bone.transform.rotation;
        }

        bool TryGetLinked(int chain, int d, out Bone a, out Bone b)
        {
            Bone[] next = bones[(chain + 1) % bones.Count];
            bool linked = bones.Count > 1 && (bones.Count > 2 || chain == 0) && d < bones[chain].Length && d < next.Length;
            a = linked ? bones[chain][d] : null;
            b = linked ? next[d] : null;
            return linked;
        }

        static Vector3 OnLength(Bone bone, Vector3 point) => bone.origin + (point - bone.origin).normalized * bone.length;

        static float Sqr(float x) => x * x;
    }
}
