using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 敵船の移動AI。プレイヤーへ近づき、一定距離まで来たら周回する。
    /// 被弾の減速（ISlowable）に対応。沈没したら大砲を止めて少し後に消える。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ShipHealth))]
    public class EnemyShip : MonoBehaviour, ISlowable
    {
        // 実行時にPlayerTargetから自動取得する（Inspectorでは設定しない）
        Transform target;

        [Header("狙う相手")]
        [SerializeField]
        [Tooltip("この距離(m)まで近づいたら、周回に切り替える")]
        float preferredDistance = 25f;

        [Header("推進・操舵")]
        [SerializeField]
        float maxSpeed = 8f;
        [SerializeField]
        float timeToMaxSpeed = 3f;
        [SerializeField]
        float steerTorque = 600f;
        [SerializeField]
        [Tooltip("減速中、最高速度を超えている分を落とす強さ")]
        float slowBrake = 3f;
        [SerializeField]
        float lateralDamping = 4f;
        [SerializeField]
        float angularDamping = 2f;

        [Header("沈没")]
        [SerializeField]
        [Tooltip("沈没を始めてから消えるまでの時間(秒)")]
        float sinkDuration = 5f;
        [SerializeField]
        [Tooltip("沈む速さ(m/秒)。小さいほどゆっくり沈む")]
        float sinkSpeed = 0.8f;
        [SerializeField]
        [Tooltip("沈みながら傾く速さ(度/秒)。0なら傾かない")]
        float sinkRollSpeed = 6f;
        [SerializeField]
        [Tooltip("沈没の瞬間に出す爆発の演出（任意）。Assets/Urano/VFX のVfxAsset")]
        VfxAsset deathVfx;

        /// <summary>現在生きている敵船の一覧（カメラが一番近い敵を探すのに使う）</summary>
        public static readonly List<EnemyShip> Active = new List<EnemyShip>();

        public bool IsSunk => sunk;

        Rigidbody rb;
        ShipHealth health;
        EnemyCannon[] cannons;
        BoatBuoyancy buoyancy;
        Collider[] colliders;
        bool defaultUseGravity;
        float slowMultiplier = 1f;
        float slowEndTime;
        bool sunk;

        public void ApplySlow(float speedMultiplier, float duration)
        {
            slowMultiplier = Time.time < slowEndTime ? Mathf.Min(slowMultiplier, speedMultiplier) : speedMultiplier;
            slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            health = GetComponent<ShipHealth>();
            cannons = GetComponentsInChildren<EnemyCannon>();
            buoyancy = GetComponent<BoatBuoyancy>();
            colliders = GetComponentsInChildren<Collider>();
            defaultUseGravity = rb.useGravity;
        }

        // プールから再利用されるたびに状態を初期化する
        void OnEnable()
        {
            health.Died += OnDied;
            Active.Add(this);
            sunk = false;
            slowMultiplier = 1f;
            slowEndTime = 0f;
            foreach (EnemyCannon c in cannons) c.enabled = true;

            // 沈没で変えたものを元に戻す
            if (buoyancy != null) buoyancy.enabled = true;
            foreach (Collider c in colliders) c.enabled = true;
            rb.useGravity = defaultUseGravity;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        void OnDisable()
        {
            health.Died -= OnDied;
            Active.Remove(this);
            CancelInvoke();
        }

        void OnDied()
        {
            sunk = true;
            foreach (EnemyCannon c in cannons) c.enabled = false;

            // 浮力と当たり判定を切って、FixedUpdateで自前でゆっくり沈める
            if (buoyancy != null) buoyancy.enabled = false;
            foreach (Collider c in colliders) c.enabled = false;
            rb.useGravity = false;

            if (deathVfx != null) deathVfx.Play(transform.position);
            Invoke(nameof(Despawn), sinkDuration);
        }

        void Despawn() => PoolManager.Release(gameObject);

        // 沈没中：横の勢いを落としつつ一定速度で沈み、船の前後軸まわりに傾く
        void Sink()
        {
            Vector3 v = rb.linearVelocity;
            v.x *= 1f - 1.5f * Time.fixedDeltaTime;
            v.z *= 1f - 1.5f * Time.fixedDeltaTime;
            v.y = -sinkSpeed;
            rb.linearVelocity = v;

            rb.angularVelocity = transform.forward * (sinkRollSpeed * Mathf.Deg2Rad);
        }

        void FixedUpdate()
        {
            if (sunk)
            {
                Sink();
                return;
            }
            if (target == null)
            {
                if (PlayerTarget.Current == null) return;
                target = PlayerTarget.Current.transform;
            }

            float currentMax = maxSpeed * (Time.time < slowEndTime ? slowMultiplier : 1f);
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            // 推進／減速
            if (forwardSpeed > currentMax)
            {
                rb.AddForce(-transform.forward * ((forwardSpeed - currentMax) * slowBrake * rb.mass));
            }
            else
            {
                float accel = maxSpeed / Mathf.Max(0.01f, timeToMaxSpeed);
                rb.AddForce(transform.forward * (accel * rb.mass));
            }

            // 操舵：遠ければ相手へ、近ければ相手を横に見て周回
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            Vector3 dir = dist > 0.01f ? toTarget / dist : transform.forward;
            Vector3 desired = dist > preferredDistance ? dir : Vector3.Cross(Vector3.up, dir);

            float angle = Vector3.SignedAngle(transform.forward, desired, Vector3.up);
            float steer = Mathf.Clamp(angle / 45f, -1f, 1f);
            float speedFactor = Mathf.Clamp01(forwardSpeed / maxSpeed);
            rb.AddTorque(Vector3.up * (steer * steerTorque * Mathf.Max(0.3f, speedFactor)));

            // 横滑りと回頭のダンピング
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            localVel.x *= 1f - lateralDamping * Time.fixedDeltaTime;
            rb.linearVelocity = transform.TransformDirection(localVel);

            Vector3 av = rb.angularVelocity;
            av.y *= 1f - angularDamping * Time.fixedDeltaTime;
            rb.angularVelocity = av;
        }
    }
}
