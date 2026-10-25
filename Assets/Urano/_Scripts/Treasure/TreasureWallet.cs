using System;
using UnityEngine;

namespace Pirates
{
    public enum TreasureRank { B, A, S }

    /// <summary>
    /// 獲得したお宝の合計金額とランク判定。シーンに1つ置く。
    /// 発進時に0へ戻し、リザルトで Total / Rank を表示に使う。
    /// </summary>
    public class TreasureWallet : MonoBehaviour
    {
        [Header("ランクのしきい値（この金額以上でそのランク。仮置き）")]
        [SerializeField]
        [Tooltip("星1つ目の金額。ランクはBが最低のままで、これは星表示と難易度の段階にだけ使う")]
        int thresholdB = 1000;
        [SerializeField] int thresholdA = 3000;
        [SerializeField] int thresholdS = 6000;

        public static TreasureWallet Current { get; private set; }

        /// <summary>(獲得したお宝, 合計金額)</summary>
        public event Action<TreasureAsset, int> Added;

        /// <summary>星の数が変わった（引数=新しい星の数 0〜3）</summary>
        public event Action<int> StarsChanged;

        public const int MaxStars = 3;

        public int Total { get; private set; }
        public TreasureRank Rank => RankFor(Total);

        /// <summary>星の数。0=Bの手前、1=B到達、2=A到達、3=S到達（最大）</summary>
        public int Stars => StarsFor(Total);

        /// <summary>次の星までのゲージ(0〜1)。最大のときは1</summary>
        public float StageProgress
        {
            get
            {
                int stars = Stars;
                if (stars >= MaxStars) return 1f;
                int from = stars == 0 ? 0 : StarThreshold(stars - 1);
                int to = StarThreshold(stars);
                return Mathf.Clamp01((Total - from) / (float)Mathf.Max(1, to - from));
            }
        }

        /// <summary>シーンの星の数。ウォレットが無いシーンでは0（難易度の段階などが参照する）</summary>
        public static int CurrentStars => Current != null ? Current.Stars : 0;

        // index 0=星1つ目(B), 1=星2つ目(A), 2=星3つ目(S)
        int StarThreshold(int index) => index == 0 ? thresholdB : index == 1 ? thresholdA : thresholdS;

        public int StarsFor(int amount)
        {
            int n = 0;
            for (int i = 0; i < MaxStars; i++) if (amount >= StarThreshold(i)) n = i + 1;
            return n;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        void Awake() => Current = this;

        // GameFlow.Current は Awake で決まるので、購読は全Awake後の Start で行う
        void Start()
        {
            if (GameFlow.Current != null) GameFlow.Current.GameStarted += ResetTotal;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (GameFlow.Current != null) GameFlow.Current.GameStarted -= ResetTotal;
        }

        public void ResetTotal()
        {
            Total = 0;
            StarsChanged?.Invoke(0);
        }

        public void Add(TreasureAsset treasure)
        {
            if (treasure == null) return;
            int before = Stars;
            Total += treasure.Value;
            Debug.Log($"[Treasure] {treasure.DisplayName} +{treasure.Value} (合計 {Total} / ランク {Rank})", this);
            Added?.Invoke(treasure, Total);
            if (Stars != before) StarsChanged?.Invoke(Stars);
        }

        public TreasureRank RankFor(int amount)
        {
            if (amount >= thresholdS) return TreasureRank.S;
            if (amount >= thresholdA) return TreasureRank.A;
            return TreasureRank.B; // Bが最低ランク（Aのしきい値未満は全てB）
        }
    }
}
