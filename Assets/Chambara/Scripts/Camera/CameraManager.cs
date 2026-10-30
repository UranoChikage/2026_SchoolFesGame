using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chambara
{
    public class CameraManager : MonoBehaviour
    {
        private static CameraManager instance;
        public static CameraManager Instance => instance;


        [SerializeField] Camera mainCamera;

        [Header("SlowCamera Settings")]
        [SerializeField] private Camera slowCamera;
        [SerializeField] private float slowStartDistance = 4f;   // 横から見る距離（開始時）
        [SerializeField] private float slowFarDistance   = 6f;   // 横から見る距離（一番引いたとき）
        [SerializeField] private float slowEndDistance   = 1.5f; // 横から見る距離（近づき終わり）
        [SerializeField] private float slowPullBackTime  = 0.1f; // 引きにかける時間（実時間・秒）
        [SerializeField] private float slowApproachTime  = 1f;   // 寄りにかける時間（実時間・秒）
        [SerializeField] private float slowHeight        = 1.2f; // カメラと注視点の高さ
        Coroutine slowCameraRoutine;

        [Header("ClashCamera Settings")]
        [SerializeField] private ClashCamera clashCamera;   // 吹っ飛ばされた方を追いかける

        [Header("Camera Settings")]
        [SerializeField] private Camera playerCamera;
        Camera playerCamera2;
        private Camera[] playerCameras => new Camera[] { playerCamera, playerCamera2 };
        [SerializeField] private Vector3 cameraOffset = new Vector3(0.5f, 1.8f, -2f);   // プレイヤーから見たカメラの位置（x:右 y:上 z:前）
        [SerializeField] private Vector3 lookOffset   = new Vector3(0f, 1.2f, 2f);      // プレイヤーから見た注視点（前方＝相手の方を見る）

        [Header("Expand Settings")]
        [SerializeField] private float expandRate = 1.3f;   // 拡大率（1で等倍。FOVを 1/expandRate にする）
        [SerializeField] private AnimationCurve expandCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);   // 0→1 で等倍→最大拡大
        float baseFov;
        Coroutine expandRoutine;


        GameState currentState = GameState.None;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            ChambaraManager.Instance.OnGameStateChanged += ChangeGameState;
            ChambaraManager.Instance.OnKnockback += loser => ChangeClashMode(loser.Body);

            // ChambaraManager の Start が先に動いて、登録前に Ready になっていたらここで反映する
            if (ChambaraManager.Instance.GameState != GameState.None) ChangeGameState(ChambaraManager.Instance.GameState);
        }
        private void ChangeGameState(GameState newState)
        {
            switch (newState)
            {
                case GameState.Ready:
                    Initialize();
                    break;
                case GameState.Playing:
                    ChangePlayingMode();
                    break;
                case GameState.Slow:
                    ChangeSlowMode();
                    break;
                case GameState.Finished:
                    break;
            }
            currentState = newState;
        }

        private void Update()
        {
            if(currentState is GameState.Playing or GameState.Ready)
            {
                SetPlayerCameraPos(ChambaraManager.Instance.Players[0].transform, playerCamera);

                SetPlayerCameraPos(ChambaraManager.Instance.Players[1].transform, playerCamera2);
            }

        }

        private void Initialize()
        {
            playerCamera2 = Instantiate(playerCamera);
            baseFov = playerCamera.fieldOfView;

            playerCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
            playerCamera2.rect = new Rect(0.5f, 0f, 0.5f, 1f);

            playerCamera.gameObject.SetActive(true);
            playerCamera2.gameObject.SetActive(true);

            mainCamera.gameObject.SetActive(false);
        }

        private void SetPlayerCameraPos(Transform p,Camera camera)
        {
            // プレイヤーの向きに合わせて肩越しに置く。怯みののけぞり（前後の傾き）にはカメラを追従させない
            Vector3 forward = Vector3.ProjectOnPlane(p.forward, Vector3.up);
            Quaternion yaw = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;

            camera.transform.position = p.position + yaw * cameraOffset;
            camera.transform.LookAt(p.position + yaw * lookOffset);
        }

        private void ChangePlayingMode()
        {
            if (slowCameraRoutine != null) StopCoroutine(slowCameraRoutine);
            playerCamera.gameObject.SetActive(true);
            playerCamera2.gameObject.SetActive(true);
            slowCamera.gameObject.SetActive(false);
            if (clashCamera != null)
            {
                clashCamera.SetTarget(null);
                clashCamera.gameObject.SetActive(false);
            }
        }

        private void ChangeSlowMode()
        {
            slowCamera.gameObject.SetActive(true);
            playerCamera.gameObject.SetActive(false);
            playerCamera2.gameObject.SetActive(false);
            if (slowCameraRoutine != null) StopCoroutine(slowCameraRoutine);
            slowCameraRoutine = StartCoroutine(ChangeCameraBack());
        }

        //吹っ飛ばされた方（target）を ClashCamera に追いかけさせる
        public void ChangeClashMode(Transform target)
        {
            if (clashCamera == null) return;
            if (slowCameraRoutine != null) StopCoroutine(slowCameraRoutine);
            slowCameraRoutine = null;
            slowCamera.gameObject.SetActive(false);
            playerCamera.gameObject.SetActive(false);
            playerCamera2.gameObject.SetActive(false);
            clashCamera.gameObject.SetActive(true);
            clashCamera.SetTarget(target);
        }

        //プレイヤーとプレイヤーの間の位置を横から見て、一度引いてからだんだん近づく
        IEnumerator ChangeCameraBack()
        {
            Transform p0 = ChambaraManager.Instance.Players[0].transform;
            Transform p1 = ChambaraManager.Instance.Players[1].transform;

            AnimationCurve curve = ChambaraManager.Instance.SlowMotionCurve;
            bool hasCurve = curve != null && curve.length > 0;   // カーブ未設定時は線形

            // 2人を結ぶ線に垂直な水平方向から見る（向きはスロー開始時に固定）
            Vector3 line = Vector3.ProjectOnPlane(p1.position - p0.position, Vector3.up);
            Vector3 side = line.sqrMagnitude > 0.0001f ? Vector3.Cross(line.normalized, Vector3.up) : Vector3.right;

            // スロー中はtimeScaleが下がるので実時間で進める。Finished後も最後まで近づく
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float distance;
                bool done = false;
                if (t < slowPullBackTime)
                {
                    // 引き：開始距離 → 最遠距離を一瞬で（減速しながら引く）
                    float p = t / slowPullBackTime;
                    distance = Mathf.Lerp(slowStartDistance, slowFarDistance, Mathf.Sin(p * Mathf.PI * 0.5f));
                }
                else
                {
                    // 寄り：最遠距離 → 終了距離
                    float p = slowApproachTime > 0f ? Mathf.Clamp01((t - slowPullBackTime) / slowApproachTime) : 1f;
                    float k = hasCurve ? curve.Evaluate(p) : p;
                    distance = Mathf.LerpUnclamped(slowFarDistance, slowEndDistance, k);
                    done = p >= 1f;
                }

                Vector3 center = (p0.position + p1.position) / 2f + Vector3.up * slowHeight;
                slowCamera.transform.position = center + side * distance;
                slowCamera.transform.LookAt(center);
                if (done) break;
                yield return null;
            }
            slowCameraRoutine = null;
        }

        public void ExpandSmooth(float duration)
        {
            if (expandRoutine != null) StopCoroutine(expandRoutine);
            expandRoutine = StartCoroutine(ExpandSmoothCoroutine(duration));
        }

        //プレイヤーカメラのFOVを狭めてだんだん拡大し、終わったら元に戻す
        //（位置はUpdateで毎フレーム上書きされるので、前に出す代わりにFOVで寄る）
        IEnumerator ExpandSmoothCoroutine(float duration)
        {
            bool hasCurve = expandCurve != null && expandCurve.length > 0;   // カーブ未設定時は線形

            // ヒットストップ中はtimeScaleが下がるので実時間で進める
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = t / duration;
                float k = hasCurve ? expandCurve.Evaluate(p) : p;
                SetPlayerCameraFov(baseFov / Mathf.LerpUnclamped(1f, expandRate, k));
                yield return null;
            }
            expandRoutine = null;
        }

        private void SetPlayerCameraFov(float fov)
        {
            foreach (Camera camera in playerCameras)
            {
                if (camera != null) camera.fieldOfView = fov;
            }
        }
    }
}
