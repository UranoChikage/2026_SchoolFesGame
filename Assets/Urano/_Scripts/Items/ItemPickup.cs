using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 海に浮かぶアイテム。大砲を当てると、当てた砲手が取得する（拾う操作なし）。
    /// その砲手がすでにアイテムを持っていたら取得できず、そのまま残る。
    /// </summary>
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("上下にゆれる幅(m)")]
        float bobHeight = 0.3f;
        [SerializeField]
        float bobSpeed = 2f;

        /// <summary>海に出ているアイテム一覧（画面端アイコンが使う）</summary>
        public static readonly System.Collections.Generic.List<ItemPickup> Active = new System.Collections.Generic.List<ItemPickup>();

        public Color Color => item != null ? item.Color : Color.white;

        ItemAsset item;
        float baseY;

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        // 全ドロップで1つを共有（Prefab未設定時の仮の見た目）
        static ItemPickup fallbackPrefab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => fallbackPrefab = null;

        /// <summary>アイテムを海に出す。prefab未設定なら仮の球。lifetime秒後に消える</summary>
        public static ItemPickup Spawn(ItemPickup prefab, ItemAsset item, Vector3 position, float lifetime)
        {
            ItemPickup p = PoolManager.Spawn(prefab != null ? prefab : FallbackPrefab, position, Quaternion.identity, lifetime);
            p.Setup(item, position.y);
            return p;
        }

        static ItemPickup FallbackPrefab
        {
            get
            {
                if (fallbackPrefab == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "ItemPickup";
                    go.transform.localScale = Vector3.one * 2f; // 当てやすいよう大きめ
                    fallbackPrefab = go.AddComponent<ItemPickup>();
                    go.SetActive(false);
                }
                return fallbackPrefab;
            }
        }

        void Setup(ItemAsset asset, float y)
        {
            item = asset;
            baseY = y;
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = asset.Color;
        }

        void Update()
        {
            Vector3 p = transform.position;
            p.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = p;
        }

        /// <summary>砲弾を当てた砲が取得を試みる。取得できたら消える</summary>
        public bool TryCollect(CannonShooter source)
        {
            if (item == null || source == null) return false;
            ItemHolder holder = ItemHolder.For(source);
            if (!holder.TryAcquire(item)) return false;

            PoolManager.Release(gameObject);
            return true;
        }
    }
}
