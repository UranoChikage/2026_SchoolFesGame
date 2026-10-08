using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 1つの演出（VFX）の定義。Prefabと再生時間をまとめた設定で、Assets/Urano/VFX で一括管理する。
    /// コードからは vfx.Play(位置) で呼ぶ。生成・返却は PoolManager 経由。
    /// Inspector下部のプレビューで再生／停止して見た目を確認できる（Editor拡張）。
    /// </summary>
    [CreateAssetMenu(menuName = "Urano/VFX Asset", fileName = "NewVfx")]
    public class VfxAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("演出のPrefab（ParticleSystemなど）")]
        GameObject prefab;
        [SerializeField]
        [Tooltip("再生してからプールへ自動返却するまでの時間(秒)。プレビューのループ長にも使う")]
        float lifetime = 3f;
        [SerializeField]
        [TextArea]
        [Tooltip("用途のメモ（任意）")]
        string memo;

        public GameObject Prefab => prefab;
        public float Lifetime => lifetime;

        public GameObject Play(Vector3 position) => Play(position, Quaternion.identity);

        /// <summary>指定位置で再生する（プールから取り出し、lifetime秒後に自動返却）</summary>
        public GameObject Play(Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            return PoolManager.Spawn(prefab, position, rotation, lifetime);
        }
    }
}
