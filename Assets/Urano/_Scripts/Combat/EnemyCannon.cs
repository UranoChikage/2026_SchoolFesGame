using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 敵の大砲のAI。プレイヤー船の進行方向を先読み（偏差）した着弾予測点にマーカーを立て、
    /// chargeTime秒後にその地点へ発射する。予測点は立てた時点で固定なので、プレイヤーは避けられる。
    /// カメラに映っていない砲は撃たない。
    /// </summary>
    [RequireComponent(typeof(CannonShooter))]
    public class EnemyCannon : MonoBehaviour
    {
        // 実行時にPlayerTargetから自動取得する（Inspectorでは設定しない）
        Rigidbody target;
        BoatBuoyancy water;

        [Header("攻撃")]
        [SerializeField]
        [Tooltip("狙い始めてから発射するまでの時間(秒)")]
        float chargeTime = 2f;
        [SerializeField]
        [Tooltip("この距離(m)より遠いと撃たない")]
        float maxRange = 60f;
        [SerializeField]
        [Tooltip("偏差の強さ（0=相手の現在位置、1=完全な先読み）")]
        [Range(0f, 1f)] float leadFactor = 0.8f;
        [SerializeField]
        [Tooltip("予測点にばらつきを与える半径(m)。大きいほど外れやすい")]
        float aimError = 2f;

        [Header("カメラ判定")]
        [SerializeField]
        [Tooltip("画面端からこの割合(0〜0.5)内側に入っていないと「映っている」とみなさない")]
        [Range(0f, 0.5f)] float screenMargin = 0.02f;

        [Header("着弾予測マーカー")]
        [SerializeField]
        [Tooltip("マーカーのPrefab（任意）。未設定なら赤い円盤を自動生成。直径1m・Y厚みの薄い形を想定")]
        GameObject markerPrefab;
        [SerializeField]
        [Tooltip("狙っている間、砲口から予測点までの放物線を表示する")]
        bool showTrajectory = true;
        [SerializeField]
        Color trajectoryColor = new Color(1f, 0.25f, 0.2f, 0.85f);
        [SerializeField]
        [Tooltip("マーカーを水面からどれだけ浮かせるか(m)。水面に埋もれて見えないとき大きくする")]
        float markerHeightOffset = 0.3f;
        [SerializeField]
        [Tooltip("発射直前に向けてマーカーが縮む倍率の開始値")]
        float markerStartScale = 1.6f;

        CannonShooter shooter;
        TrajectoryLine trajectory;
        // 全敵で1つを共有（敵ごとに作ると、元Prefabもプールも別々になって無駄）
        static GameObject fallbackMarker;
        GameObject marker;
        Vector3 aimPoint;
        float fireTime;
        bool charging;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => fallbackMarker = null;

        void Awake()
        {
            shooter = GetComponent<CannonShooter>();
            if (showTrajectory)
            {
                trajectory = GetComponent<TrajectoryLine>();
                if (trajectory == null) trajectory = gameObject.AddComponent<TrajectoryLine>();
                trajectory.SetColor(trajectoryColor);
            }
        }

        void OnDisable() => CancelCharge();

        void Update()
        {
            if (target == null)
            {
                // 未設定ならプレイヤー船を自動取得（Prefab化した敵を大量に出すため）
                PlayerTarget player = PlayerTarget.Current;
                if (player == null) return;
                target = player.Body;
                if (water == null) water = player.Water;
            }

            if (charging)
            {
                UpdateCharge();
            }
            else if (shooter.IsReady && IsOnScreen(transform.position) && InRange())
            {
                BeginCharge();
            }
        }

        bool InRange()
        {
            Vector3 d = target.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= maxRange * maxRange;
        }

        void BeginCharge()
        {
            if (!TryPredictPoint(out aimPoint)) return;

            charging = true;
            fireTime = Time.time + chargeTime;

            float diameter = shooter.ExplosionRadius * 2f;
            // 水面と重なって隠れないよう、少し浮かせて表示する
            Vector3 markerPos = aimPoint + Vector3.up * markerHeightOffset;
            marker = PoolManager.Spawn(markerPrefab != null ? markerPrefab : FallbackMarker, markerPos, Quaternion.identity);
            marker.transform.localScale = new Vector3(diameter, marker.transform.localScale.y, diameter);
        }

        void UpdateCharge()
        {
            // 撃つ直前にカメラ外へ出ていたら中止（撃たない）
            if (!IsOnScreen(transform.position))
            {
                CancelCharge();
                return;
            }

            // 砲口は船と一緒に動くので、予測線は毎フレーム撃ち直した弾道で描く（着弾点は固定）
            if (trajectory != null && shooter.TryGetLaunchVelocity(aimPoint, out Vector3 velocity))
            {
                trajectory.Show(shooter.MuzzlePosition, velocity, aimPoint.y, true);
            }

            float remaining = fireTime - Time.time;
            if (remaining <= 0f)
            {
                shooter.TryFireAt(aimPoint);
                CancelCharge();
                return;
            }

            // 発射が近づくほどマーカーが縮んで、避ける目安になる
            float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, chargeTime));
            float diameter = shooter.ExplosionRadius * 2f * Mathf.Lerp(markerStartScale, 1f, progress);
            Vector3 s = marker.transform.localScale;
            marker.transform.localScale = new Vector3(diameter, s.y, diameter);
        }

        void CancelCharge()
        {
            charging = false;
            if (trajectory != null) trajectory.Hide();
            if (marker != null)
            {
                PoolManager.Release(marker);
                marker = null;
            }
        }

        /// <summary>chargeTime＋砲弾の滞空時間ぶん先のプレイヤー位置を予測する</summary>
        bool TryPredictPoint(out Vector3 point)
        {
            Vector3 velocity = target.linearVelocity;
            velocity.y = 0f;

            Vector3 predicted = target.position;
            float flight = 0f;
            // 滞空時間は着弾点で変わるので数回回して収束させる
            for (int i = 0; i < 3; i++)
            {
                if (!shooter.TryGetFlightTime(predicted, out flight)) { point = default; return false; }
                predicted = target.position + velocity * ((chargeTime + flight) * leadFactor);
            }

            Vector2 error = Random.insideUnitCircle * aimError;
            predicted += new Vector3(error.x, 0f, error.y);
            predicted.y = water != null ? water.waterY : target.position.y;

            // 予測点が射程外・撃てない位置ならこの回は撃たない
            point = predicted;
            return shooter.TryGetFlightTime(predicted, out _);
        }

        bool IsOnScreen(Vector3 worldPos)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 v = cam.WorldToViewportPoint(worldPos);
            return v.z > 0f
                && v.x > screenMargin && v.x < 1f - screenMargin
                && v.y > screenMargin && v.y < 1f - screenMargin;
        }

        /// <summary>Prefab未設定時の赤い円盤。プールの元Prefabとして1つだけ作って使い回す</summary>
        static GameObject FallbackMarker
        {
            get
            {
                if (fallbackMarker == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "AimMarker";
                    Destroy(go.GetComponent<Collider>());
                    go.transform.localScale = new Vector3(1f, 0.05f, 1f);

                    var rend = go.GetComponent<Renderer>();
                    rend.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.1f, 0.1f, 0.45f), renderQueue = 3100 }; // 水面より後に描く
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    go.SetActive(false);
                    fallbackMarker = go;
                }
                return fallbackMarker;
            }
        }
    }
}
