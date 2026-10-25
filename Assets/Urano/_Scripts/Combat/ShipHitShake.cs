using UnityEngine;

namespace Pirates
{
    /// <summary>被弾したら船体を揺らすAnimationを再生する（Animatorのトリガーを引く）。Damagedイベントの購読者の一つ</summary>
    [RequireComponent(typeof(ShipHealth))]
    public class ShipHitShake : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("揺れるAnimator（船体の見た目側）。未設定なら子から自動で探す。BoatControllerの傾きと干渉しないよう、見た目用の子に付ける")]
        Animator animator;
        [SerializeField]
        [Tooltip("Animatorのトリガー名")]
        string triggerName = "Hit";

        ShipHealth health;
        int triggerHash;

        void Awake()
        {
            health = GetComponent<ShipHealth>();
            triggerHash = Animator.StringToHash(triggerName);
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(DamageInfo info)
        {
            if (animator == null) return;
            animator.ResetTrigger(triggerHash); // 連続被弾でもトリガーが溜まらず、毎回頭から揺れる
            animator.SetTrigger(triggerHash);
        }
    }
}
