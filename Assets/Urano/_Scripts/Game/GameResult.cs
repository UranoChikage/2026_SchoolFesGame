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

        public bool Survived => Reason == EndReason.TimeUp;

        public GameResult(EndReason reason, int total, TreasureRank rank, float remaining)
        {
            Reason = reason;
            Total = total;
            Rank = rank;
            Remaining = remaining;
        }

        /// <summary>直近の結果。まだ終了していなければ HasLast が false</summary>
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
