using System;

namespace Chambara
{
    public struct PlayerInput
    {
        public int pose;   // 今の構え方向（-1:なし）
        public int swing;  // このフレームで振った方向（-1:振っていない）

        public static readonly PlayerInput None = new PlayerInput { pose = -1, swing = -1 };
    }

    public class Fighter
    {
        public int pose = -1, poseHold;   // 構え方向と、同じ方向に構え続けているフレーム数
        public int attackDir = -1;        // 攻撃中の方向（-1:攻撃していない）
        public int hitFrame;              // 攻撃が当たるフレーム
        public int stunUntil;             // 怯みが解けるフレーム
    }

    /// <summary>
    /// Unityに依存しないチャンバラの1対1シミュレーション。1Tick = 1フレーム
    /// </summary>
    public class ChambaraSimulator
    {
        public readonly Fighter[] f = { new Fighter(), new Fighter() };
        public int Frame { get; private set; }
        public int Winner { get; private set; } = -1;   // -1:引き分け・未決着
        public bool Finished { get; private set; }

        public event Action<int> OnHit;            // 勝者
        public event Action<int> OnGuarded;        // ガードされて怯んだ側
        public event Action OnClash;               // 完全に同時 → 両方怯む
        public event Action<int, int> OnShowdown;  // ほぼ同時（勝者, フレーム差）→ スロー演出用

        readonly ChambaraRule r;
        public ChambaraSimulator(ChambaraRule rule)
        {
            r = rule;
        }

        public void Tick(PlayerInput in0, PlayerInput in1)
        {
            if (Finished) return;
            Frame++;
            Step(f[0], in0);
            Step(f[1], in1);
            if (Showdown()) return;
            for (int i = 0; i < 2 && !Finished; i++)
            {
                Resolve(i, 1 - i);
            }

            if (!Finished && Frame >= r.matchFrames) Finished = true;   // 時間切れ
        }

        public bool IsGuarding(Fighter p) => p.attackDir < 0 && p.pose >= 0 && p.poseHold >= r.guardHoldFrames;

        void Step(Fighter p, PlayerInput input)
        {
            if (Frame < p.stunUntil) return;
            if (p.attackDir >= 0)
            {
                if (Frame < p.hitFrame + r.attackRecovery) return;   // 攻撃中・硬直中
                p.attackDir = -1;
            }
            if (input.swing >= 0)
            {
                p.attackDir = input.swing;
                p.hitFrame = Frame + r.attackStartup;
                p.poseHold = 0;
                return;
            }
            p.poseHold = input.pose == p.pose ? p.poseHold + 1 : 0;
            p.pose = input.pose;
        }

        // 2人とも振り始めていて、まだ当たっていない場合の判定
        bool Showdown()
        {
            if (!InStartup(f[0]) || !InStartup(f[1])) return false;
            int diff = f[0].hitFrame - f[1].hitFrame;
            int abs = Math.Abs(diff);
            if (abs <= r.tieFrames)
            {
                Stun(f[0], r.clashStun);
                Stun(f[1], r.clashStun);
                OnClash?.Invoke();
                return true;
            }
            if (abs > r.closeCallFrames) return false;
            Finish(diff < 0 ? 0 : 1);
            OnShowdown?.Invoke(Winner, abs);
            return true;
        }

        /// <summary>
        /// 攻撃側が防御側に当たったときの処理
        /// </summary>
        void Resolve(int atk, int def)
        {
            Fighter a = f[atk], d = f[def];
            if (a.attackDir < 0 || Frame != a.hitFrame) return;
            if (IsGuarding(d) && d.pose == Mirror(a.attackDir))
            {
                Stun(a, r.parriedStun);
                OnGuarded?.Invoke(atk);
            }
            else
            {
                Finish(atk);
                OnHit?.Invoke(atk);
            }
        }

        bool InStartup(Fighter p)
        {
            return p.attackDir >= 0 && Frame <= p.hitFrame;
        }
        int Mirror(int dir) => (r.directions - dir) % r.directions;   // 向かい合っているので左右を反転
        void Stun(Fighter p, int frames) { p.attackDir = -1; p.poseHold = 0; p.stunUntil = Frame + frames; }
        void Finish(int winner) { Winner = winner; Finished = true; }
    }
}
