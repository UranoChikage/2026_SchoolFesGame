using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 砲そのもの。クールタイム管理と、放物線を描く砲弾の発射を担当する。
    /// 誰がどう狙うか（JoyCon／キーボード／マウス）は CannonStation が決める。
    /// </summary>
    public class CannonShooter : MonoBehaviour
    {
        public enum LaunchMode
        {
            [Tooltip("角度を固定し、狙った位置に届く速度を求める（弾速は上限になる）")]
            FixedAngle,
            [Tooltip("弾速を固定し、狙った位置に届く角度を求める（届かなければ最大射程まで飛ぶ）")]
            FixedSpeed,
        }

        [Header("参照")]
        [SerializeField]
        [Tooltip("砲弾の発射位置（砲口）。未設定ならこのオブジェクトの位置")]
        Transform muzzle;
        [SerializeField]
        [Tooltip("自分の船のルート（砲弾が自船に当たらないようにする）")]
        Transform owner;
        [SerializeField]
        [Tooltip("水面の基準。設定すると水面に着弾して爆発する")]
        BoatBuoyancy water;

        [Header("砲弾")]
        [SerializeField]
        [Tooltip("砲弾のPrefab（Cannonball付き）。未設定なら球を自動生成")]
        Cannonball projectilePrefab;
        [SerializeField]
        [Tooltip("着弾時の爆発の演出（任意）。Assets/Urano/VFX のVfxAsset")]
        VfxAsset explosionVfx;
        [SerializeField]
        [Tooltip("砲弾の見た目の大きさ（Prefab未設定時）")]
        float fallbackBallSize = 0.5f;

        [Header("弾道")]
        [SerializeField]
        [Tooltip("狙った位置へ撃つときの弾道の求め方")]
        LaunchMode mode = LaunchMode.FixedAngle;
        [SerializeField]
        [Tooltip("弾速(m/s)。FixedSpeedでは発射速度、FixedAngleでは速度の上限")]
        float projectileSpeed = 40f;
        [SerializeField]
        [Tooltip("発射角度(度)。FixedAngleで使う。のちにSwitchの角度から設定する")]
        [Range(5f, 85f)] float launchAngle = 45f;
        [SerializeField]
        [Tooltip("FixedSpeedで届く角度が2つあるとき、高い山なりの弾道を使う")]
        bool highArc = false;

        [Header("攻撃")]
        [SerializeField]
        [Tooltip("着弾時の爆発の影響範囲（半径m）")]
        float explosionRadius = 4f;
        [SerializeField]
        float damage = 10f;
        [SerializeField]
        [Tooltip("砲弾が当たる／爆発が影響するレイヤー（自分の船は外す）")]
        LayerMask hitMask = ~0;
        [SerializeField]
        [Tooltip("TryFireAtで、これより近い位置は撃てない(m)")]
        float minRange = 2f;

        [Header("クールタイム")]
        [SerializeField]
        float cooldown = 1.5f;

        float nextFireTime;
        // 全砲で1つを共有（砲ごとに作ると、元Prefabもプールも別々になる）
        static Cannonball fallbackPrefab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => fallbackPrefab = null;

        /// <summary>弾速(m/s)</summary>
        public float ProjectileSpeed => projectileSpeed;

        /// <summary>発射角度(度)。TryFireAtのFixedAngle用</summary>
        public float LaunchAngle
        {
            get => launchAngle;
            set => launchAngle = Mathf.Clamp(value, 5f, 85f);
        }

        /// <summary>クールタイムの残り割合（0=撃てる、1=撃った直後）。UI用</summary>
        public float CooldownRatio => cooldown <= 0f ? 0f : Mathf.Clamp01((nextFireTime - Time.time) / cooldown);
        public bool IsReady => Time.time >= nextFireTime;

        Transform Muzzle => muzzle != null ? muzzle : transform;

        /// <summary>砲口の位置</summary>
        public Vector3 MuzzlePosition => Muzzle.position;
        /// <summary>爆発の半径(m)。着弾予測マーカーの大きさに使う</summary>
        public float ExplosionRadius => explosionRadius;

        /// <summary>指定位置に撃ったときの初速。弾道の予測線用。撃てない位置ならfalse</summary>
        public bool TryGetLaunchVelocity(Vector3 target, out Vector3 velocity)
        {
            return TrySolveVelocity(Muzzle.position, target, out velocity);
        }

        /// <summary>指定位置に撃ったときの滞空時間(秒)。撃てない位置ならfalse</summary>
        public bool TryGetFlightTime(Vector3 target, out float time)
        {
            time = 0f;
            if (!TrySolveVelocity(Muzzle.position, target, out Vector3 velocity)) return false;

            Vector3 flat = target - Muzzle.position;
            flat.y = 0f;
            float horizontalSpeed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
            if (horizontalSpeed < 0.01f) return false;

            time = flat.magnitude / horizontalSpeed;
            return true;
        }

        /// <summary>指定した初速で発射する。撃てたらtrue</summary>
        bool TryFire(Vector3 velocity)
        {
            if (!IsReady) return false;

            Vector3 origin = Muzzle.position;
            Cannonball ball = PoolManager.Spawn(projectilePrefab != null ? projectilePrefab : FallbackPrefab, origin, Quaternion.identity);
            BoatBuoyancy surface = water != null ? water : PlayerTarget.Current?.Water; // 未設定ならプレイヤー船の水面
            ball.Launch(velocity, explosionRadius, damage, hitMask, surface, owner, explosionVfx, 15f);

            nextFireTime = Time.time + cooldown;
            return true;
        }

        /// <summary>指定した位置に着弾するよう弾道を計算して発射する。撃てたらtrue</summary>
        public bool TryFireAt(Vector3 target)
        {
            if (!IsReady) return false;
            return TrySolveVelocity(Muzzle.position, target, out Vector3 velocity) && TryFire(velocity);
        }

        /// <summary>Prefab未設定時に使う球。プールの元Prefabとして1つだけ作って使い回す</summary>
        Cannonball FallbackPrefab
        {
            get
            {
                if (fallbackPrefab == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "Cannonball";
                    Destroy(go.GetComponent<Collider>());
                    go.transform.localScale = Vector3.one * fallbackBallSize;
                    fallbackPrefab = go.AddComponent<Cannonball>();
                    go.SetActive(false);
                }
                return fallbackPrefab;
            }
        }

        bool TrySolveVelocity(Vector3 origin, Vector3 target, out Vector3 velocity)
        {
            velocity = default;

            Vector3 flat = target - origin;
            float dy = flat.y;
            flat.y = 0f;
            float dx = flat.magnitude;
            if (dx < minRange) return false;
            Vector3 dir = flat / dx;

            float g = Mathf.Abs(Physics.gravity.y);
            float angle;
            float speed;

            if (mode == LaunchMode.FixedAngle)
            {
                // 角度固定：  v^2 = g*dx^2 / (2*cos^2(θ) * (dx*tanθ - dy))
                angle = launchAngle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float denom = 2f * cos * cos * (dx * Mathf.Tan(angle) - dy);
                if (denom <= 0f) return false; // この角度では届かない高さ
                speed = Mathf.Min(Mathf.Sqrt(g * dx * dx / denom), projectileSpeed);
            }
            else
            {
                // 弾速固定：  tanθ = (v^2 ± sqrt(v^4 - g*(g*dx^2 + 2*dy*v^2))) / (g*dx)
                speed = projectileSpeed;
                float v2 = speed * speed;
                float disc = v2 * v2 - g * (g * dx * dx + 2f * dy * v2);
                if (disc < 0f)
                {
                    angle = 45f * Mathf.Deg2Rad; // 届かない：最大射程の角度で撃つ
                }
                else
                {
                    float root = Mathf.Sqrt(disc);
                    angle = Mathf.Atan((v2 + (highArc ? root : -root)) / (g * dx));
                }
            }

            velocity = dir * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
            return true;
        }
    }
}
