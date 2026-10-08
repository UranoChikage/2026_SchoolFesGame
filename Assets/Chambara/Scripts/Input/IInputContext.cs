namespace Chambara
{
    public struct PlayerInput
    {
        public int pose;   // 今の構え方向（-1:なし）
        public int swing;  // 振った方向（-1:振っていない）

        public static readonly PlayerInput None = new PlayerInput { pose = -1, swing = -1 };
    }

    /// <summary>
    /// プレイヤー1人分の入力元（デバッグ用キーボード、Joy-Conなど）
    /// </summary>
    public interface IChambaraInputContext
    {
        void Init(int playerIndex);

        /// <summary>
        /// 今の入力を返す。振り入力は読んだら消費される
        /// </summary>
        PlayerInput Read();
    }
}
