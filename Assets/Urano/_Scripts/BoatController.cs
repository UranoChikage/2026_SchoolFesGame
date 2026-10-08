using UnityEngine;

namespace Pirates
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatController : MonoBehaviour, ISlowable
    {
        [Header("入力ソース")]
        [SerializeField]
        [Tooltip("舵入力を取得するJoyConRudder（JoyCon／PCキーボード両対応）")]
        JoyConRudder input;

        [Header("推進")]
        [SerializeField]
        [Tooltip("推進力をかける位置（船尾のTransform）。未設定なら船体後方を自動使用")]
        Transform thrustPoint;
        [SerializeField]
        [Tooltip("thrustPoint未設定時、ローカル後方にどれだけオフセットするか(m)")]
        float thrustOffsetBack = 0.0f;
        
        [SerializeField]
        [Tooltip("最高速度に達するまでの時間(秒)。常時前進し、舵取りのみで操作する")]
        float timeToMaxSpeed = 2f;
        [SerializeField]
        [Tooltip("最高速度（m/s）")]
        float maxSpeed = 12f;

        [Header("操舵（回頭速度で指定：曲がりやすい）")]
        [SerializeField]
        [Tooltip("ONなら舵角に応じた目標の回頭速度へ素直に追従する（扱いやすい）。OFFなら従来のトルク方式")]
        bool directTurn = true;
        [SerializeField]
        [Tooltip("舵が最大のときの回頭速度(度/秒)")]
        float maxTurnRate = 55f;
        [SerializeField]
        [Tooltip("目標の回頭速度に追いつく速さ。大きいほどキビキビ曲がる")]
        float turnResponse = 4f;
        [SerializeField]
        [Tooltip("低速でも最低これだけは舵が効く(0〜1)。止まりかけでも曲がれる")]
        [Range(0f, 1f)] float minSteerEffect = 0.4f;

        [Header("操舵（トルク方式）")]
        [SerializeField]
        [Tooltip("舵角(-1〜+1)に対する旋回トルクの倍率")]
        float steerTorque = 800f;
        [SerializeField]
        [Tooltip("速度が低いと舵が効きにくくなる係数（0で常時最大効力、1で速度比例）")]
        [Range(0f, 1f)] public float speedSteerCoupling = 0.6f;

        [Header("水中ダンピング")]
        [SerializeField]
        [Tooltip("横滑り抑制（横方向の速度を減衰）")]
        float lateralDamping = 4f;
        [SerializeField]
        [Tooltip("回頭の収束（角速度を減衰）")]
        float angularDamping = 2f;
        [SerializeField]
        [Tooltip("減速中、最高速度を超えている分を落とす強さ")]
        float slowBrake = 3f;


        public float ForwardSpeed => Vector3.Dot(rb.linearVelocity, transform.forward);
        public float MaxSpeed => maxSpeed;// 外部参照用プロパティ
        Rigidbody rb;
        float slowMultiplier = 1f;
        float slowEndTime;

        public void ApplySlow(float speedMultiplier, float duration)
        {
            slowMultiplier = Time.time < slowEndTime ? Mathf.Min(slowMultiplier, speedMultiplier) : speedMultiplier;
            slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (input == null || !input.IsConnected) return;

            float steer = input.RudderNormalized;     // -1〜+1
    
            // 推進
            float currentMax = maxSpeed * (Time.time < slowEndTime ? slowMultiplier : 1f);
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            if (forwardSpeed > currentMax)
            {
                // 減速中：最高速度を超えている分をブレーキで落とす
                rb.AddForce(-transform.forward * ((forwardSpeed - currentMax) * slowBrake * rb.mass));
            }
            else
            {
                // 質量に依らず timeToMaxSpeed 秒で最高速になる加速度
                float accel = maxSpeed / Mathf.Max(0.01f, timeToMaxSpeed);
                Vector3 thrust = transform.forward * (accel * rb.mass);
                Vector3 pos = thrustPoint != null
                    ? thrustPoint.position
                    : transform.TransformPoint(new Vector3(0f, 0f, -thrustOffsetBack));
                rb.AddForceAtPosition(thrust, pos);
            }

            // 操舵：速度が乗っているほど舵が効く
            float speedFactor = Mathf.Lerp(1f, Mathf.Clamp01(forwardSpeed / maxSpeed), speedSteerCoupling);
            if (!directTurn)
            {
                rb.AddTorque(transform.up * (steer * steerTorque * speedFactor));
            }

            // 横滑り抑制：船体に対して横方向の速度成分を減衰
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            localVel.x *= 1f - lateralDamping * Time.fixedDeltaTime;
            rb.linearVelocity = transform.TransformDirection(localVel);

            // 角速度ダンピング
            Vector3 av = rb.angularVelocity;
            if (directTurn)
            {
                // 舵角 → 目標の回頭速度（波によるロール／ピッチは触らずヨーだけ制御）
                float effect = Mathf.Max(minSteerEffect, Mathf.Clamp01(forwardSpeed / maxSpeed));
                float targetYawRate = steer * maxTurnRate * effect * Mathf.Deg2Rad;
                av.y = Mathf.Lerp(av.y, targetYawRate, 1f - Mathf.Exp(-turnResponse * Time.fixedDeltaTime));
            }
            else
            {
                av.y *= 1f - angularDamping * Time.fixedDeltaTime;
            }
            rb.angularVelocity = av;
        }
    }
}
