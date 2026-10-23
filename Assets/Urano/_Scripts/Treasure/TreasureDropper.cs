using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 敵船に付ける。沈没した瞬間にお宝を重み付き抽選し、海に宝箱(TreasurePickup)として出す。
    /// 大砲かビームを当てると獲得できる。ShipHealth は自動取得するので、設定するのは候補リストくらい。
    /// </summary>
    [RequireComponent(typeof(ShipHealth))]
    public class TreasureDropper : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("ドロップ候補。各お宝の重みで抽選")]
        TreasureAsset[] candidates;
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("沈めたときにお宝が出る確率")]
        float dropChance = 1f;
        [SerializeField]
        [Tooltip("宝箱の見た目のPrefab（任意）。TreasurePickup付き。未設定なら仮の立方体")]
        TreasurePickup pickupPrefab;
        [SerializeField]
        [Tooltip("出てから消えるまでの時間(秒)")]
        float lifetime = 20f;
        [SerializeField]
        [Tooltip("水面からの高さ(m)")]
        float height = 1f;

        ShipHealth health;

        void Awake() => health = GetComponent<ShipHealth>();

        void OnEnable() => health.Died += OnDied;

        void OnDisable() => health.Died -= OnDied;

        void OnDied()
        {
            if (Random.value > dropChance) return;
            TreasureAsset t = Pick();
            if (t == null) return;

            PlayerTarget player = PlayerTarget.Current;
            float y = (player != null && player.Water != null ? player.Water.waterY : transform.position.y) + height;
            Vector3 pos = transform.position;
            TreasurePickup.Spawn(pickupPrefab, t, new Vector3(pos.x, y, pos.z), lifetime);
        }

        TreasureAsset Pick()
        {
            if (candidates == null || candidates.Length == 0) return null;
            float sum = 0f;
            foreach (TreasureAsset c in candidates) if (c != null) sum += c.Weight;
            if (sum <= 0f) return null;

            float r = Random.value * sum;
            foreach (TreasureAsset c in candidates)
            {
                if (c == null) continue;
                r -= c.Weight;
                if (r <= 0f) return c;
            }
            return candidates[candidates.Length - 1];
        }
    }
}
