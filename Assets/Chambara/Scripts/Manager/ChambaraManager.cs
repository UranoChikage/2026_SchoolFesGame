using System;
using System.Collections;
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
        public event Action OnTieClash;        // 相打ちで剣がぶつかった瞬間
        public event Action<int> OnGuarded;    // ガードされた瞬間（引数は攻撃した側）

        [Header("Slow Motion Settings")]
        [SerializeField] float slowTimeScale = 0.2f;
        [SerializeField] float slowInTime = 0.1f;    // 剣がぶつかるまでの時間（実時間）。この間にスローにする
        [SerializeField] float slowDuration = 1f;    // 剣がぶつかってからスローを保つ時間（実時間）
        [SerializeField] float slowOutTime = 0.5f;   // 元の速さに戻るまで（実時間）
        public float SlowTimeScale => slowTimeScale;
        public float SlowDuration => slowDuration;
        [SerializeField] AnimationCurve slowMotionCurve;
        public AnimationCurve SlowMotionCurve => slowMotionCurve;
        float defaultFixedDeltaTime;

        [Header("Model Settings")]
        [SerializeField] string[] playerModelNames;
        [SerializeField] string[] swordModelNames;

        private void Awake()
        {
            instance = this;
            defaultFixedDeltaTime = Time.fixedDeltaTime;
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
            players[0].Init(0, playerModelNames[0], swordModelNames[0], sim.players[0], edgePlayerPos0, edgePlayerPos1);
            players[1].Init(1, playerModelNames[1], swordModelNames[1], sim.players[1], edgePlayerPos1, edgePlayerPos0);
            startTime = Time.time;
            SetGameState(GameState.Ready);
        }

        public void StartGame()
        {
            SetGameState(GameState.Playing);
        }

        private void Update()
        {
            if (gameState is GameState.Playing)
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

            p0.Resolved = p1.Resolved = true;   // 結果は剣がぶつかった瞬間に出すので、通常の判定はさせない

            // 後から振った方の剣がぶつかる時刻（ゲーム内時間）
            float impactTime = Mathf.Max(p0.ClashImpactTime, p1.ClashImpactTime);
            string both = $"{AttackLog(0)} {AttackLog(1)}=>";
            if (result == ClashResult.Tie)
            {
                StartCoroutine(SlowMotion(impactTime, () =>
                {
                    Debug.Log(both + "相打ち");
                    OnTieClash?.Invoke();
                    p0.Stun(true);
                    p1.Stun(true);
                }));
                return;
            }
            int winner = result == ClashResult.Player0 ? 0 : 1;
            impactTime = players[winner].AttackImpactTime;   // 勝った方の剣が届いた瞬間に決着
            StartCoroutine(SlowMotion(impactTime, () => Finish(both + $"ほぼ同時 {winner + 1}P:勝ち", 1 - winner)));
        }

        // 振っている途中からスローにして、impactTimeに onImpact を呼び、しばらくしたら元の速さに戻す
        IEnumerator SlowMotion(float impactTime, Action onImpact)
        {
            SetGameState(GameState.Slow);

            //剣が届くまでの残り（ゲーム内時間）を、ちょうどslowInTime（実時間）かけて進むように減速する
            //カーブを何乗かして減速の早さを調整するので、剣の振りの速さ自体は変えない
            float power = SolveSlowInPower((impactTime - Time.time) / slowInTime);
            for (float t = 0f; Time.time < impactTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Pow(Mathf.Clamp01(EvaluateSlowCurve(Mathf.Clamp01(t / slowInTime))), power);
                SetTimeScale(Mathf.Lerp(1f, slowTimeScale, k));
                yield return null;
            }
            SetTimeScale(slowTimeScale);
            onImpact();

            yield return new WaitForSecondsRealtime(slowDuration);

            //slowOutTimeの時間をかけて元に戻す
            yield return ChangeTimeScale(slowTimeScale, 1f, slowOutTime);
            if (gameState == GameState.Slow) SetGameState(GameState.Playing);
        }

        // 減速中の平均の速さが avgScale になる（＝残り時間をslowInTimeで進む）ように、カーブを何乗するか求める
        // avgScale が slowTimeScale〜1 の外だと間に合わないので、近い方の端（すぐスロー / ほぼ減速なし）になる
        float SolveSlowInPower(float avgScale)
        {
            float lo = -10f, hi = 10f;   // 2の何乗か
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) / 2f;
                if (AverageSlowInScale(Mathf.Pow(2f, mid)) > avgScale) hi = mid;   // 乗数が大きいほど減速が遅れて平均が速くなる
                else lo = mid;
            }
            return Mathf.Pow(2f, (lo + hi) / 2f);
        }

        float AverageSlowInScale(float power)
        {
            const int samples = 32;
            float sum = 0f;
            for (int i = 0; i < samples; i++)
            {
                float k = Mathf.Pow(Mathf.Clamp01(EvaluateSlowCurve((i + 0.5f) / samples)), power);
                sum += Mathf.Lerp(1f, slowTimeScale, k);
            }
            return sum / samples;
        }

        IEnumerator ChangeTimeScale(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetTimeScale(Mathf.Lerp(from, to, EvaluateSlowCurve(t / duration)));
                yield return null;
            }
            SetTimeScale(to);
        }

        // カーブ未設定時は線形
        float EvaluateSlowCurve(float t)
        {
            if (slowMotionCurve == null || slowMotionCurve.length == 0) return t;
            return slowMotionCurve.Evaluate(t);
        }

        void SetTimeScale(float scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(scale, 0.01f); // 物理もスローに合わせて滑らかにする
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
                    OnGuarded?.Invoke(atk);
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
            if (loser >= 0)
            {
                players[loser].Lose();
                players[1 - loser].Win();
            }
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
