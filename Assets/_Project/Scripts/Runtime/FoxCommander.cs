using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// Turns Rian's Fox button into orders: sends the Fox to the nearest <see cref="IFoxNode"/>,
    /// or calls it back if it is already on one.
    /// </summary>
    [RequireComponent(typeof(FoxCompanion))]
    public class FoxCommander : MonoBehaviour
    {
        [Tooltip("How close to Rian a node must be to send the Fox there.")]
        public float commandRadius = 3f;

        FoxCompanion fox;
        RianInput input;

        void Awake()
        {
            fox = GetComponent<FoxCompanion>();
            input = fox.Leader.GetComponent<RianInput>();
        }

        void OnEnable() => input.FoxPressed += Command;
        void OnDisable() => input.FoxPressed -= Command;

        void Command()
        {
            if (fox.IsBusy)
            {
                fox.Recall();
                return;
            }

            var node = FoxNodes.FindNear(fox.Leader.transform.position, commandRadius);
            if (node != null)
                fox.SendTo(node);
        }
    }
}
