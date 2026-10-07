using UnityEngine;

namespace Pirates
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatController : MonoBehaviour
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

        [Header("操舵")]
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


        public float ForwardSpeed => Vector3.Dot(rb.linearVelocity, transform.forward);
        public float MaxSpeed => maxSpeed;// 外部参照用プロパティ
        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (input == null || !input.IsConnected) return;

            float steer = input.RudderNormalized;     // -1〜+1
    
            // 推進
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            if (forwardSpeed < maxSpeed)
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
            Vector3 torque = transform.up * (steer * steerTorque * speedFactor);
            rb.AddTorque(torque);

            // 横滑り抑制：船体に対して横方向の速度成分を減衰
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            localVel.x *= 1f - lateralDamping * Time.fixedDeltaTime;
            rb.linearVelocity = transform.TransformDirection(localVel);

            // 角速度ダンピング
            Vector3 av = rb.angularVelocity;
            av.y *= 1f - angularDamping * Time.fixedDeltaTime;
            rb.angularVelocity = av;
        }
    }
}
