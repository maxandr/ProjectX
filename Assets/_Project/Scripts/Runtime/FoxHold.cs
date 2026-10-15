using UnityEngine;

namespace ProjectX
{
    /// <summary>
    /// A <see cref="FoxBarrier"/> that keeps the following Fox back, e.g. in front of a puzzle,
    /// but lets it through while it is on an order (sent to an <see cref="IFoxNode"/>).
    /// </summary>
    public class FoxHold : FoxBarrier
    {
        [SerializeField] FoxCompanion fox;

        public override bool Clamp(float x, ref float targetX, float margin)
        {
            return !fox.IsBusy && base.Clamp(x, ref targetX, margin);
        }
    }
}
