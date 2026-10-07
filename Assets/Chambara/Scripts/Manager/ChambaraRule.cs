using System;

namespace Chambara
{
    /// <summary>
    /// チャンバラのルール・調整値
    /// </summary>
    [Serializable]
    public class ChambaraRule
    {
        public int directions      = 8;        // 攻撃・防御の方向数（上=0から時計回り）
        public int matchFrames     = 60 * 60;  // 試合時間（1分）
        public int guardHoldFrames = 60;       // 同じ方向にこのフレーム数構えるとガード成立
        public int attackStartup   = 12;       // 振ってから当たるまで
        public int attackRecovery  = 20;       // 攻撃後の硬直
        public int parriedStun     = 50;       // ガードされた側の怯み
        public int clashStun       = 30;       // 完全に同時だったときの怯み
        public int tieFrames       = 0;        // 当たるタイミングの差がこれ以内なら「完全に同時」
        public int closeCallFrames = 6;        // 当たるタイミングの差がこれ以内なら「ほぼ同時」
    }
}
