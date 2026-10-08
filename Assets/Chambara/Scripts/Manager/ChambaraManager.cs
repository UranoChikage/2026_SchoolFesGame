using System;
using UnityEngine;

namespace Chambara
{
    public enum GameState { None, Ready ,Playing, Slow, Finished } //開始前 /プレイ中 /スロー演出中 / 終了後

    /// <summary>
    /// 試合の流れ。プレイヤーの状態を更新し、判定はChambaraSimulatorに任せる
    /// </summary>
    public class ChambaraManager : MonoBehaviour
    {
        private static ChambaraManager instance;
        public static ChambaraManager Instance => instance;

        [SerializeField] PlayerBehaviour playerPrefab;
        [SerializeField] Transform edgePlayerPos0;
        [SerializeField] Transform edgePlayerPos1;
        [SerializeField] float matchTime = 60f;
        [SerializeField] ChambaraRule rule = new ChambaraRule();

        ChambaraSimulator sim;
        PlayerBehaviour[] players;
        public PlayerBehaviour[] Players => players;
        float startTime;

        [SerializeField] GameState gameState = GameState.None;
        public GameState GameState => gameState;
        public event Action<GameState> OnGameStateChanged;

        private void Awake()
        {
            instance = this;
        }
        private void Start()
        {
            ReadyGame();
        }

        public void ReadyGame()
        {
            sim = new ChambaraSimulator(rule);
            players = new[]
            {
                Instantiate(playerPrefab, edgePlayerPos0.position, Quaternion.identity),
                Instantiate(playerPrefab, edgePlayerPos1.position, Quaternion.identity),
            };
            players[0].Init(0, sim.players[0], edgePlayerPos0, edgePlayerPos1);
            players[1].Init(1, sim.players[1], edgePlayerPos1, edgePlayerPos0);
            startTime = Time.time;
            SetGameState(GameState.Ready);
        }

        public void StartGame()
        {
            SetGameState(GameState.Playing);
        }

        private void Update()
        {
            if (gameState is GameState.Playing or GameState.Slow)
            {
                sim.Advance(Time.deltaTime);
                foreach (var p in players) p.UpdateState(Time.deltaTime);
                CheckClash();
                for (int i = 0; i < 2; i++)
                {
                    CheckAttack(i);
                }
                if (Time.time - startTime >= matchTime) Finish("時間切れ 引き分け", -1);
            }

            if (gameState != GameState.Ready && gameState != GameState.None)
            {
                foreach (var p in players) p.Render(sim.Distance);
            }

        }

        public void SetGameState(GameState gameState)
        {
            this.gameState = gameState;
            OnGameStateChanged?.Invoke(gameState);
        }

        // 2人とも振っていて、まだ判定が済んでいない場合
        void CheckClash()
        {
            PlayerBehaviour p0 = players[0], p1 = players[1];
            if (p0.Data.state != PlayerState.Attack || p1.Data.state != PlayerState.Attack || p0.Resolved || p1.Resolved) return;

            ClashResult result = sim.Clash(p0.AttackTime, p1.AttackTime);
            if (result == ClashResult.None) return;

            string both = $"{AttackLog(0)} {AttackLog(1)}=>";
            if (result == ClashResult.Tie)
            {
                Debug.Log(both + "相打ち");
                p0.Stun(true);
                p1.Stun(true);
                return;
            }
            int winner = result == ClashResult.Player0 ? 0 : 1;
            Finish(both + $"ほぼ同時 {winner + 1}P:勝ち", 1 - winner);   // TODO: スロー・ズーム演出
        }

        void CheckAttack(int atk)
        {
            PlayerBehaviour p = players[atk];
            if (!p.IsHitTiming) return;
            p.Resolved = true;

            string log = $"{AttackLog(atk)}=>";
            switch (sim.Attack(atk, p.Data.dir))
            {
                case AttackResult.Whiff:
                    Debug.Log(log + "空振り");
                    break;
                case AttackResult.Guarded:
                    Debug.Log(log + $"{2 - atk}P:ガード");
                    p.Stun(false);
                    break;
                case AttackResult.Hit:
                    Finish(log + $"{2 - atk}P:ヒット", 1 - atk);
                    break;
            }
        }

        // loser: 負けたプレイヤー（-1:引き分け）
        void Finish(string log, int loser)
        {
            Debug.Log(log);
            if (loser >= 0) players[loser].Lose();
            SetGameState(GameState.Finished);
        }

        // 方向はテンキー表記（8=上, 9=右上, 6=右 … 7=左上）。各プレイヤー視点
        static readonly int[] Numpad = { 8, 9, 6, 3, 2, 1, 4, 7 };
        string AttackLog(int player)
        {
            int dir = players[player].Data.dir;
            return $"{player + 1}P:{(rule.directions == 8 ? Numpad[dir] : dir)}攻撃";
        }
    }
}
