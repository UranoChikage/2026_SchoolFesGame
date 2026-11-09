using System;
using UnityEngine;

namespace Pirates
{
    /// <summary>ダメージを受けた内容。Damagedイベントで配られる</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 HitPoint;

        public DamageInfo(float amount, Vector3 hitPoint)
        {
            Amount = amount;
            HitPoint = hitPoint;
        }
    }

    /// <summary>砲弾の爆発などでダメージを受けるもの（プレイヤー船・敵船）が実装する</summary>
    public interface IDamageable
    {
        /// <summary>ダメージを受けるたびに通知される。減速・演出・スコアなどの処理はここに購読して増やす</summary>
        event Action<DamageInfo> Damaged;

        void TakeDamage(float damage, Vector3 hitPoint);
    }

    /// <summary>外から速度を落とされる船（被弾時の減速用）</summary>
    public interface ISlowable
    {
        /// <summary>duration秒のあいだ最高速度をspeedMultiplier倍にする（重ねがけは強いほう優先で時間は延長）</summary>
        void ApplySlow(float speedMultiplier, float duration);
    }
}
