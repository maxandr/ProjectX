using System.Collections.Generic;
using UnityEngine;

namespace ProjectX
{
    /// <summary>Registry of the active <see cref="IFoxNode"/>s in the scene.</summary>
    public static class FoxNodes
    {
        static readonly List<IFoxNode> all = new List<IFoxNode>();

        public static void Register(IFoxNode node) => all.Add(node);
        public static void Unregister(IFoxNode node) => all.Remove(node);

        /// <summary>The node closest to <paramref name="position"/> within <paramref name="radius"/>, or null.</summary>
        public static IFoxNode FindNear(Vector3 position, float radius)
        {
            IFoxNode nearest = null;
            float best = radius;
            foreach (var node in all)
            {
                float distance = Vector3.Distance(position, node.StandPoint);
                if (distance <= best)
                {
                    best = distance;
                    nearest = node;
                }
            }
            return nearest;
        }
    }
}
