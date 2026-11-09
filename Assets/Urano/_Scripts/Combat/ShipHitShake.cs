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

        [SerializeField]
        [Tooltip("揺れのステート名。入っていて存在すれば、トランジションを通さずこのステートを頭から直接再生する（遷移のラグが出ない）。空ならトリガー方式")]
        string stateName = "ShipShake";
        [SerializeField]
        [Tooltip("ステートがあるレイヤー番号")]
        int layer;

        ShipHealth health;
        int triggerHash;
        int stateHash;

        void Awake()
        {
            health = GetComponent<ShipHealth>();
            triggerHash = Animator.StringToHash(triggerName);
            stateHash = Animator.StringToHash(stateName);
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(DamageInfo info)
        {
            if (animator == null) return;
            // ステートを直接再生：遷移の判定待ちが無く、被弾と同じフレームから揺れ始める
            if (!string.IsNullOrEmpty(stateName) && animator.HasState(layer, stateHash))
            {
                animator.Play(stateHash, layer, 0f);
                return;
            }

            animator.ResetTrigger(triggerHash); // 連続被弾でもトリガーが溜まらず、毎回頭から揺れる
            animator.SetTrigger(triggerHash);
        }
    }
}
