using Common;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Chambara
{
    /// <summary>
    /// 指定したプレイヤー数ぶんのコントローラー全員がボタンを押すまで待つ。
    /// 押したプレイヤーには「1P OK!」のような表示（readyIndicators[i]）を出し、全員そろったら onAllReady を呼ぶ。
    ///
    /// ■ 使い方
    ///   1. 適当なオブジェクトに付ける
    ///   2. readyIndicators に「1P OK!」「2P OK!」… のUIオブジェクトを 1P から順に入れる（最初は非表示にしておく）
    ///   3. onAllReady に全員OK後の処理を登録する
    ///   有効になった時点で待ち始める。もう一度待たせたいときは ResetReady() を呼ぶ
    /// </summary>
    public class AllPlayersReady : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("待つプレイヤー数（コントローラー番号 0 〜 playerCount-1）")]
        int playerCount = 2;

        [SerializeField, Tooltip("ON: 繋がっている人数ぶんだけ待つ（最大は playerCount、最小は minPlayers）。OFF: 常に playerCount 人を待つ")]
        bool followConnectedCount = false;

        [SerializeField, Min(1), Tooltip("followConnectedCount が ON のとき、繋がっていなくても最低この人数は待つ")]
        int minPlayers = 2;

        [SerializeField, Tooltip("押してもらうボタン")]
        SwitchButton readyButton = SwitchButton.Cancel;

        [SerializeField, Tooltip("プレイヤーごとの「OK!」表示。1P から順に。押したら表示される")]
        GameObject[] readyIndicators;

        [SerializeField, Tooltip("全員OKになった瞬間に1回だけ呼ばれる")]
        UnityEvent onAllReady;

        [SerializeField, Tooltip("誰かがOKになったときに呼ばれる（引数はプレイヤー番号 0=1P）")]
        UnityEvent<int> onPlayerReady;

        [SerializeField, Tooltip("デバッグ用：キーボードの Enter で全員OK扱いにして進む")]
        bool debugEnterSkip = true;

        bool[] ready;
        bool completed;

        [SerializeField] float duration = 1f;
        [SerializeField] CanvasGroup canvasGroup;

        [SerializeField] bool isSkipOnStart = false;

        public bool IsReady(int player) => ready != null && player >= 0 && player < ready.Length && ready[player];

        void OnEnable() => ResetReady();

        private void Start()
        {
            StartCoroutine(SkipCoroutine());
        }

        IEnumerator SkipCoroutine()
        {
            yield return null;
            if (isSkipOnStart)
            {
                onAllReady?.Invoke();
                transform.root.gameObject.SetActive(false);
            }
        }

        /// <summary>全員を未OKに戻して、もう一度待ち始める</summary>
        public void ResetReady()
        {
            ready = new bool[playerCount];
            completed = false;
            for (int i = 0; i < playerCount; i++) SetIndicator(i, false);
        }

        void Update()
        {
            if (completed) return;

            // いま待つ人数。繋がっている人数に合わせる設定なら、minPlayers 〜 playerCount の範囲に収める
            int need = followConnectedCount
                ? Mathf.Clamp(SwitchInput.I.ConnectedCount, Mathf.Min(minPlayers, playerCount), playerCount)
                : playerCount;

            if (debugEnterSkip && DebugEnterPressed())
            {
                // デバッグ用：Enter で全員OK扱いにして進む
                for (int i = 0; i < need; i++)
                {
                    if (ready[i]) continue;
                    ready[i] = true;
                    SetIndicator(i, true);
                    onPlayerReady?.Invoke(i);
                }
            }

            for (int i = 0; i < need; i++)
            {
                if (ready[i] || !SwitchInput.I.ButtonDown(i, readyButton)) continue;

                ready[i] = true;
                SetIndicator(i, true);
                onPlayerReady?.Invoke(i);
            }

            for (int i = 0; i < need; i++) if (!ready[i]) return;

            completed = true;
            StartCoroutine(Coroutine());
        }

        static bool DebugEnterPressed()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }

        void SetIndicator(int player, bool on)
        {
            if (readyIndicators == null || player >= readyIndicators.Length) return;
            if (readyIndicators[player] != null) readyIndicators[player].SetActive(on);
        }

        IEnumerator Coroutine()
        {
            for(float t = 0f; t < duration; t += Time.deltaTime)
            {
                float a = 1-Mathf.Clamp01(t / duration);
                if (canvasGroup != null) canvasGroup.alpha = a;
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            onAllReady?.Invoke();
        }
    }
}
