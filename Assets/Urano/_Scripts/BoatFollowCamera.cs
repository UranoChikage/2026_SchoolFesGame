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

        [Header("一番近い敵も映す")]
        [SerializeField]
        [Tooltip("注視点を自船から一番近い敵へどれだけ寄せるか（0=自船のみ、0.5=自船と敵の中間、1=敵）")]
        [Range(0f, 1f)] float enemyWeight = 0.35f;
        [SerializeField]
        [Tooltip("この距離(m)より遠い敵は無視する")]
        float enemyMaxDistance = 80f;
        [SerializeField]
        [Tooltip("敵との距離1mにつきカメラを引く量(m)。寄せた分、自船が画面外に出にくくする")]
        float extraDistancePerMeter = 0.15f;
        [SerializeField]
        [Tooltip("引く量の上限(m)")]
        float maxExtraDistance = 12f;
        [SerializeField]
        [Tooltip("注視点・引き具合が切り替わるときの滑らかさ(秒)。敵が入れ替わっても画面が飛ばない")]
        float enemySmoothTime = 0.6f;

        float yaw;
        float yawVelocity;
        Vector3 focus;
        Vector3 focusVelocity;
        Vector3 enemyOffset;      // 自船から敵方向へずらす量（平滑化済み）
        Vector3 enemyOffsetVelocity;
        float extraDistance;
        float extraDistanceVelocity;

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
            enemyOffset = Vector3.zero;
            enemyOffsetVelocity = Vector3.zero;
            extraDistance = 0f;
            extraDistanceVelocity = 0f;
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

            // 一番近い敵の方向へ注視点をずらし、そのぶんカメラも引く
            Vector3 wantOffset = Vector3.zero;
            float wantExtra = 0f;
            if (TryFindNearestEnemy(out Vector3 enemyPos, out float enemyDist))
            {
                wantOffset = (enemyPos - target.position) * enemyWeight;
                wantOffset.y = 0f;
                wantExtra = Mathf.Min(enemyDist * extraDistancePerMeter, maxExtraDistance) * enemyWeight;
            }
            enemyOffset = Vector3.SmoothDamp(enemyOffset, wantOffset, ref enemyOffsetVelocity, enemySmoothTime);
            extraDistance = Mathf.SmoothDamp(extraDistance, wantExtra, ref extraDistanceVelocity, enemySmoothTime);

            Apply();
        }

        bool TryFindNearestEnemy(out Vector3 position, out float distance)
        {
            position = default;
            distance = 0f;
            float best = enemyMaxDistance * enemyMaxDistance;
            bool found = false;

            foreach (EnemyShip e in EnemyShip.Active)
            {
                if (e == null || e.IsSunk) continue;
                Vector3 d = e.transform.position - target.position;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    position = e.transform.position;
                    distance = Mathf.Sqrt(sqr);
                    found = true;
                }
            }
            return found;
        }

        void Apply()
        {
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 center = focus + enemyOffset;
            transform.position = center + yawRot * new Vector3(0f, height + extraDistance * 0.6f, -(distance + extraDistance));
            transform.rotation = Quaternion.LookRotation(
                center + yawRot * lookOffset - transform.position, Vector3.up);
        }
    }
}
