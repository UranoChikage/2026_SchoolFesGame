using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Pirates
{
    public enum GamePhase { Standby, Intro, Playing, Outro, Result }

    /// <summary>
    /// ゲーム全体の進行。待機(スロットル0) → 導入演出 → 発進(敵スポーン・制限時間) → 終了演出 → リザルト。
    /// BGMやUIは C# イベント、またはInspectorのUnityEventを購読して連動させる。
    /// 導入／終了の演出はTimeline(PlayableDirector)を割り当てるとその終了待ち、未設定なら秒数で進む。
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [Header("制限時間")]
        [SerializeField]
        [Tooltip("発進してからの制限時間(秒)")]
        float timeLimit = 90f;

        [Header("待機")]
        [SerializeField]
        [Tooltip("ONなら開始操作を待たず、シーン開始と同時に導入演出へ進む")]
        bool autoStart = false;

        [Header("導入演出（UIアニメ→発進）")]
        [SerializeField]
        [Tooltip("導入のTimeline（任意）。終わったら発進。未設定ならintroDurationだけ待つ")]
        PlayableDirector introDirector;
        [SerializeField]
        float introDuration = 3f;

        [Header("終了演出（時間切れ）")]
        [SerializeField]
        [Tooltip("時間切れの終了Timeline（任意）。終わったらリザルト。未設定ならoutroDurationだけ待つ")]
        PlayableDirector outroDirector;
        [SerializeField]
        float outroDuration = 3f;
        [SerializeField]
        [Tooltip("リザルトになったら読み込むシーン名（任意・Build Settingsに登録が必要）。空ならこのシーンのまま")]
        string sceneAfterTimeUp = "";

        [Header("終了演出（沈没）")]
        [SerializeField]
        [Tooltip("沈没の終了Timeline（任意）。未設定なら sunkOutroDuration だけ待つ")]
        PlayableDirector sunkOutroDirector;
        [SerializeField]
        float sunkOutroDuration = 3f;
        [SerializeField]
        [Tooltip("リザルトになったら読み込むシーン名（任意）。空ならこのシーンのまま")]
        string sceneAfterSunk = "";

        [Header("制御する対象（未設定ならシーンから自動取得）")]
        [SerializeField] EnemySpawner spawner;

        [Header("Inspectorから繋ぐイベント（BGM・UI切替など）")]
        public UnityEvent onIntroStarted;
        public UnityEvent onGameStarted;
        public UnityEvent onGameEnded;
        public UnityEvent onTimeUp;
        public UnityEvent onSunk;
        public UnityEvent onResult;

        public static GameFlow Current { get; private set; }

        public event Action IntroStarted;
        /// <summary>発進した瞬間（BGM再生・敵スポーン開始）</summary>
        public event Action GameStarted;
        /// <summary>制限時間切れ、または自船の沈没で終了した瞬間。引数=生き残ったか</summary>
        public event Action<bool> GameEnded;
        public event Action Result;
        /// <summary>(残り秒, 制限時間)</summary>
        public event Action<float, float> TimeChanged;

        public GamePhase Phase { get; private set; } = GamePhase.Standby;
        public float Remaining { get; private set; }
        public float TimeLimit => timeLimit;
        public bool Survived { get; private set; }
        public EndReason Reason { get; private set; }

        BoatController boat;
        ShipHealth boatHealth;
        CannonStation[] stations;
        float phaseEndTime;
        PlayableDirector outro; // 今回の終了演出（理由で切り替わる）

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        void Awake()
        {
            Current = this;
            boat = FindFirstObjectByType<BoatController>();
            if (boat != null) boatHealth = boat.GetComponent<ShipHealth>();
            stations = FindObjectsByType<CannonStation>(FindObjectsSortMode.None);
            if (spawner == null) spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            if (boatHealth != null) boatHealth.Died += OnPlayerDied;
        }

        void OnDisable()
        {
            if (boatHealth != null) boatHealth.Died -= OnPlayerDied;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Start()
        {
            Remaining = timeLimit;
            SetPlaying(false);
            TimeChanged?.Invoke(Remaining, timeLimit);
            if (autoStart) StartGame();
        }

        /// <summary>待機中の開始操作（UIボタンなどから呼ぶ）</summary>
        public void StartGame()
        {
            if (Phase != GamePhase.Standby) return;
            Phase = GamePhase.Intro;
            IntroStarted?.Invoke();
            onIntroStarted?.Invoke();
            BeginSequence(introDirector, introDuration);
        }

        void Update()
        {
            switch (Phase)
            {
                case GamePhase.Standby:
                    if (GameInput.StartPressed()) StartGame();
                    break;

                case GamePhase.Intro:
                    if (GameInput.SkipPressed()) SkipIntro();
                    else if (SequenceDone(introDirector)) BeginPlaying();
                    break;

                case GamePhase.Playing:
                    Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
                    TimeChanged?.Invoke(Remaining, timeLimit);
                    if (Remaining <= 0f) EndGame(EndReason.TimeUp);
                    break;

                case GamePhase.Outro:
                    if (SequenceDone(outro)) ShowResult();
                    break;
            }
        }

        void BeginPlaying()
        {
            Phase = GamePhase.Playing;
            SetPlaying(true);
            GameStarted?.Invoke();
            onGameStarted?.Invoke();
        }

        void OnPlayerDied()
        {
            if (Phase == GamePhase.Playing) EndGame(EndReason.Sunk);
        }

        void EndGame(EndReason reason)
        {
            Reason = reason;
            Survived = reason == EndReason.TimeUp;
            Phase = GamePhase.Outro;
            SetPlaying(false);

            // 結果はここで確定して保持する（シーンを跨いでリザルトから読める）
            TreasureWallet wallet = TreasureWallet.Current;
            GameResult.Set(new GameResult(reason, wallet != null ? wallet.Total : 0,
                wallet != null ? wallet.Rank : TreasureRank.B, Remaining));

            GameEnded?.Invoke(Survived);
            onGameEnded?.Invoke();
            if (reason == EndReason.TimeUp) onTimeUp?.Invoke(); else onSunk?.Invoke();

            outro = reason == EndReason.TimeUp ? outroDirector : (sunkOutroDirector != null ? sunkOutroDirector : outroDirector);
            BeginSequence(outro, reason == EndReason.TimeUp ? outroDuration : sunkOutroDuration);
        }

        void ShowResult()
        {
            Phase = GamePhase.Result;
            Result?.Invoke();
            onResult?.Invoke();

            string scene = Reason == EndReason.TimeUp ? sceneAfterTimeUp : sceneAfterSunk;
            if (!string.IsNullOrEmpty(scene)) SceneManager.LoadScene(scene);
        }

        // 導入・終了の待ち：Timelineがあれば再生して終了待ち、無ければ秒数待ち
        void BeginSequence(PlayableDirector director, float fallbackSeconds)
        {
            phaseEndTime = Time.time + fallbackSeconds;
            if (director == null) return;
            director.time = 0d; // 2回目以降も先頭から
            director.Play();
        }

        // Wrap Mode が None なら終了時に再生が止まる(timeは0に戻ることがある)、Hold/Loopなら止まらず末尾に居続ける。
        // どちらでも終わりを検知できるよう、停止か末尾到達のどちらかで判定する
        bool SequenceDone(PlayableDirector director)
        {
            if (director != null) return director.state != PlayState.Playing || director.time >= director.duration;
            return Time.time >= phaseEndTime;
        }

        /// <summary>導入演出の長さ(秒)。Timeline未設定ならintroDuration</summary>
        public float IntroDuration => introDirector != null ? (float)introDirector.duration : introDuration;
        /// <summary>終了演出の長さ(秒)。Timeline未設定ならoutroDuration</summary>
        public float OutroDuration => outro != null ? (float)outro.duration : (Reason == EndReason.TimeUp ? outroDuration : sunkOutroDuration);
        /// <summary>導入演出の経過秒</summary>
        public float IntroElapsed => introDirector != null ? (float)introDirector.time : Mathf.Max(0f, introDuration - (phaseEndTime - Time.time));

        /// <summary>導入演出をスキップして発進する。Timelineは最終状態まで進めてから止める（カメラなども演出後の状態になる）</summary>
        public void SkipIntro()
        {
            if (Phase != GamePhase.Intro) return;
            if (introDirector != null)
            {
                introDirector.time = introDirector.duration;
                introDirector.Evaluate();
                introDirector.Stop();
            }
            BeginPlaying();
        }

        // 操縦・砲撃・敵スポーンのON/OFFをまとめて切り替える
        void SetPlaying(bool playing)
        {
            if (boat != null) boat.ControlEnabled = playing;
            foreach (CannonStation s in stations) if (s != null) s.enabled = playing;
            if (spawner != null) spawner.enabled = playing;
        }
    }
}
