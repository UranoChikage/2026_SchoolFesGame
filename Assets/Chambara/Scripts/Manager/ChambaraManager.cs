using System;
using System.Collections;
using Common;
using UnityEngine;
using UnityEngine.InputSystem;

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
        public event Action<int> OnHit;        // 攻撃が当たった瞬間（引数は攻撃した側）。1本取ったときだけ
        public event Action<int> OnThrustHit;  // 突きが当たって相手がのけぞった瞬間（引数は攻撃した側）。1本にならないとき
        public event Action<PlayerBehaviour> OnKnockback;   // 斬られた方が吹っ飛び始めた瞬間（ヒットストップの後）

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

        [Header("Hit Settings")]
        [SerializeField] HitStop hitStop;   // 未設定ならシーンから探す。シーンにも無ければヒットストップせずにすぐ吹っ飛ぶ

        [Header("Model Settings")]
        [SerializeField] string[] playerModelNames;
        [SerializeField] string[] swordModelNames;

        [Header("Match Settings")]
        [SerializeField] int winScore = 2;                 // 何本先取で勝ちか
        [SerializeField] Key nextRoundKey = Key.Space;     // 1本取った後、このキーで次のラウンドへ（決着後は再戦）
        [SerializeField] SwitchButton nextRoundButton = SwitchButton.Confirm;   // Joy-Con（1P・2Pどちらでも）で次へ進むボタン。ZR/ZL
        [SerializeField] float winnerResultDelay = 1.5f;   // 決着のスコア表示から WinnerResult を呼ぶまで（実時間・秒）
        [SerializeField] WinnerResult winnerResult;        // 未設定ならシーンから探す

        readonly int[] scores = new int[2];
        public int[] Scores => scores;                     // 取った本数（[0]:1P [1]:2P）
        public int LastRoundWinner { get; private set; } = -1;   // 直前のラウンドで1本取った方（-1:引き分け）
        bool waitingNextRound;                             // 次のラウンドへ進むボタン待ち
        bool waitingRematch;                               // 決着後、再戦するボタン待ち

        private void Awake()
        {
            instance = this;
            defaultFixedDeltaTime = Time.fixedDeltaTime;
        }
        private void Start()
        {
            // 設定し忘れてもシーンに置いてあれば使う
            if (hitStop == null) hitStop = FindAnyObjectByType<HitStop>();
            if (hitStop == null) Debug.LogWarning("HitStop がシーンにないので、ヒットストップせずに吹っ飛ばします");
            if (winnerResult == null) winnerResult = FindAnyObjectByType<WinnerResult>();
            ReadyGame();
        }

        public void ReadyGame()
        {
            waitingNextRound = false;
            waitingRematch = false;
            LastRoundWinner = -1;
            sim = new ChambaraSimulator(rule);
            players = new[]
            {
                Instantiate(playerPrefab, edgePlayerPos0.position, Quaternion.identity),
                Instantiate(playerPrefab, edgePlayerPos1.position, Quaternion.identity),
            };
            // キャラ選択画面から来たときはその選択を使う（このシーンから直接始めたときはインスペクターの設定）
            string[] models = ChambaraSelection.HasValue ? ChambaraSelection.PlayerModelNames : playerModelNames;
            string[] swords = ChambaraSelection.HasValue ? ChambaraSelection.SwordModelNames : swordModelNames;
            players[0].Init(0, models[0], swords[0], sim, edgePlayerPos0, edgePlayerPos1);
            players[1].Init(1, models[1], swords[1], sim, edgePlayerPos1, edgePlayerPos0);
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
                foreach (var p in players) p.Render();
            }

            if ((waitingNextRound || waitingRematch) && NextPressed())
            {
                if (waitingRematch) Rematch();
                else NextRound();
            }
        }

        // ラウンドの結果（スコア）を見せ終わったときに呼ぶ（ClashCamera から）
        // どちらかが winScore 本取っていれば WinnerResult、まだならボタンで次のラウンドへ
        public void OnRoundResultShown()
        {
            if (gameState != GameState.Finished || waitingNextRound) return;
            if (LastRoundWinner >= 0 && scores[LastRoundWinner] >= winScore)
            {
                StartCoroutine(ShowWinnerResult(LastRoundWinner));
                return;
            }
            Debug.Log($"{scores[0]} - {scores[1]}　{nextRoundKey} で次のラウンドへ");
            waitingNextRound = true;
        }

        IEnumerator ShowWinnerResult(int winner)
        {
            yield return new WaitForSecondsRealtime(winnerResultDelay);
            if (winnerResult != null) winnerResult.Play(winner, scores[0], scores[1]);
            else Debug.LogWarning("WinnerResult がシーンにありません");
            Debug.Log($"{nextRoundKey} / Joy-Con の {nextRoundButton} で再戦");
            waitingRematch = true;
        }

        // 次へ進む操作（キーボード、または1P・2Pどちらかの Joy-Con）
        bool NextPressed()
        {
            if (Keyboard.current != null && Keyboard.current[nextRoundKey].wasPressedThisFrame) return true;
            for (int i = 0; i < 2; i++)
            {
                if (SwitchInput.I.ButtonDown(i, nextRoundButton)) return true;
            }
            return false;
        }

        // 決着後、スコアを0に戻して同じキャラでもう一試合
        public void Rematch()
        {
            if (!waitingRematch) return;
            waitingRematch = false;
            Array.Clear(scores, 0, scores.Length);
            Restart();
        }

        // プレイヤーを作り直して、次のラウンドを Ready から始める（UIボタンや Joy-Con からも呼べる）
        public void NextRound()
        {
            if (!waitingNextRound) return;
            Restart();
        }

        void Restart()
        {
            StopAllCoroutines();
            SetTimeScale(1f);
            foreach (var p in players)
            {
                if (p != null) Destroy(p.gameObject);
            }
            ReadyGame();
        }

        private void OnValidate()
        {
            if (rule.stepDistance <= rule.attackRange)
                Debug.LogWarning($"stepDistance({rule.stepDistance}) は attackRange({rule.attackRange}) より大きくしてください");
        }

        public void SetGameState(GameState gameState)
        {
            this.gameState = gameState;
            OnGameStateChanged?.Invoke(gameState);
        }

        // 2人とも攻撃（斬り・突き）していて、まだ判定が済んでいない場合
        void CheckClash()
        {
            PlayerBehaviour p0 = players[0], p1 = players[1];
            if (!p0.IsAttacking || !p1.IsAttacking || p0.Resolved || p1.Resolved) return;

            float range = Mathf.Max(sim.Reach(p0.Data.state), sim.Reach(p1.Data.state));
            ClashResult result = sim.Clash(p0.AttackTime, p1.AttackTime, range);
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
            bool thrust = players[winner].Data.state == PlayerState.Thrust;
            StartCoroutine(SlowMotion(impactTime, () =>
            {
                if (thrust)
                {
                    ThrustHit(winner, both + $"ほぼ同時 {winner + 1}P:");   // 突きが勝ったときは突きのヒットと同じ
                    return;
                }
                Finish(both + $"ほぼ同時 {winner + 1}P:勝ち", 1 - winner);
                Hit(winner);
            }));
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

        public void SetTimeScale(float scale)
        {
            Debug.Log($"TimeScale: {scale}");
            Time.timeScale = scale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * Mathf.Max(scale, 0.01f); // 物理もスローに合わせて滑らかにする
        }

        void CheckAttack(int atk)
        {
            PlayerBehaviour p = players[atk];
            if (!p.IsHitTiming) return;
            p.Resolved = true;

            string log = $"{AttackLog(atk)}=>";
            if (p.Data.state == PlayerState.Thrust)
            {
                CheckThrust(atk, log);
                return;
            }
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
                    Hit(atk);
                    break;
            }
        }

        void CheckThrust(int atk, string log)
        {
            switch (sim.Thrust(atk))
            {
                case AttackResult.Whiff:
                    Debug.Log(log + "空振り");
                    break;
                case AttackResult.Guarded:
                    Debug.Log(log + $"{2 - atk}P:ガード");
                    OnGuarded?.Invoke(atk);
                    players[atk].ThrustGuarded();
                    break;
                case AttackResult.Hit:
                    ThrustHit(atk, log + $"{2 - atk}P:");
                    break;
            }
        }

        // 突きが当たった：受けた側をのけぞらせて1歩分下げ、当てた側を詰め寄らせる
        // 受けた側が一番後ろにいたら吹っ飛ばして1本
        void ThrustHit(int atk, string log)
        {
            if (sim.ThrustPush(atk))
            {
                Finish(log + "突きで吹っ飛び", 1 - atk);
                Hit(atk);
                return;
            }
            Debug.Log(log + "突きヒット");
            players[1 - atk].ThrustHitTaken();
            players[atk].ThrustClose();
            OnThrustHit?.Invoke(atk);
        }

        // 攻撃が当たった瞬間の演出。ヒットストップが終わってから斬られた方を吹っ飛ばす
        void Hit(int attacker)
        {
            OnHit?.Invoke(attacker);
            PlayerBehaviour loser = players[1 - attacker];
            if (hitStop != null) hitStop.Play(() => Knockback(loser));
            else Knockback(loser);
        }

        void Knockback(PlayerBehaviour loser)
        {
            loser.Knockback();
            OnKnockback?.Invoke(loser);
        }

        // loser: 負けたプレイヤー（-1:引き分け）
        void Finish(string log, int loser)
        {
            Debug.Log(log);
            if (loser >= 0)
            {
                players[loser].Lose();
                players[1 - loser].Win();
                LastRoundWinner = 1 - loser;
                scores[LastRoundWinner]++;
            }
            SetGameState(GameState.Finished);

            // 引き分けは吹っ飛び演出もスコア表示もないので、ポイントなしですぐやり直し待ちにする
            if (loser < 0)
            {
                Debug.Log($"ポイントなし　{nextRoundKey} でやり直し");
                waitingNextRound = true;
            }
        }

        // 方向はテンキー表記（8=上, 9=右上, 6=右 … 7=左上）。各プレイヤー視点
        static readonly int[] Numpad = { 8, 9, 6, 3, 2, 1, 4, 7 };
        string AttackLog(int player)
        {
            if (players[player].Data.state == PlayerState.Thrust) return $"{player + 1}P:突き";
            int dir = players[player].Data.dir;
            return $"{player + 1}P:{(rule.directions == 8 ? Numpad[dir] : dir)}攻撃";
        }
    }
}
