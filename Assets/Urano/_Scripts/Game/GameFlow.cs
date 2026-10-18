using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

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
        [SerializeField]
        [Tooltip("PC用の開始キー（待機中に押すと導入演出へ）")]
        Key startKey = Key.Space;

        [Header("導入演出（UIアニメ→発進）")]
        [SerializeField]
        [Tooltip("導入のTimeline（任意）。終わったら発進。未設定ならintroDurationだけ待つ")]
        PlayableDirector introDirector;
        [SerializeField]
        float introDuration = 3f;
        [SerializeField]
        [Tooltip("PC用のスキップキー（導入演出中に押すと発進）")]
        Key skipKey = Key.Enter;

        [Header("終了演出")]
        [SerializeField]
        [Tooltip("終了のTimeline（任意）。終わったらリザルト。未設定ならoutroDurationだけ待つ")]
        PlayableDirector outroDirector;
        [SerializeField]
        float outroDuration = 3f;

        [Header("制御する対象（未設定ならシーンから自動取得）")]
        [SerializeField] EnemySpawner spawner;

        [Header("Inspectorから繋ぐイベント（BGM・UI切替など）")]
        public UnityEvent onIntroStarted;
        public UnityEvent onGameStarted;
        public UnityEvent onGameEnded;
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

        BoatController boat;
        ShipHealth boatHealth;
        CannonStation[] stations;
        float phaseEndTime;

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
                    if (Keyboard.current != null && Keyboard.current[startKey].wasPressedThisFrame) StartGame();
                    break;

                case GamePhase.Intro:
                    if (Keyboard.current != null && Keyboard.current[skipKey].wasPressedThisFrame) SkipIntro();
                    else if (SequenceDone(introDirector)) BeginPlaying();
                    break;

                case GamePhase.Playing:
                    Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
                    TimeChanged?.Invoke(Remaining, timeLimit);
                    if (Remaining <= 0f) EndGame(true);
                    break;

                case GamePhase.Outro:
                    if (SequenceDone(outroDirector)) ShowResult();
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
            if (Phase == GamePhase.Playing) EndGame(false);
        }

        void EndGame(bool survived)
        {
            Survived = survived;
            Phase = GamePhase.Outro;
            SetPlaying(false);
            GameEnded?.Invoke(survived);
            onGameEnded?.Invoke();
            BeginSequence(outroDirector, outroDuration);
        }

        void ShowResult()
        {
            Phase = GamePhase.Result;
            Result?.Invoke();
            onResult?.Invoke();
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
        public float OutroDuration => outroDirector != null ? (float)outroDirector.duration : outroDuration;
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
