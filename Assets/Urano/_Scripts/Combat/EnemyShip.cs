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

        [Header("カメラに映る努力（通常時）")]
        [SerializeField]
        [Tooltip("ONなら、画面の外にいるとき、カメラの前を横切るように走って画面に入る。映っている間は通常どおり近づいて周回する")]
        bool stayOnCamera = true;
        [SerializeField]
        [Tooltip("横切る位置の、カメラの向きに対してプレイヤーより前の距離(m)")]
        float crossAhead = 30f;
        [SerializeField]
        [Tooltip("横切る位置の、左右の距離(m)。画面の左右の端あたりになるように")]
        float crossLateral = 22f;
        [SerializeField]
        [Range(0f, 0.3f)]
        [Tooltip("画面の端からこの割合だけ内側に入っていたら『映っている』とみなす。端ぎりぎりで出入りしないための余白")]
        float onScreenInset = 0.08f;

        [Header("推進・操舵")]
        [SerializeField]
        float maxSpeed = 8f;
        [SerializeField]
        float timeToMaxSpeed = 3f;
        [SerializeField]
        float steerTorque = 600f;
        [SerializeField]
        [Min(1f)]
        [Tooltip("並走モード中の旋回力の倍率。速度が上がるぶん回頭が追いつかなくなるので、旋回トルクに掛ける")]
        float rushTurnBoost = 3f;
        [SerializeField]
        [Min(1f)]
        [Tooltip("並走モードで、横のずれがこの距離(m)以上あるとき、最大の45度で斜めに向かう。小さいほど素早く横へ寄る")]
        float rushLateralSettle = 15f;
        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("並走モードで、進行方向が目標と大きくずれているとき、速度の上限を何倍まで落とすか。小さいほど、曲がる間にしっかり減速する")]
        float rushTurnSlowest = 0.4f;
        [SerializeField]
        [Tooltip("減速中、最高速度を超えている分を落とす強さ")]
        float slowBrake = 3f;
        [SerializeField]
        float lateralDamping = 4f;
        [SerializeField]
        float angularDamping = 2f;

        [Header("転覆")]
        [SerializeField]
        [Tooltip("船体がこの角度(度)以上傾いたら自爆して沈む。0以下なら転覆しない")]
        float capsizeAngle = 90f;

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

        // 「まだ一度も敵がカメラに映っていない」ときに EnemyFirstContact が立てる。並走役以外の全員が、画面の中心付近へ向かう
        public static bool SeekingCamera { get; private set; }
        static Vector3 seekPoint;
        static float seekSpeedMultiplier = 1f;

        /// <summary>全ての敵に、カメラに映る位置(point)へ向かわせる。speedMultiplier=最高速度の倍率</summary>
        public static void BeginSeekCamera(Vector3 point, float speedMultiplier)
        {
            SeekingCamera = true;
            seekPoint = point;
            seekSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        }

        /// <summary>向かう先を更新する（カメラが動くので毎フレーム）</summary>
        public static void UpdateSeekPoint(Vector3 point) => seekPoint = point;

        public static void EndSeekCamera() => SeekingCamera = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic()
        {
            SeekingCamera = false;
            Active.Clear();
        }
        /// <summary>全速でプレイヤーの横へ並走しに行っている最中（EnemyFirstContact が指示する）</summary>
        public bool IsRushing => rushing;

        Rigidbody rb;
        ShipHealth health;
        EnemyCannon[] cannons;
        BoatBuoyancy buoyancy;
        Collider[] colliders;
        bool defaultUseGravity;
        float slowMultiplier = 1f;
        float slowEndTime;
        bool sunk;

        // 並走モード：プレイヤーの横（斜め前）の位置を目指して全速で追いつき、横に付いたら速度を合わせる
        bool rushing;
        float rushSpeedMultiplier;
        float rushLateral;
        float rushAhead;
        float rushSide = 1f;
        PlayerTarget rushPlayer;

        /// <summary>全速でプレイヤーの横に並走する。speedMultiplier=最高速度の倍率、lateral=横の距離(m)、ahead=プレイヤーより前に出る距離(m)</summary>
        public void BeginRush(float speedMultiplier, float lateral, float ahead)
        {
            PlayerTarget player = PlayerTarget.Current;
            if (player == null) return;

            rushing = true;
            rushPlayer = player;
            rushSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
            rushLateral = lateral;
            rushAhead = ahead;
            // 今いる側の横に付く（反対側へ回り込まない）
            rushSide = Vector3.Dot(transform.position - player.transform.position, player.transform.right) >= 0f ? 1f : -1f;
        }

        public void EndRush() => rushing = false;

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
            MoveVisualsToSilhouetteLayer();
        }

        // 見た目だけのオブジェクトを EnemyVisual レイヤーへ移す（隠れたときのシルエット描画の対象にする）。
        // 物理判定に影響しないよう、コライダーを持つオブジェクトは触らない
        void MoveVisualsToSilhouetteLayer()
        {
            int layer = LayerMask.NameToLayer("EnemyVisual");
            if (layer < 0) return;

            foreach (Renderer r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<Collider>() == null) r.gameObject.layer = layer;
            }
        }

        // プールから再利用されるたびに状態を初期化する
        void OnEnable()
        {
            health.Died += OnDied;
            Active.Add(this);
            sunk = false;
            rushing = false;
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

        // 操舵：遠ければ相手へ、近ければ相手を横に見て周回
        Vector3 NormalSteer()
        {
            if (stayOnCamera && TryCrossInFront(out Vector3 cross)) return cross;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            Vector3 dir = dist > 0.01f ? toTarget / dist : transform.forward;
            return dist > preferredDistance ? dir : Vector3.Cross(Vector3.up, dir);
        }

        // 画面の外にいるとき、カメラの前（プレイヤーの前方）を、今いる側とは反対の側へ横切る向きを返す。映っていればfalse
        bool TryCrossInFront(out Vector3 dir)
        {
            dir = default;
            Camera cam = Camera.main;
            if (cam == null || target == null) return false;

            Vector3 v = cam.WorldToViewportPoint(transform.position);
            float lo = onScreenInset, hi = 1f - onScreenInset;
            if (v.z > 0f && v.x > lo && v.x < hi && v.y > lo && v.y < hi) return false;

            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward = camForward.sqrMagnitude > 0.0001f ? camForward.normalized : Vector3.forward;
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight = camRight.normalized;

            // 今いる側（カメラから見て左右）の反対にある、プレイヤーの前方の地点を目指す＝前を横切る
            float side = Vector3.Dot(transform.position - target.position, camRight) >= 0f ? -1f : 1f;
            Vector3 spot = target.position + camForward * crossAhead + camRight * (side * crossLateral);
            Vector3 to = spot - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return false;

            dir = to.normalized;
            return true;
        }

        // 並走：目標地点（プレイヤーの横・斜め前）へ向かう。着く手前ではプレイヤーと同じ向きになり、速度もプレイヤーに合わせる
        Vector3 RushSteer(out float speedCap, out float accelScale)
        {
            Transform player = rushPlayer.transform;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = player.right;
            right.y = 0f;

            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

            // 目標地点（プレイヤーの横・斜め前）までの差を、プレイヤーの進行方向（前後）と横に分けて考える
            Vector3 spot = player.position + forward * rushAhead + right * (rushSide * rushLateral);
            Vector3 toSpot = spot - transform.position;
            toSpot.y = 0f;
            float forwardGap = Vector3.Dot(toSpot, forward); // +なら目標はまだ前（追いつく必要あり）、-なら行き過ぎ
            float lateralGap = Vector3.Dot(toSpot, right);   // 横方向のずれ

            // 前後：差が大きいほど速く、縮まるほどプレイヤーの速度へ収束。後ろに取り残されても、同じ向きに走って追いつく
            float playerSpeed = Mathf.Max(0f, Vector3.Dot(rushPlayer.Body.linearVelocity, forward));
            float wantSpeed = Mathf.Min(maxSpeed * rushSpeedMultiplier, playerSpeed + forwardGap * 0.8f);
            speedCap = Mathf.Max(wantSpeed, maxSpeed * 0.3f); // 先に行き過ぎたときも、舵が効く最低限の速さは保つ
            accelScale = rushSpeedMultiplier;

            // 左右：プレイヤーと同じ向きを基本に、横のずれの分だけ斜めに向ける。
            // 目標地点へ一直線に向かわない（後ろから追うと、プレイヤーの真後ろに付いてしまう）
            float lean = Mathf.Clamp(lateralGap / rushLateralSettle, -1f, 1f);
            return (forward + right * lean).normalized;
        }

        void FixedUpdate()
        {
            if (sunk)
            {
                Sink();
                return;
            }

            // 傾きすぎ（±90度など）で転覆。Died → OnDied で通常の沈没・爆発になる
            if (capsizeAngle > 0f && Vector3.Angle(transform.up, Vector3.up) >= capsizeAngle)
            {
                health.Kill();
                return;
            }

            if (target == null)
            {
                if (PlayerTarget.Current == null) return;
                target = PlayerTarget.Current.transform;
            }

            Vector3 desired;
            float speedCap = maxSpeed;
            float accelScale = 1f;
            bool rush = rushing && rushPlayer != null;
            if (rush)
            {
                desired = RushSteer(out speedCap, out accelScale);
                // 向きが目標から大きくずれている間は減速して、小さく回る（速いまま回ると旋回半径が大きくて通り過ぎる）
                float misalign = Mathf.Abs(Vector3.SignedAngle(transform.forward, desired, Vector3.up)) / 90f;
                speedCap *= Mathf.Lerp(1f, rushTurnSlowest, Mathf.Clamp01(misalign));
            }
            else if (SeekingCamera)
            {
                // 画面の中心付近へ向かう。着くまで周回しない
                Vector3 to = seekPoint - transform.position;
                to.y = 0f;
                desired = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                speedCap = maxSpeed * seekSpeedMultiplier;
                accelScale = seekSpeedMultiplier;
            }
            else desired = NormalSteer();

            float currentMax = speedCap * (Time.time < slowEndTime ? slowMultiplier : 1f);
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            // 推進／減速
            if (forwardSpeed > currentMax)
            {
                rb.AddForce(-transform.forward * ((forwardSpeed - currentMax) * slowBrake * rb.mass));
            }
            else
            {
                float accel = maxSpeed * accelScale / Mathf.Max(0.01f, timeToMaxSpeed);
                rb.AddForce(transform.forward * (accel * rb.mass));
            }

            float angle = Vector3.SignedAngle(transform.forward, desired, Vector3.up);
            float steer = Mathf.Clamp(angle / 45f, -1f, 1f);
            float speedFactor = Mathf.Clamp01(forwardSpeed / maxSpeed);
            float torque = steerTorque * (rush ? rushTurnBoost : 1f);
            rb.AddTorque(Vector3.up * (steer * torque * Mathf.Max(0.3f, speedFactor)));

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
