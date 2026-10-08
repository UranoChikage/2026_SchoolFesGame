using System;

namespace Chambara
{
    /// <summary>
    /// ChambaraSimulatorの判定に使う値
    /// </summary>
    [Serializable]
    public class ChambaraRule
    {
        public int directions      = 8;        // 攻撃・防御の方向数（上=0から時計回り）

        // 距離は両端の間を1、接触を0とする
        public float moveSpeed     = 0.3f;     // 1人が1秒に進む距離
        public float slowDistance  = 0.3f;     // この距離から減速し始める
        public float stopDistance  = 0.05f;    // この距離で速度が0になる
        public float attackRange   = 0.15f;    // この距離より遠いと空振り

        // 2人の攻撃時刻の差（秒）
        public float closeCallTime = 0.1f;     // これ以内なら「ほぼ同時」→ 速い方が勝ち
        public float tieTime       = 0.01f;    // これ以内なら「同時」→ 相殺
    }
}
