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
        float maxHealth = 100f;

        float current;

        public event Action<DamageInfo> Damaged;
        /// <summary>(現在HP, 最大HP)</summary>
        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float Current => current;
        public float Max => maxHealth;
        public bool IsDead => current <= 0f;

        void Awake()
        {
            current = maxHealth;
        }

        // プールから再利用されるたびにHPを全回復
        void OnEnable()
        {
            current = maxHealth;
        }

        public void TakeDamage(float damage, Vector3 hitPoint)
        {
            if (IsDead || damage <= 0f) return;

            current = Mathf.Max(0f, current - damage);
            Damaged?.Invoke(new DamageInfo(damage, hitPoint));
            HealthChanged?.Invoke(current, maxHealth);
            if (IsDead) Died?.Invoke();
        }
    }
}
