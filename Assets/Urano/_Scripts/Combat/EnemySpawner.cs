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

        [Header("出現位置（プレイヤー中心）")]
        [SerializeField]
        float minRadius = 50f;
        [SerializeField]
        float maxRadius = 80f;
        [SerializeField]
        [Tooltip("カメラに映らない位置を探す試行回数。全部映ってしまったらその回は出さない")]
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
            nextSpawnTime = Time.time + spawnInterval;

            if (TryFindSpawnPoint(player, out Vector3 point))
            {
                Vector3 toPlayer = player.transform.position - point;
                toPlayer.y = 0f;
                Quaternion rot = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
                alive.Add(PoolManager.Spawn(enemyPrefab, point, rot).gameObject);
            }
        }

        int CurrentMaxAlive
        {
            get
            {
                if (rampSeconds <= 0f) return finalMaxAlive;
                float t = Mathf.Clamp01((Time.time - startTime) / rampSeconds);
                return Mathf.RoundToInt(Mathf.Lerp(initialMaxAlive, finalMaxAlive, t));
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

            for (int i = 0; i < placeAttempts; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                float radius = Random.Range(minRadius, maxRadius);
                point = new Vector3(center.x + dir.x * radius, y, center.z + dir.y * radius);
                if (!IsOnScreen(point)) return true;
            }

            point = default;
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
