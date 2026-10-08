using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 全 VfxAsset の一覧表。名前で探せる。Inspectorのボタンでプロジェクト内の VfxAsset を自動収集できる。
    /// </summary>
    [CreateAssetMenu(menuName = "Urano/VFX Library", fileName = "VfxLibrary")]
    public class VfxLibrary : ScriptableObject
    {
        [SerializeField]
        List<VfxAsset> entries = new List<VfxAsset>();

        public IReadOnlyList<VfxAsset> Entries => entries;

        /// <summary>アセット名で探す。無ければnull</summary>
        public VfxAsset Find(string vfxName)
        {
            foreach (VfxAsset v in entries)
            {
                if (v != null && v.name == vfxName) return v;
            }
            return null;
        }
    }
}
