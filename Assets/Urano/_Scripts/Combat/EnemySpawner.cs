using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// プレイヤー船の周囲に敵船を出し続ける。敵はPoolManagerで使い回す。
    /// カメラに映らない位置に出し、離れすぎた敵は回収する。同時に出る数は時間とともに増える。
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("敵")]
        [SerializeField]
        [Tooltip("敵船のPrefab（EnemyShip付き）")]
        EnemyShip enemyPrefab;

        [Header("数")]
        [SerializeField]
        [Tooltip("開始時に同時に出る数の上限")]
        int initialMaxAlive = 3;
        [SerializeField]
        [Tooltip("rampSeconds後に到達する、同時に出る数の上限")]
        int finalMaxAlive = 15;
        [SerializeField]
        [Tooltip("上限がinitialからfinalまで増えるのにかかる時間(秒)。0なら最初からfinal")]
        float rampSeconds = 120f;
        [SerializeField]
        [Tooltip("出現の間隔(秒)")]
        float spawnInterval = 2.5f;

        [Header("スコア連動（星の数で増える）")]
        [SerializeField]
        [Tooltip("星の数(0〜3)ごとの倍率。出現の間隔を割り、同時に出る数の上限に掛ける。1=そのまま")]
        float[] rateByStars = { 1f, 1.2f, 1.5f, 2f };
        [SerializeField]
        [Tooltip("倍率をかけたあとの同時に出る数の絶対上限")]
        int hardMaxAlive = 25;

        [Header("出現位置（プレイヤー中心）")]
        [SerializeField]
        float minRadius = 50f;
        [SerializeField]
        float maxRadius = 80f;
        [SerializeField]
        [Range(30f, 180f)]
        [Tooltip("カメラの前方からこの角度(度)の範囲にだけ出す（左右それぞれ）。90なら前方の半円、180なら全方位。後ろに出ると、追うために舵を切り続けることになる")]
        float spawnHalfAngle = 100f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("出現位置は、すでにいる敵からこの距離(m)以上離す。船同士が重なって挙動がおかしくなるのを防ぐ。0なら見ない")]
        float minSpacing = 15f;
        [SerializeField]
        [Tooltip("カメラに映らない位置を探す試行回数（映る位置・他の敵に近すぎる位置はやり直す）。全部だめならその回は出さない")]
        int placeAttempts = 8;
        [SerializeField]
        [Tooltip("水面からの出現高さのオフセット(m)")]
        float spawnHeight = 0f;
        [SerializeField]
        [Tooltip("プレイヤーからこの距離(m)より離れた敵は回収する")]
        float despawnDistance = 150f;

        readonly List<GameObject> alive = new List<GameObject>();
        float nextSpawnTime;
        float startTime;

        void OnEnable()
        {
            startTime = Time.time;
            nextSpawnTime = Time.time + spawnInterval;
        }

        void Update()
        {
            PlayerTarget player = PlayerTarget.Current;
            if (player == null || enemyPrefab == null) return;

            CleanUp(player.transform.position);

            if (Time.time < nextSpawnTime || alive.Count >= CurrentMaxAlive) return;
            nextSpawnTime = Time.time + spawnInterval / StarRate;

            if (TryFindSpawnPoint(player, out Vector3 point))
            {
                Vector3 toPlayer = player.transform.position - point;
                toPlayer.y = 0f;
                Quaternion rot = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
                alive.Add(PoolManager.Spawn(enemyPrefab, point, rot).gameObject);
            }
        }

        // いまの星の数に対応する倍率（未設定は1）
        float StarRate
        {
            get
            {
                if (rateByStars == null || rateByStars.Length == 0) return 1f;
                int i = Mathf.Clamp(TreasureWallet.CurrentStars, 0, rateByStars.Length - 1);
                return Mathf.Max(0.1f, rateByStars[i]);
            }
        }

        int CurrentMaxAlive
        {
            get
            {
                float baseMax = finalMaxAlive;
                if (rampSeconds > 0f)
                {
                    float t = Mathf.Clamp01((Time.time - startTime) / rampSeconds);
                    baseMax = Mathf.Lerp(initialMaxAlive, finalMaxAlive, t);
                }
                return Mathf.Min(hardMaxAlive, Mathf.RoundToInt(baseMax * StarRate));
            }
        }

        // 沈没して返却された敵と、離れすぎた敵を管理対象から外す
        void CleanUp(Vector3 playerPos)
        {
            float limit = despawnDistance * despawnDistance;
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                GameObject e = alive[i];
                if (e == null || !e.activeSelf)
                {
                    alive.RemoveAt(i);
                    continue;
                }

                Vector3 d = e.transform.position - playerPos;
                d.y = 0f;
                if (d.sqrMagnitude > limit)
                {
                    PoolManager.Release(e);
                    alive.RemoveAt(i);
                }
            }
        }

        bool TryFindSpawnPoint(PlayerTarget player, out Vector3 point)
        {
            float y = (player.Water != null ? player.Water.waterY : player.transform.position.y) + spawnHeight;
            Vector3 center = player.transform.position;

            // 前方の基準：カメラの向き（水平）。取れなければ船の向き
            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            for (int i = 0; i < placeAttempts; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-spawnHalfAngle, spawnHalfAngle), 0f) * forward;
                float radius = Random.Range(minRadius, maxRadius);
                point = new Vector3(center.x + dir.x * radius, y, center.z + dir.z * radius);
                if (!IsOnScreen(point) && !IsTooClose(point)) return true;
            }

            point = default;
            return false;
        }

        // すでにいる敵に近すぎる位置か（水平距離）
        bool IsTooClose(Vector3 point)
        {
            if (minSpacing <= 0f) return false;

            float limit = minSpacing * minSpacing;
            foreach (GameObject e in alive)
            {
                if (e == null || !e.activeSelf) continue;
                Vector3 d = e.transform.position - point;
                d.y = 0f;
                if (d.sqrMagnitude < limit) return true;
            }
            return false;
        }

        // 出現時に急に現れないよう、余白つきで「映っている」を判定
        bool IsOnScreen(Vector3 worldPos)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 v = cam.WorldToViewportPoint(worldPos);
            return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }
    }
}
