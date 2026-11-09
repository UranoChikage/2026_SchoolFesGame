using UnityEngine;

namespace Pirates
{
    /// <summary>敵船に付ける。沈没時にたまにアイテムを海へ落とす。候補リストと確率だけ設定する。</summary>
    [RequireComponent(typeof(ShipHealth))]
    public class ItemDropper : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("ドロップ候補。各アイテムの重みで抽選")]
        ItemAsset[] candidates;
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("沈めたときにアイテムが出る確率（仮置き）")]
        float dropChance = 0.15f;
        [SerializeField]
        [Tooltip("星の数(0〜3)ごとの、出る確率にかける倍率。1=そのまま（掛けたあと1を超えたら必ず出る）")]
        float[] chanceRateByStars = { 1f, 1.3f, 1.7f, 2.5f };
        [SerializeField]
        [Tooltip("アイテムの見た目のPrefab（任意）。ItemPickup付き。未設定なら仮の球")]
        ItemPickup pickupPrefab;
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
            if (Random.value > dropChance * StarRate) return;
            ItemAsset item = Pick();
            if (item == null) return;

            PlayerTarget player = PlayerTarget.Current;
            float y = (player != null && player.Water != null ? player.Water.waterY : transform.position.y) + height;
            Vector3 pos = transform.position;
            ItemPickup.Spawn(pickupPrefab, item, new Vector3(pos.x, y, pos.z), lifetime);
        }

        float StarRate
        {
            get
            {
                if (chanceRateByStars == null || chanceRateByStars.Length == 0) return 1f;
                return Mathf.Max(0f, chanceRateByStars[Mathf.Clamp(TreasureWallet.CurrentStars, 0, chanceRateByStars.Length - 1)]);
            }
        }

        ItemAsset Pick()
        {
            if (candidates == null || candidates.Length == 0) return null;
            float sum = 0f;
            foreach (ItemAsset c in candidates) if (c != null) sum += c.Weight;
            if (sum <= 0f) return null;

            float r = Random.value * sum;
            foreach (ItemAsset c in candidates)
            {
                if (c == null) continue;
                r -= c.Weight;
                if (r <= 0f) return c;
            }
            return candidates[candidates.Length - 1];
        }
    }
}
