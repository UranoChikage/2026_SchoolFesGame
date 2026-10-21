using System;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 船のHP。プレイヤー船にも敵船にも付ける共通のダメージ受け口。
    /// 被弾時の追加処理は Damaged / HealthChanged / Died を購読して増やす。
    /// </summary>
    public class ShipHealth : MonoBehaviour, IDamageable
    {
        [SerializeField]
        [Tooltip("最大HP。0以下にすると無敵になり、HPは減らず沈まないが、ダメージの通知(Damaged)だけ受け取る（岩礁など）")]
        float maxHealth = 100f;

        float current;

        public event Action<DamageInfo> Damaged;
        /// <summary>(現在HP, 最大HP)</summary>
        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float Current => current;
        public float Max => maxHealth;
        /// <summary>最大HPが0以下の無敵状態。ダメージは通知(Damaged)されるだけでHPは減らない</summary>
        public bool IsIndestructible => maxHealth <= 0f;
        public bool IsDead => !IsIndestructible && current <= 0f;
        /// <summary>trueの間はダメージを受けない（船大福バリア用）</summary>
        public bool Invulnerable { get; set; }

        void Awake()
        {
            current = maxHealth;
        }

        // プールから再利用されるたびにHPを全回復
        void OnEnable()
        {
            Invulnerable = false;
            current = maxHealth;
        }

        public void TakeDamage(float damage, Vector3 hitPoint)
        {
            if (IsDead || Invulnerable || damage <= 0f) return;

            if (IsIndestructible)
            {
                Damaged?.Invoke(new DamageInfo(damage, hitPoint));
                return;
            }

            current = Mathf.Max(0f, current - damage);
            Damaged?.Invoke(new DamageInfo(damage, hitPoint));
            HealthChanged?.Invoke(current, maxHealth);
            if (IsDead)
            {
                Debug.Log($"[Sunk] {name} が沈没", this);
                Died?.Invoke();
            }
        }
    }
}
