namespace Chambara
{
    public enum ActionType
    {
        None = 0,
        Move,
        Attack,
        Guard,
        Stun
    }

    public enum ActionPhase
    {
        Startup,
        Active,
        Recovery
    }

    /// <summary>
    /// プレイヤーの動的データを管理するクラス
    /// </summary>
    public class PlayerData
    {
        /// <summary>
        /// プレイヤーの現在の距離
        /// </summary>
        public float distance = 0f;

        /// <summary>
        /// プレイヤーの現在の状態
        /// </summary>
        public ActionType actionType = ActionType.None;

        /// <summary>
        /// プレイヤーの現在の状態のフェーズ
        /// </summary>
        public ActionPhase phase;

        public int frame = 0;
    }
}
