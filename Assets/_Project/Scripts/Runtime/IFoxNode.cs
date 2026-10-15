using UnityEngine;

namespace ProjectX
{
    /// <summary>A spot the Fox can be sent to and occupy, such as a weight plate.</summary>
    public interface IFoxNode
    {
        Vector3 StandPoint { get; }
        bool IsOccupied { get; }

        /// <summary>The Fox has arrived and settled on the node.</summary>
        void Occupy();

        /// <summary>The Fox has been called away.</summary>
        void Release();
    }
}
