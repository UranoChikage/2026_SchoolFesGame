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

        [Header("沈没時")]
        [SerializeField]
        [Tooltip("自船が沈没したときに追加で引く距離(m)")]
        float sunkExtraDistance = 10f;
        [SerializeField]
        [Tooltip("沈没時のカメラ移動の滑らかさ(秒)")]
        float sunkSmoothTime = 1.5f;

        [Header("速度感の演出")]
        [SerializeField]
        [Tooltip("最高速度のときに広げる視野角(度)。0なら変えない")]
        float speedFovBoost = 8f;
        [SerializeField]
        [Tooltip("視野角が変わるときの滑らかさ(秒)")]
        float fovSmoothTime = 0.4f;
        [SerializeField]
        [Tooltip("最高速度のときのカメラの揺れ幅(m)。0なら揺らさない")]
        float speedShake = 0.05f;

        // ビームなどの演出用（外から BeginEffect / EndEffect で指定）
        public static BoatFollowCamera Current { get; private set; }
        int effectCount;
        float effectZoom;
        float effectShake;
        float effectExtra;
        float effectExtraVelocity;
        float shakeAmount;
        float shakeVelocity;

        Camera cam;
        float baseFov;
        float fovBoost;
        float fovBoostVelocity;
        float yaw;
        float yawVelocity;
        Vector3 focus;
        Vector3 focusVelocity;
        Vector3 enemyOffset;      // 自船から敵方向へずらす量（平滑化済み）
        Vector3 enemyOffsetVelocity;
        float extraDistance;
        float extraDistanceVelocity;
        BoatController targetBoat;
        float sunkExtra;
        float sunkExtraVelocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>演出開始：カメラを zoomOut(m) 引き、shake(m) 揺らす。複数同時に呼べる（全部終わるまで続く）</summary>
        public void BeginEffect(float zoomOut, float shake)
        {
            effectCount++;
            effectZoom = Mathf.Max(effectZoom, zoomOut);
            effectShake = Mathf.Max(effectShake, shake);
        }

        /// <summary>BeginEffect と対で呼ぶ。全て終わったらカメラが元に戻る</summary>
        public void EndEffect()
        {
            effectCount = Mathf.Max(0, effectCount - 1);
            if (effectCount == 0)
            {
                effectZoom = 0f;
                effectShake = 0f;
            }
        }

        void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;
            if (target == null) return;
            targetBoat = target.GetComponent<BoatController>();
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
            sunkExtra = 0f;
            sunkExtraVelocity = 0f;
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

            float wantSunk = targetBoat != null && targetBoat.IsSunk ? sunkExtraDistance : 0f;
            sunkExtra = Mathf.SmoothDamp(sunkExtra, wantSunk, ref sunkExtraVelocity, sunkSmoothTime);

            effectExtra = Mathf.SmoothDamp(effectExtra, effectZoom, ref effectExtraVelocity, 0.35f);
            shakeAmount = Mathf.SmoothDamp(shakeAmount, effectShake, ref shakeVelocity, 0.15f);

            Apply();
            ApplySpeedEffects();
            ApplyEffectShake();
        }

        // 速度に応じて視野角を広げ、わずかに揺らして「速く走っている」感じを出す
        void ApplySpeedEffects()
        {
            if (targetBoat == null) return;
            float speed01 = targetBoat.IsSunk ? 0f : Mathf.Clamp01(targetBoat.ForwardSpeed / Mathf.Max(0.01f, targetBoat.MaxSpeed));

            if (cam != null && speedFovBoost > 0f)
            {
                fovBoost = Mathf.SmoothDamp(fovBoost, speedFovBoost * speed01, ref fovBoostVelocity, fovSmoothTime);
                cam.fieldOfView = baseFov + fovBoost;
            }

            if (speedShake > 0f)
            {
                float t = Time.time * 18f;
                var shake = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f);
                transform.position += transform.rotation * (shake * (2f * speedShake * speed01 * speed01));
            }
        }

        void ApplyEffectShake()
        {
            if (shakeAmount <= 0.001f) return;
            float t = Time.time * 28f;
            var shake = new Vector3(Mathf.PerlinNoise(t, 3f) - 0.5f, Mathf.PerlinNoise(7f, t) - 0.5f, 0f);
            transform.position += transform.rotation * (shake * (2f * shakeAmount));
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
            transform.position = center + yawRot * new Vector3(0f, height + (extraDistance + sunkExtra + effectExtra) * 0.6f, -(distance + extraDistance + sunkExtra + effectExtra));
            transform.rotation = Quaternion.LookRotation(
                center + yawRot * lookOffset - transform.position, Vector3.up);
        }
    }
}
