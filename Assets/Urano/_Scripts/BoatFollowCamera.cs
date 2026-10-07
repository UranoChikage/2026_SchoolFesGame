using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 船を後方上空から見下ろす三人称カメラ。
    /// 回転（ヨー）は遅れて追従し、全体を見渡せるようにする。波によるピッチ／ロールは無視する。
    /// </summary>
    [ExecuteAlways]
    public class BoatFollowCamera : MonoBehaviour
    {
        [Header("追従対象")]
        [SerializeField]
        [Tooltip("追従する船のTransform")]
        Transform target;

        [Header("配置")]
        [SerializeField]
        [Tooltip("船からカメラまでの水平距離(m)。大きいほど広く見渡せる")]
        float distance = 18f;
        [SerializeField]
        [Tooltip("カメラの高さ(m)")]
        float height = 12f;
        [SerializeField]
        [Tooltip("注視点の船からのオフセット（前方がプラス）。船の少し先を見ることで進行方向が見やすくなる")]
        Vector3 lookOffset = new Vector3(0f, 1f, 4f);

        [Header("追従の遅さ")]
        [SerializeField]
        [Tooltip("船の向きへ回り込むまでの時間(秒)。大きいほどゆっくり回転する")]
        float rotationSmoothTime = 1.5f;
        [SerializeField]
        [Tooltip("回り込みの最大速度(度/秒)。急旋回でも画面が振り回されないようにする")]
        float maxRotationSpeed = 40f;
        [SerializeField]
        [Tooltip("位置追従の遅れ(秒)")]
        float positionSmoothTime = 0.25f;

        float yaw;
        float yawVelocity;
        Vector3 focus;
        Vector3 focusVelocity;

        void Start()
        {
            if (target == null) return;
            yaw = target.eulerAngles.y;
            focus = target.position;
            Apply();
        }

        // 再生していないときはスムージングなしで即座に配置する（エディタプレビュー用）
        void OnValidate()
        {
            if (!Application.isPlaying) SnapToTarget();
        }

        void SnapToTarget()
        {
            if (target == null) return;
            yaw = target.eulerAngles.y;
            focus = target.position;
            yawVelocity = 0f;
            focusVelocity = Vector3.zero;
            Apply();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                SnapToTarget();
                return;
            }
            if (target == null) return;

            yaw = Mathf.SmoothDampAngle(yaw, target.eulerAngles.y, ref yawVelocity,
                rotationSmoothTime, maxRotationSpeed);
            focus = Vector3.SmoothDamp(focus, target.position, ref focusVelocity, positionSmoothTime);
            Apply();
        }

        void Apply()
        {
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            transform.position = focus + yawRot * new Vector3(0f, height, -distance);
            transform.rotation = Quaternion.LookRotation(
                focus + yawRot * lookOffset - transform.position, Vector3.up);
        }
    }
}
