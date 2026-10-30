using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 吹っ飛ばされたプレイヤーを、飛ぶ方向の斜め横の決まった位置から目で追うカメラ
    /// CameraManager から SetTarget で追いかける Transform を受け取る
    /// </summary>
    [DefaultExecutionOrder(200)]   // BodyRig が体を動かした後にカメラを合わせる
    [RequireComponent(typeof(Camera))]
    public class ClashCamera : MonoBehaviour
    {
        [SerializeField] private float distance    = 4f;    // 飛ぶ方向の横から見る距離
        [SerializeField] private float back        = 1.5f;  // 飛んでくる側（斬った側）へずらす距離
        [SerializeField] private float height      = 1f;    // 体より上にずらす高さ
        [SerializeField] private float followSpeed = 8f;    // 向きを追いかける速さ（大きいほどぴったり付いていく）

        Transform target;
        Vector3 offset;

        // target: 吹っ飛ぶ体。吹っ飛び始めの向きの後ろへ飛ぶので、その向きで見る方向を決めて固定する
        public void SetTarget(Transform target)
        {
            this.target = target;
            if (target == null) return;

            Vector3 fly = Vector3.ProjectOnPlane(-target.forward, Vector3.up);
            fly = fly.sqrMagnitude > 0.0001f ? fly.normalized : Vector3.back;
            Vector3 side = Vector3.Cross(fly, Vector3.up);
            offset = side * distance - fly * back + Vector3.up * height;

            transform.position = target.position + offset;
            transform.LookAt(target.position);
        }

        // 位置は SetTarget で置いたまま固定し、向きだけ体の方へ回して追いかける
        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 dir = target.position - transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;

            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);   // ヒットストップ・スローに合わせてゲーム内時間で追う
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), k);
        }
    }
}
