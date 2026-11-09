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
        public GameObject Play(Vector3 position, Quaternion rotation) => Play(position, rotation, Vector3.one);

        /// <summary>
        /// 大きさを指定して再生する。scale はPrefab本来の大きさに対する倍率
        /// （直径1m基準のPrefabなら、直径をそのまま渡せる。ルートParticle SystemのScaling Modeは Hierarchy にする）。
        /// </summary>
        public GameObject Play(Vector3 position, float scale) => Play(position, Quaternion.identity, Vector3.one * scale);

        /// <summary>縦横を分けた倍率で再生する（x,z=直径方向、y=高さ）</summary>
        public GameObject Play(Vector3 position, Vector3 scale) => Play(position, Quaternion.identity, scale);

        /// <summary>
        /// 自動返却せずに出す。呼び出し側が PoolManager.Release で返す。
        /// 出しっぱなしにして位置を追従させたいもの（ビームの当たり演出など）用で、Prefabはループ再生にしておく。
        /// </summary>
        public GameObject Hold(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (prefab == null) return null;
            GameObject instance = PoolManager.Spawn(prefab, position, rotation);
            if (instance != null) instance.transform.localScale = Vector3.Scale(prefab.transform.localScale, scale);
            return instance;
        }

        /// <summary>向きと縦横別の倍率を指定して再生する</summary>
        public GameObject Play(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (prefab == null) return null;
            GameObject instance = PoolManager.Spawn(prefab, position, rotation, lifetime);
            // プール再利用でスケールが残らないよう、毎回Prefab本来の大きさ×倍率に戻す
            if (instance != null) instance.transform.localScale = Vector3.Scale(prefab.transform.localScale, scale);
            return instance;
        }
    }
}
