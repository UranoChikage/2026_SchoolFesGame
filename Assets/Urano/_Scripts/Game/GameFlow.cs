using System;
using System.Collections.Generic;
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

        [Header("キャリブレーション（導入のあと）")]
        [SerializeField]
        [Tooltip("導入Timelineが終わったら出す「Cancelを押して合わせる」UI（CancelReadyUi）。最初は非表示にしておく。" +
                 "未設定ならそのまま発進する。UIの AllPlayersReady.onAllReady に GameFlow.CompleteCalibration を繋ぐこと")]
        GameObject calibrationUi;

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
        bool calibrating;    // 導入後、全員のCancel待ち
        bool startRequested; // 開始要求済み（ロード画面が開き終わるのを待っている場合を含む）

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

            // Play On Awake のままのTimelineがロード画面の裏で先に動かないよう、開始前に止めておく
            StopIfPlaying(introDirector);
            StopIfPlaying(outroDirector);
            StopIfPlaying(sunkOutroDirector);

            startRequested = autoStart;

            // 導入Timelineの0秒時点の状態を、再生を待たずに反映する。
            // Activation Track はTimelineが評価されるまで効かないので、これが無いと待機中は、エフェクトなどが
            // シーンに保存されたON/OFFのままになる（autoStart だと直後に再生されるので気づきにくい）。
            // 再生はしない（Pausedのまま）ので、StartGame で改めて頭から再生される
            if (introDirector != null)
            {
                introDirector.time = 0d;
                introDirector.Evaluate();
                HoldTimelineAnimators(introDirector);
            }
        }

        // 導入Timelineの Animation Track にバインドされたAnimatorは、コントローラの初期ステートが勝手に再生されてしまう
        // （Play On Awake の設定が無い）。0秒の姿勢にしたまま止めておき、導入が始まる直前に動かせる状態へ戻す
        readonly List<Animator> heldAnimators = new List<Animator>();

        void HoldTimelineAnimators(PlayableDirector director)
        {
            heldAnimators.Clear();
            if (director.playableAsset == null) return;
            foreach (PlayableBinding binding in director.playableAsset.outputs)
            {
                if (binding.outputTargetType != typeof(Animator)) continue;
                if (!(director.GetGenericBinding(binding.sourceObject) is Animator animator) || animator == null) continue;
                animator.enabled = false;
                heldAnimators.Add(animator);
            }
        }

        void ReleaseTimelineAnimators()
        {
            foreach (Animator animator in heldAnimators) if (animator != null) animator.enabled = true;
            heldAnimators.Clear();
        }

        static void StopIfPlaying(PlayableDirector director)
        {
            if (director != null && director.state == PlayState.Playing) director.Stop();
        }

        /// <summary>待機中の開始操作（UIボタンなどから呼ぶ）。ロード画面が開ききるまでは、開いてから始める</summary>
        public void StartGame()
        {
            if (Phase != GamePhase.Standby) return;
            if (SceneTransition.IsOpening) { startRequested = true; return; }
            startRequested = false;
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
                    // Play On Awake のTimelineは Start より後に動き出すことがあるので、開始するまで毎フレーム止める
                    StopIfPlaying(introDirector);
                    if (SceneTransition.IsOpening)
                    {
                        if (GameInput.StartPressed()) startRequested = true; // 開き終わったら始める
                        break;
                    }
                    if (startRequested || GameInput.StartPressed()) StartGame();
                    break;

                case GamePhase.Intro:
                    if (calibrating) break;   // 全員のCancelを待っている（CompleteCalibration で進む）
                    if (GameInput.SkipPressed()) SkipIntro();
                    else if (SequenceDone(introDirector)) EndIntro();
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

        // 導入が終わった。キャリブレーションUIがあれば、全員がCancelを押すまで発進を待つ
        void EndIntro()
        {
            if (calibrationUi == null) { BeginPlaying(); return; }

            calibrating = true;
            calibrationUi.SetActive(true);   // AllPlayersReady は有効になった時に待ち始める
        }

        /// <summary>全員のキャリブレーションが終わった（AllPlayersReady.onAllReady から呼ぶ）。カーソルを中央に戻して発進する</summary>
        public void CompleteCalibration()
        {
            if (!calibrating) return;
            calibrating = false;
            if (calibrationUi != null) calibrationUi.SetActive(false);
            foreach (CannonStation s in stations) if (s != null) s.Recenter();
            BeginPlaying();
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
                wallet != null ? wallet.Rank : TreasureRank.B, Remaining, wallet != null ? wallet.Stars : 0,
                wallet != null ? wallet.ThresholdA : 3000, wallet != null ? wallet.ThresholdS : 6000));

            AudioManager.Play(reason == EndReason.TimeUp ? Sfx.TimeUp : Sfx.GameOver);
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
            SceneTransition.Load(scene);
        }

        // 導入・終了の待ち：Timelineがあれば再生して終了待ち、無ければ秒数待ち
        void BeginSequence(PlayableDirector director, float fallbackSeconds)
        {
            phaseEndTime = Time.time + fallbackSeconds;
            if (director == null) return;
            ReleaseTimelineAnimators(); // 止めておいたAnimatorを、Timelineが動かせる状態に戻す
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
                ReleaseTimelineAnimators();
                introDirector.time = introDirector.duration;
                introDirector.Evaluate();
                introDirector.Stop();
            }
            EndIntro();   // スキップしてもキャリブレーションは飛ばさない
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
