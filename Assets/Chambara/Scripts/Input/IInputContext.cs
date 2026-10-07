namespace Chambara
{
    /// <summary>
    /// プレイヤー1人分の入力元（デバッグ用キーボード、Joy-Conなど）
    /// </summary>
    public interface IChambaraInputContext
    {
        void Init(int playerIndex);

        /// <summary>
        /// シミュレーション1フレーム分の入力を返す。振り入力は読んだら消費される
        /// </summary>
        PlayerInput Read();
    }
}
