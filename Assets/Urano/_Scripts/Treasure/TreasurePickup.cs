using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 海に浮かぶ宝箱。大砲の爆発かビームが触れると獲得（拾う操作なし）。
    /// アイテムと違い、ビームでも取れる。誰が取っても金額は共通なので持ち主は関係ない。
    /// </summary>
    public class TreasurePickup : MonoBehaviour
    {

        static readonly int OpenHash = Animator.StringToHash("Open");

        [SerializeField]
        [Tooltip("獲得してから消えるまでの時間(秒)。Animatorの Open が終わる長さに合わせる。0なら即消える")]
        float openDuration = 1f;

        /// <summary>海に出ている宝箱一覧（画面端アイコンが使う）</summary>
        public static readonly System.Collections.Generic.List<TreasurePickup> Active = new System.Collections.Generic.List<TreasurePickup>();

        TreasureAsset treasure;
        float baseY;
        bool collected;
        float releaseTime;
        Animator animator;
        Collider[] colliders;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        // プールから出るたびに、閉じた状態・当たり判定ありに戻す
        void OnEnable()
        {
            Active.Add(this);
            releaseTime = 0f;
            foreach (Collider c in colliders) c.enabled = true;
            if (animator != null) animator.Rebind();
        }

        void OnDisable() => Active.Remove(this);

        void Update()
        {
            if (releaseTime > 0f && Time.time >= releaseTime) PoolManager.Release(gameObject);
        }

        // 全ドロップで1つを共有（Prefab未設定時の仮の見た目）
        static TreasurePickup fallbackPrefab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => fallbackPrefab = null;

        /// <summary>宝箱を海に出す。prefab未設定なら仮の球。lifetime秒後に消える</summary>
        public static TreasurePickup Spawn(TreasurePickup prefab, TreasureAsset treasure, Vector3 position, float lifetime)
        {
            TreasurePickup p = PoolManager.Spawn(prefab != null ? prefab : FallbackPrefab, position, Quaternion.identity, lifetime);
            p.treasure = treasure;
            p.baseY = position.y ;
            p.collected = false;
            return p;
        }

        static TreasurePickup FallbackPrefab
        {
            get
            {
                if (fallbackPrefab == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "TreasurePickup";
                    go.transform.localScale = Vector3.one * 2f; // 当てやすいよう大きめ
                    fallbackPrefab = go.AddComponent<TreasurePickup>();
                    go.SetActive(false);
                }
                return fallbackPrefab;
            }
        }

        /// <summary>砲弾の爆発やビームが当たったときに呼ぶ。獲得して Open を再生し、openDuration 秒後に消える</summary>
        public bool TryCollect()
        {
            // 1回の爆発で複数のコライダーに触れても、二重に加算しない
            if (collected || treasure == null || TreasureWallet.Current == null) return false;
            collected = true;

            TreasureWallet.Current.Add(treasure);
            treasure.PlayDropVfx(transform.position);

            // 開いている間は当たり判定と画面端アイコンを無くす
            foreach (Collider c in colliders) c.enabled = false;
            Active.Remove(this);

            if (animator != null && openDuration > 0f)
            {
                animator.SetTrigger(OpenHash);
                releaseTime = Time.time + openDuration;
            }
            else
            {
                PoolManager.Release(gameObject);
            }
            return true;
        }
    }
}
