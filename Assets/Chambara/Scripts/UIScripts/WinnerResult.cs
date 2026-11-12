using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chambara
{
    /// <summary>
    /// 試合（2本先取）に勝ったときの演出
    /// 2人を決まった位置に立たせて専用のカメラで映す。勝った方は片腕を突き上げて喜び、負けた方は崩れ落ちて泣く
    /// </summary>
    public class WinnerResult : MonoBehaviour
    {
        [Header("立ち位置（位置と向き。カメラの方を向けておく）。勝敗に関係なく 1P / 2P で固定")]
        [SerializeField, FormerlySerializedAs("winnerPoint")] Transform player1Point;
        [SerializeField, FormerlySerializedAs("loserPoint")] Transform player2Point;

        [Header("カメラ（シーンに置いて無効にしておく。演出中だけ有効にする）")]
        [SerializeField] Camera resultCamera;

        [Header("涙（任意。負けた方の頭に付ける）")]
        [SerializeField] GameObject tearsEffect;
        [SerializeField, Tooltip("頭の中心から見た位置（x:右 y:上 z:前）")] Vector3 tearsOffset = new Vector3(0f, 0f, 0.1f);
        GameObject tears;

        private void Start()
        {
            if (resultCamera != null) resultCamera.gameObject.SetActive(false);
            if (ChambaraManager.Instance != null) ChambaraManager.Instance.OnGameStateChanged += OnGameStateChanged;
        }

        private void OnDestroy()
        {
            if (ChambaraManager.Instance != null) ChambaraManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }

        // 再戦で次の試合が始まったら演出を片付ける
        private void OnGameStateChanged(GameState state)
        {
            if (state != GameState.Ready) return;
            if (resultCamera != null) resultCamera.gameObject.SetActive(false);
            if (tears != null) Destroy(tears);
        }

        // winner: 勝ったプレイヤー（0:1P 1:2P）　score1 / score2: 最終スコア
        public void Play(int winner, int score1, int score2)
        {
            Debug.Log($"{winner + 1}P の勝ち！ {score1} - {score2}");

            PlayerBehaviour[] players = ChambaraManager.Instance.Players;
            PlayerBehaviour l = players[1 - winner];

            Transform[] points = { player1Point, player2Point };
            for (int i = 0; i < 2; i++)
            {
                if (points[i] != null) players[i].PlayResult(points[i]);
                else Debug.LogWarning($"WinnerResult の player{i + 1}Point が未設定です");
            }

            if (resultCamera != null && CameraManager.Instance != null) CameraManager.Instance.ChangeResultMode(resultCamera);
            else Debug.LogWarning("WinnerResult の resultCamera が未設定です");

            if (tearsEffect != null && l.TryGetComponent(out BodyRig rig) && rig.Head != null)
            {
                tears = Instantiate(tearsEffect, rig.Head);
                tears.transform.position = rig.HeadCenter + rig.Head.rotation * tearsOffset;
            }
        }
    }
}
