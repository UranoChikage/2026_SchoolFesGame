using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// ゲーム開始から一定時間たっても、敵が一度もカメラに映っていなければ、近い敵を全速力でプレイヤーの横に並走させて画面に入れる。
    /// 他の船に邪魔されて1隻だけでは並走できないことがあるので、複数隻(既定5)を同時に向かわせ、前後にずらした位置を割り当てる。
    /// 一度でも映ったらそれで終わり（全員通常の動きに戻る）。シーンに1つ置く（設定は既定のままで動く）。
    /// </summary>
    public class EnemyFirstContact : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("発進してからこの秒数たっても敵が一度も映っていなければ、並走させる")]
        float graceSeconds = 6f;
        [SerializeField]
        [Range(1, 10)]
        [Tooltip("同時に並走準備させる敵の数。他の船に邪魔されても誰かが映るように複数向かわせる")]
        int rushCount = 5;
        [SerializeField]
        [Min(1f)]
        [Tooltip("並走中の最高速度の倍率")]
        float rushSpeedMultiplier = 2.5f;
        [SerializeField]
        [Tooltip("並走するときのプレイヤーとの横の距離(m)。カメラに映る範囲に収める")]
        float lateralDistance = 14f;
        [SerializeField]
        [Tooltip("プレイヤーよりどれだけ前に付けるか(m)。カメラはプレイヤーの少し先を見ているので、前寄りの方が映りやすい")]
        float aheadDistance = 8f;
        [SerializeField]
        [Min(1f)]
        [Tooltip("並走する船同士の前後の間隔(m)。重なって邪魔し合わないよう、1隻ごとに前後へずらす")]
        float slotSpacing = 9f;
        [SerializeField]
        [Min(1f)]
        [Tooltip("並走役以外の敵が、画面の中心付近へ向かうときの最高速度の倍率")]
        float seekSpeedMultiplier = 1.5f;

        float playingTime;
        bool seenOnce;
        EnemyShip[] rushers;

        void Awake() => rushers = new EnemyShip[Mathf.Max(1, rushCount)];

        // rushCount をプレイ中に変えても追従する
        void EnsureSlots()
        {
            int n = Mathf.Max(1, rushCount);
            if (rushers.Length == n) return;
            EndAll();
            rushers = new EnemyShip[n];
        }

        void Update()
        {
            GameFlow flow = GameFlow.Current;
            if (flow == null || flow.Phase != GamePhase.Playing)
            {
                // 発進前は初期化しておく。終了後は何もしない
                if (flow == null || flow.Phase == GamePhase.Standby || flow.Phase == GamePhase.Intro) Reset();
                return;
            }
            if (seenOnce) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            if (AnyEnemyVisible(cam))
            {
                seenOnce = true;
                EndAll();
                return;
            }

            playingTime += Time.deltaTime;
            if (playingTime < graceSeconds) return;

            // 並走役以外の全員も、画面の中心付近へ向かう（周回して映らない位置にいるのをやめさせる）
            Vector3 point = ViewCenterOnWater(cam);
            if (EnemyShip.SeekingCamera) EnemyShip.UpdateSeekPoint(point);
            else EnemyShip.BeginSeekCamera(point, seekSpeedMultiplier);

            EnsureSlots();
            for (int i = 0; i < rushers.Length; i++)
            {
                // 沈没・回収された、またはプールで再利用された船は交代させる
                EnemyShip s = rushers[i];
                if (s != null && s.isActiveAndEnabled && !s.IsSunk && s.IsRushing) continue;

                s = FindNearestFreeEnemy();
                rushers[i] = s;
                if (s != null) s.BeginRush(rushSpeedMultiplier, lateralDistance, aheadDistance + SlotOffset(i));
            }
        }

        // 0番=基準、1番=基準より前、2番=後ろ、3番=さらに前、4番=さらに後ろ…と交互にずらす
        float SlotOffset(int index)
        {
            int step = (index + 1) / 2;
            return (index % 2 == 1 ? step : -step) * slotSpacing;
        }

        void Reset()
        {
            playingTime = 0f;
            seenOnce = false;
            EndAll();
        }

        void OnDisable() => EnemyShip.EndSeekCamera();

        // カメラの中心が見ている水面上の点（水面と交わらなければプレイヤーの前方）
        static Vector3 ViewCenterOnWater(Camera cam)
        {
            PlayerTarget player = PlayerTarget.Current;
            float y = player != null ? (player.Water != null ? player.Water.waterY : player.transform.position.y) : 0f;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (ray.direction.y < -0.01f)
            {
                float t = (y - ray.origin.y) / ray.direction.y;
                if (t > 0f) return ray.origin + ray.direction * t;
            }

            Vector3 flat = cam.transform.forward;
            flat.y = 0f;
            Vector3 origin = player != null ? player.transform.position : cam.transform.position;
            return new Vector3(origin.x, y, origin.z) + flat.normalized * 20f;
        }

        void EndAll()
        {
            EnemyShip.EndSeekCamera();
            for (int i = 0; i < rushers.Length; i++)
            {
                if (rushers[i] != null) rushers[i].EndRush();
                rushers[i] = null;
            }
        }

        static bool AnyEnemyVisible(Camera cam)
        {
            foreach (EnemyShip e in EnemyShip.Active)
            {
                if (e == null || e.IsSunk) continue;
                Vector3 v = cam.WorldToViewportPoint(e.transform.position);
                if (v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f) return true;
            }
            return false;
        }

        // まだ並走を指示されていない、一番近い敵
        static EnemyShip FindNearestFreeEnemy()
        {
            PlayerTarget player = PlayerTarget.Current;
            if (player == null) return null;

            EnemyShip best = null;
            float bestSqr = float.MaxValue;
            foreach (EnemyShip e in EnemyShip.Active)
            {
                if (e == null || e.IsSunk || e.IsRushing) continue;
                Vector3 d = e.transform.position - player.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr)
                {
                    bestSqr = d.sqrMagnitude;
                    best = e;
                }
            }
            return best;
        }
    }
}
