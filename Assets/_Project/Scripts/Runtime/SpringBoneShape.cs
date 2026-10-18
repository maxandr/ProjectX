using UnityEngine;

namespace ProjectX
{
    /// <summary>Something <see cref="SpringBones"/> keep their tips out of. Not a physics collider.</summary>
    public abstract class SpringBoneShape : MonoBehaviour
    {
        /// <summary>Moves <paramref name="point"/> out of the shape, keeping a gap of <paramref name="margin"/>.</summary>
        public abstract Vector3 PushOut(Vector3 point, float margin);
    }
}
