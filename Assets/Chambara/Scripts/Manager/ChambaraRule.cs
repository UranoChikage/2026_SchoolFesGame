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
        public float stepDistance     = 0.3f;  // 前進1歩の距離（attackRangeより大きくする）
        public float backStepDistance = 0.3f;  // 後退1歩の距離（自分の端より後ろには下がらない）
        public float stopDistance     = 0.05f; // 1歩進むとこの距離より近くなる場合は前進できない
        public float attackRange      = 0.15f; // この距離より遠いと空振り

        // 突き
        public float thrustDistance   = 0.3f;  // 突きで前へ出る距離（相手に stopDistance より近づく分は進まない）
        public float thrustRange      = 0.2f;  // 前へ出た後、この距離より遠いと空振り

        // 2人の攻撃時刻の差（秒）
        public float closeCallTime = 0.1f;     // これ以内なら「ほぼ同時」→ 速い方が勝ち
        public float tieTime       = 0.01f;    // これ以内なら「同時」→ 相殺
    }
}
