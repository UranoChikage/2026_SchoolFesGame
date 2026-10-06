using UnityEngine;

namespace Pirates
{
    /// <summary>砲弾の爆発などでダメージを受けるもの（敵船など）が実装する</summary>
    public interface IDamageable
    {
        void TakeDamage(float damage, Vector3 hitPoint);
    }
}
