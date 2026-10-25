using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 制限時間の時計。本体の画像は固定、秒針の画像だけ回す。
    /// 秒針のRectTransformは、Pivotを回転の中心（本体の中心）に合わせておく。画像は12時方向に向けて作る。
    /// </summary>
    public class TimeClock : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("回す秒針（RectTransform）。Pivotを時計の中心に")]
        RectTransform secondHand;
        [SerializeField]
        [Min(0f)]
        [Tooltip("秒針が1周する秒数。0なら制限時間ちょうどで1周する")]
        float secondsPerRevolution = 0f;
        [SerializeField]
        [Tooltip("ONなら1秒ごとにカチッと進む。OFFは滑らかに回る")]
        bool tickPerSecond = true;
        [SerializeField]
        [Tooltip("ONなら時計回り、OFFなら反時計回り")]
        bool clockwise = true;

        [Header("残りわずか")]
        [SerializeField]
        [Tooltip("残りこの秒数以下で、時計全体を脈動させる。0なら何もしない")]
        float warnSeconds = 10f;
        [SerializeField]
        [Range(0f, 0.3f)]
        float warnPulse = 0.08f;

        GameFlow flow;
        Vector3 baseScale;

        void Awake() => baseScale = transform.localScale;

        void Update()
        {
            if (flow == null) flow = GameFlow.Current;
            if (flow == null) return;

            // 経過秒（待機中は0、発進後に進む）
            float elapsed = flow.TimeLimit - flow.Remaining;
            if (tickPerSecond) elapsed = Mathf.Floor(elapsed);

            float period = secondsPerRevolution > 0f ? secondsPerRevolution : Mathf.Max(0.01f, flow.TimeLimit);
            float angle = elapsed / period * 360f;
            if (secondHand != null) secondHand.localRotation = Quaternion.Euler(0f, 0f, clockwise ? -angle : angle);

            bool warn = warnSeconds > 0f && flow.Phase == GamePhase.Playing && flow.Remaining <= warnSeconds;
            float pulse = warn ? 1f + Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI)) * warnPulse : 1f;
            transform.localScale = baseScale * pulse;
        }
    }
}
