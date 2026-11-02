namespace Pirates
{
    public enum EndReason { TimeUp, Sunk }

    /// <summary>
    /// 1回のプレイの結果。GameFlow が終了時に作り、GameResult.Last に保持する。
    /// static なのでシーンを切り替えても残り、リザルト用のシーンからそのまま読める。
    /// </summary>
    public readonly struct GameResult
    {
        public readonly EndReason Reason;
        public readonly int Total;
        public readonly TreasureRank Rank;
        /// <summary>終了時の残り秒（時間切れなら0）</summary>
        public readonly float Remaining;
        /// <summary>終了時点で点いていた星の数（リザルトのボーナス計算に使う）</summary>
        public readonly int Stars;

        /// <summary>ランクA/Sのしきい値（リザルトで「懸賞金+ボーナス」からランクを再判定するために保持）</summary>
        public readonly int ThresholdA;
        public readonly int ThresholdS;

        public bool Survived => Reason == EndReason.TimeUp;

        public GameResult(EndReason reason, int total, TreasureRank rank, float remaining, int stars = 0, int thresholdA = 3000, int thresholdS = 6000)
        {
            Reason = reason;
            Total = total;
            Rank = rank;
            Remaining = remaining;
            Stars = stars;
            ThresholdA = thresholdA;
            ThresholdS = thresholdS;
        }

        /// <summary>直近の結果。まだ終了していなければ HasLast が false</summary>
        /// <summary>金額からランクを判定する（Bが最低）</summary>
        public TreasureRank RankFor(int amount) =>
            amount >= ThresholdS ? TreasureRank.S : amount >= ThresholdA ? TreasureRank.A : TreasureRank.B;

        public static GameResult Last { get; private set; }
        public static bool HasLast { get; private set; }

        public static void Set(GameResult result)
        {
            Last = result;
            HasLast = true;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic()
        {
            Last = default;
            HasLast = false;
        }
    }
}
