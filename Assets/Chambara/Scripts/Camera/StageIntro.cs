using System;
using System.Collections;
using Common;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Chambara
{
    /// <summary>
    /// シーン開始時の導入演出。ステージ全体を遠くから → 1Pアップ → 2Pアップ → 通常画面（分割）に戻して、
    /// AllPlayersReady（readyRoot）を表示する。
    ///
    /// ■ 使い方
    ///   1. 適当なオブジェクトに付ける
    ///   2. readyRoot に AllPlayersReady が付いたUIのルート（Canvas など）を入れる（演出中は非表示にする）
    ///   3. introCamera は空なら CameraManager の mainCamera を使う
    ///   4. shots の各カットの位置は Inspector で調整する
    /// </summary>
    public class StageIntro : MonoBehaviour
    {
        public enum IntroTarget { Stage, Player1, Player2 }

        [Serializable]
        public class Shot
        {
            [Tooltip("Stage: 2人の中間（stageCenter があればそこ）を基準。前方 = 1P→2P の向き\nPlayer1/2: そのプレイヤーを基準。前方 = プレイヤーの向き（相手の方）")]
            public IntroTarget target = IntroTarget.Stage;
            [Min(0.01f)] public float duration = 2f;
            [Tooltip("カメラの位置（開始）。基準から見た x:右 y:上 z:前")]
            public Vector3 fromOffset;
            [Tooltip("カメラの位置（終了）。基準から見た x:右 y:上 z:前")]
            public Vector3 toOffset;
            [Tooltip("注視点。基準から見た x:右 y:上 z:前")]
            public Vector3 lookOffset;
            public float fov = 40f;
        }

        [SerializeField, Tooltip("演出を流すか（OFFなら何もせず readyRoot を表示する）")]
        bool playIntro = true;

        [SerializeField, Tooltip("演出用カメラ。空なら CameraManager の mainCamera を使う")]
        Camera introCamera;

        [SerializeField, Tooltip("Stage カットの基準。空なら2人の中間")]
        Transform stageCenter;

        [SerializeField] Shot[] shots =
        {
            new Shot { target = IntroTarget.Stage,   duration = 3f,   fromOffset = new Vector3(18f, 12f, -14f), toOffset = new Vector3(12f, 5f, 6f),   lookOffset = new Vector3(0f, 1f, 0f),   fov = 50f },
            new Shot { target = IntroTarget.Player1, duration = 1.5f, fromOffset = new Vector3(0.5f, 1.5f, 2.4f), toOffset = new Vector3(0.3f, 1.5f, 1.6f), lookOffset = new Vector3(0f, 1.3f, 0f), fov = 40f },
            new Shot { target = IntroTarget.Player2, duration = 1.5f, fromOffset = new Vector3(-0.5f, 1.5f, 2.4f), toOffset = new Vector3(-0.3f, 1.5f, 1.6f), lookOffset = new Vector3(0f, 1.3f, 0f), fov = 40f },
        };

        [SerializeField, Tooltip("カット内の動き（0→1 で from→to）")]
        AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField, Tooltip("AllPlayersReady が付いたUIのルート。演出中は非表示、終わったら表示する")]
        GameObject readyRoot;
        [SerializeField, Tooltip("readyRoot を表示するとき、CanvasRenderer の透明度を 0→1 にかける時間（秒）。0以下ならすぐ表示")]
        float readyFadeTime = 0.5f;

        [Serializable]
        public class PlayerUi
        {
            [Tooltip("そのプレイヤーのアップ中だけ表示するUI（最初は非表示にしておく）")]
            public GameObject root;
            [Tooltip("キャラクター名（日本語名）を入れるテキスト")]
            public TMP_Text nameText;
        }

        [Header("Player UI")]
        [SerializeField, Tooltip("1Pアップ・2Pアップのときに出すUI。1P から順に")]
        PlayerUi[] playerUis = new PlayerUi[2];
        [SerializeField, Tooltip("プレイヤーUIを出すとき、透明度を 0→1 にかける時間（秒）。0以下ならすぐ表示")]
        float playerUiFadeTime = 0.3f;
        Coroutine playerUiFade;

        [Header("Skip")]
        [SerializeField, Tooltip("ボタンで演出を飛ばせるか")]
        bool allowSkip = true;
        [SerializeField] SwitchButton skipButton = SwitchButton.Cancel;
        [SerializeField, Tooltip("デバッグ用：キーボードの Enter で飛ばす")]
        bool debugEnterSkip = true;

        [Header("Events")]
        [SerializeField, Tooltip("各カットが始まった瞬間（引数はカット番号）。名前表示や効果音に")]
        UnityEvent<int> onShotStarted;
        [SerializeField, Tooltip("演出が終わって通常画面に戻った瞬間")]
        UnityEvent onFinished;

        public bool IsPlaying { get; private set; }

        void Awake()
        {
            // AllPlayersReady は有効になった瞬間から待ち始めるので、演出が終わるまで止めておく
            if (playIntro && readyRoot != null) readyRoot.SetActive(false);
            ShowPlayerUi(-1);
        }

        void Start()
        {
            if (!playIntro)
            {
                Finish();
                return;
            }
            StartCoroutine(PlayRoutine());
        }

        IEnumerator PlayRoutine()
        {
            IsPlaying = true;
            Camera cam = introCamera != null ? introCamera : CameraManager.Instance.MainCamera;
            CameraManager.Instance.BeginIntro(cam);

            // ChambaraManager の Start でプレイヤーが作られるまで待つ
            while (ChambaraManager.Instance.Players == null) yield return null;

            bool skipped = false;
            for (int i = 0; i < shots.Length && !skipped; i++)
            {
                Shot shot = shots[i];
                cam.fieldOfView = shot.fov;
                ShowPlayerUi(shot.target switch { IntroTarget.Player1 => 0, IntroTarget.Player2 => 1, _ => -1 });
                onShotStarted?.Invoke(i);

                for (float t = 0f; t < shot.duration; t += Time.unscaledDeltaTime)
                {
                    PlaceCamera(cam, shot, t / shot.duration);
                    if (SkipPressed()) { skipped = true; break; }
                    yield return null;
                }
                if (!skipped) PlaceCamera(cam, shot, 1f);
            }

            // 飛ばしたボタンがそのまま AllPlayersReady の OK にならないよう1フレーム空ける
            if (skipped) yield return null;

            ShowPlayerUi(-1);
            CameraManager.Instance.EndIntro();
            IsPlaying = false;
            Finish();
        }

        void Finish()
        {
            if (readyRoot != null)
            {
                readyRoot.SetActive(true);
                if (readyFadeTime > 0f) StartCoroutine(FadeIn(readyRoot, readyFadeTime));
            }
            onFinished?.Invoke();
        }

        // player 番目（0=1P）のUIだけ表示して名前を入れる。-1 なら全部隠す
        void ShowPlayerUi(int player)
        {
            if (playerUis == null) return;
            if (playerUiFade != null) StopCoroutine(playerUiFade);
            playerUiFade = null;

            for (int i = 0; i < playerUis.Length; i++)
            {
                PlayerUi ui = playerUis[i];
                if (ui == null || ui.root == null) continue;
                ui.root.SetActive(i == player);
            }

            if (player < 0 || player >= playerUis.Length) return;
            PlayerUi target = playerUis[player];
            if (target == null || target.root == null) return;

            PlayerBehaviour[] players = ChambaraManager.Instance.Players;
            if (target.nameText != null && players != null && player < players.Length)
                target.nameText.text = players[player].DisplayName;
            if (playerUiFadeTime > 0f) playerUiFade = StartCoroutine(FadeIn(target.root, playerUiFadeTime));
        }

        // root 以下の CanvasRenderer の透明度を 0→1 に滑らかに上げる
        IEnumerator FadeIn(GameObject root, float time)
        {
            CanvasRenderer[] renderers = root.GetComponentsInChildren<CanvasRenderer>(true);
            SetReadyAlpha(renderers, 0f);
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                SetReadyAlpha(renderers, t / time);
                yield return null;
            }
            SetReadyAlpha(renderers, 1f);
        }

        static void SetReadyAlpha(CanvasRenderer[] renderers, float alpha)
        {
            foreach (CanvasRenderer r in renderers)
            {
                if (r != null) r.SetAlpha(alpha);
            }
        }

        void PlaceCamera(Camera cam, Shot shot, float p)
        {
            GetFrame(shot.target, out Vector3 origin, out Quaternion yaw);
            float k = moveCurve != null && moveCurve.length > 0 ? moveCurve.Evaluate(Mathf.Clamp01(p)) : p;
            Vector3 offset = Vector3.LerpUnclamped(shot.fromOffset, shot.toOffset, k);

            cam.transform.position = origin + yaw * offset;
            cam.transform.LookAt(origin + yaw * shot.lookOffset);
        }

        // カットの基準位置と向き（水平の回転だけ）
        void GetFrame(IntroTarget target, out Vector3 origin, out Quaternion yaw)
        {
            PlayerBehaviour[] players = ChambaraManager.Instance.Players;
            Transform p0 = players[0].transform;
            Transform p1 = players[1].transform;

            switch (target)
            {
                case IntroTarget.Player1:
                    origin = p0.position;
                    yaw = Yaw(p0.forward);
                    break;
                case IntroTarget.Player2:
                    origin = p1.position;
                    yaw = Yaw(p1.forward);
                    break;
                default:
                    origin = stageCenter != null ? stageCenter.position : (p0.position + p1.position) / 2f;
                    yaw = stageCenter != null ? Yaw(stageCenter.forward) : Yaw(p1.position - p0.position);
                    break;
            }
        }

        static Quaternion Yaw(Vector3 dir)
        {
            Vector3 f = Vector3.ProjectOnPlane(dir, Vector3.up);
            return f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f) : Quaternion.identity;
        }

        bool SkipPressed()
        {
            if (!allowSkip) return false;
            if (debugEnterSkip)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
            }
            for (int i = 0; i < 2; i++)
            {
                if (SwitchInput.I.ButtonDown(i, skipButton)) return true;
            }
            return false;
        }
    }
}
