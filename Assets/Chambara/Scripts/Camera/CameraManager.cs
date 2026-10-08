using UnityEngine;

namespace Chambara
{
    public class CameraManager : MonoBehaviour
    {
        [SerializeField] Camera mainCamera;


        [Header("Camera Settings")]
        [SerializeField] private Camera playerCamera;
        Camera playerCamera2;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0.5f, 1.8f, -2f);   // プレイヤーから見たカメラの位置（x:右 y:上 z:前）
        [SerializeField] private Vector3 lookOffset   = new Vector3(0f, 1.2f, 2f);      // プレイヤーから見た注視点（前方＝相手の方を見る）


        GameState currentState = GameState.None;

        private void Start()
        {
            ChambaraManager.Instance.OnGameStateChanged += ChangeGameState;
        }
        private void ChangeGameState(GameState newState)
        {
            currentState = newState;
            switch (newState)
            {
                case GameState.Ready:
                    Initialize();
                    break;
                case GameState.Playing:
                    break;
                case GameState.Slow:
                    break;
                case GameState.Finished:
                    break;
            }
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
    }
}
