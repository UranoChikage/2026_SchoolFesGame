using UnityEngine;

namespace Pirates
{
    /// <summary>被弾したら船を減速させる。Damagedイベントの購読者の一つ（プレイヤー・敵共通）</summary>
    [RequireComponent(typeof(ShipHealth))]
    public class ShipSlowOnDamage : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("被弾中の最高速度の倍率")]
        [Range(0.1f, 1f)] float speedMultiplier = 0.5f;
        [SerializeField]
        [Tooltip("減速が続く時間(秒)")]
        float duration = 2.5f;

        ShipHealth health;
        ISlowable slowable;

        void Awake()
        {
            health = GetComponent<ShipHealth>();
            slowable = GetComponent<ISlowable>();
        }

        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(DamageInfo info)
        {
            slowable?.ApplySlow(speedMultiplier, duration);
        }
    }
}
