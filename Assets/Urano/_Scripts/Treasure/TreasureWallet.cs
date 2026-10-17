using System;
using UnityEngine;

namespace Pirates
{
    public enum TreasureRank { E, D, C, B, A, S }

    /// <summary>
    /// 獲得したお宝の合計金額とランク判定。シーンに1つ置く。
    /// 発進時に0へ戻し、リザルトで Total / Rank を表示に使う。
    /// </summary>
    public class TreasureWallet : MonoBehaviour
    {
        [Header("ランクのしきい値（この金額以上でそのランク。仮置き）")]
        [SerializeField] int thresholdD = 500;
        [SerializeField] int thresholdC = 1500;
        [SerializeField] int thresholdB = 3000;
        [SerializeField] int thresholdA = 5000;
        [SerializeField] int thresholdS = 8000;

        public static TreasureWallet Current { get; private set; }

        /// <summary>(獲得したお宝, 合計金額)</summary>
        public event Action<TreasureAsset, int> Added;

        public int Total { get; private set; }
        public TreasureRank Rank => RankFor(Total);

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

        public void ResetTotal() => Total = 0;

        public void Add(TreasureAsset treasure)
        {
            if (treasure == null) return;
            Total += treasure.Value;
            Added?.Invoke(treasure, Total);
        }

        public TreasureRank RankFor(int amount)
        {
            if (amount >= thresholdS) return TreasureRank.S;
            if (amount >= thresholdA) return TreasureRank.A;
            if (amount >= thresholdB) return TreasureRank.B;
            if (amount >= thresholdC) return TreasureRank.C;
            if (amount >= thresholdD) return TreasureRank.D;
            return TreasureRank.E;
        }
    }
}
