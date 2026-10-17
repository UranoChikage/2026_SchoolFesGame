using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 敵船に付ける。沈没した瞬間にお宝を重み付き抽選し、金額を自動加算する（拾う操作なし）。
    /// ShipHealth と TreasureWallet は自動取得するので、設定するのは候補リストだけ。
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

        ShipHealth health;

        void Awake() => health = GetComponent<ShipHealth>();

        void OnEnable() => health.Died += OnDied;

        void OnDisable() => health.Died -= OnDied;

        void OnDied()
        {
            if (Random.value > dropChance) return;
            TreasureAsset t = Pick();
            if (t == null || TreasureWallet.Current == null) return;

            TreasureWallet.Current.Add(t);
            t.PlayDropVfx(transform.position);
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
