using System;
using UnityEngine;

namespace Chambara
{
    public enum PlayerState { Idle, Attack, Guard, Stun, Thrust }   // Thrust: 突き（Attackと同じく判定待ち→硬直）
    public enum AttackResult { Whiff, Guarded, Hit }        // 空振り / ガードされた / ヒット
    public enum ClashResult { None, Player0, Player1, Tie } // 同時判定なし / 1P勝ち / 2P勝ち / 相殺

    public class Fighter
    {
        public PlayerState state;
        public int dir = -1;   // 攻撃方向 or ガード方向（上=0から時計回り）
        public float position;         // 自分の端から進んだ距離（0=自分の端）
        public float targetPosition;   // 1歩の行き先（止まっているときは position と同じ）
    }

    /// <summary>
    /// Unityに依存しないチャンバラの判定。距離と2人の状態を持ち、判定結果を返すだけ
    /// </summary>
    public class ChambaraSimulator
    {
        public readonly Fighter[] players = { new Fighter(), new Fighter() };
        readonly ChambaraRule r;

        public ChambaraSimulator(ChambaraRule rule) => r = rule;

        // 2人の間の距離（両端=1）
        public float Distance => 1f - players[0].position - players[1].position;
        // 2人が今の移動を終えたときの距離
        public float TargetDistance => 1f - players[0].targetPosition - players[1].targetPosition;

        // 攻撃が成功したかどうか
        public AttackResult Attack(int atk, int dir)
        {
            Fighter def = players[1 - atk];
            if (Distance > r.attackRange) return AttackResult.Whiff;
            if (def.state == PlayerState.Guard )
            {
                if(Mathf.Abs(Mirror(def.dir) - dir)<=1)return AttackResult.Guarded;
            }
            return AttackResult.Hit;
        }

        // 突きが成功したかどうか（前へ出終わった後に呼ぶ）。ガードは方向を問わず防げる
        public AttackResult Thrust(int atk)
        {
            if (Distance > r.thrustRange) return AttackResult.Whiff;
            if (players[1 - atk].state == PlayerState.Guard) return AttackResult.Guarded;
            return AttackResult.Hit;
        }

        // 突きで前へ出る。行き先を targetPosition に入れる（相手に stopDistance より近づかない・下がらない）
        public void ThrustAdvance(int player)
        {
            Fighter me = players[player], op = players[1 - player];
            float limit = 1f - op.targetPosition - r.stopDistance;
            me.targetPosition = Math.Max(me.targetPosition, Math.Min(me.targetPosition + r.thrustDistance, limit));
        }

        // 突きが当たったとき：受けた側を1歩分（backStepDistance）後ろへ、当てた側を相手の至近距離（stopDistance）まで進める
        // 受けた側がもう自分の端にいたら何も動かさず true（吹っ飛ばされる）
        public bool ThrustPush(int atk)
        {
            Fighter me = players[atk], def = players[1 - atk];
            if (def.targetPosition <= 1e-5f) return true;
            def.targetPosition = Math.Max(0f, def.targetPosition - r.backStepDistance);
            me.targetPosition = Math.Max(me.targetPosition, 1f - def.targetPosition - r.stopDistance);
            return false;
        }

        // 攻撃（斬り・突き）が届く距離
        public float Reach(PlayerState state) => state == PlayerState.Thrust ? r.thrustRange : r.attackRange;

        // 2人がほぼ同時に攻撃したときの判定（引数は攻撃した時刻・秒、届く距離）
        // 突きは前へ出ながら判定するので、移動を終えたときの距離で比べる
        public ClashResult Clash(float time0, float time1, float range)
        {
            float diff = Math.Abs(time0 - time1);
            if (TargetDistance > range || diff > r.closeCallTime) return ClashResult.None;
            if (diff <= r.tieTime) return ClashResult.Tie;
            return time0 < time1 ? ClashResult.Player0 : ClashResult.Player1;
        }

        // 1歩踏み出す。行き先を targetPosition に入れ、進めないときは false
        // 2人が同時に動いても重ならないよう、相手も行き先の位置で判定する
        public bool TryStep(int player, bool forward)
        {
            Fighter me = players[player], op = players[1 - player];
            float target;
            if (forward)
            {
                target = me.targetPosition + r.stepDistance;
                if (1f - target - op.targetPosition < r.stopDistance - 1e-5f) return false;   // 近すぎるので進めない
            }
            else
            {
                target = Math.Max(0f, me.targetPosition - r.backStepDistance);
                if (target >= me.targetPosition) return false;   // もう自分の端にいる
            }
            me.targetPosition = target;
            return true;
        }

        int Mirror(int dir) => (r.directions - dir) % r.directions;   // 向かい合っているので左右を反転
    }
}
