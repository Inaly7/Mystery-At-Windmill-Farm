using UnityEngine;

namespace EggRescue
{
    /// <summary>
    /// 放在 Resources 里，让正式包能引用到特效预制体。
    /// </summary>
    public sealed class VfxCatalog : ScriptableObject
    {
        public GameObject[] Prefabs;
        public Material DustMaterial;
        public Material SmokeMaterial;
    }
}
