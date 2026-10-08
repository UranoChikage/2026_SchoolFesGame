using System;
using UnityEngine;

namespace Chambara
{
    public enum PlayerState { Idle, Attack, Guard, Stun }
    public enum AttackResult { Whiff, Guarded, Hit }        // 空振り / ガードされた / ヒット
    public enum ClashResult { None, Player0, Player1, Tie } // 同時判定なし / 1P勝ち / 2P勝ち / 相殺

    public class Fighter
    {
        public PlayerState state;
        public int dir = -1;   // 攻撃方向 or ガード方向（上=0から時計回り）
    }

    /// <summary>
    /// Unityに依存しないチャンバラの判定。距離と2人の状態を持ち、判定結果を返すだけ
    /// </summary>
    public class ChambaraSimulator
    {
        public float Distance = 1f;   // 2人の間の距離（両端=1）
        public readonly Fighter[] players = { new Fighter(), new Fighter() };
        readonly ChambaraRule r;

        public ChambaraSimulator(ChambaraRule rule) => r = rule;

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

        // 2人がほぼ同時に攻撃したときの判定（引数は攻撃した時刻・秒）
        public ClashResult Clash(float time0, float time1)
        {
            float diff = Math.Abs(time0 - time1);
            if (Distance > r.attackRange || diff > r.closeCallTime) return ClashResult.None;
            if (diff <= r.tieTime) return ClashResult.Tie;
            return time0 < time1 ? ClashResult.Player0 : ClashResult.Player1;
        }

        // 常に前進する。slowDistanceから減速し、stopDistanceで止まる
        public void Advance(float deltaTime)
        {
            float t = Math.Clamp((Distance - r.stopDistance) / (r.slowDistance - r.stopDistance), 0f, 1f);
            Distance = Math.Max(r.stopDistance, Distance - 2f * r.moveSpeed * t * deltaTime);
        }

        int Mirror(int dir) => (r.directions - dir) % r.directions;   // 向かい合っているので左右を反転
    }
}
