using UnityEngine;

namespace Chambara
{
    public class EffectGenrate : MonoBehaviour
    {
        [System.Serializable]
        public struct SpawnPoint
        {
            [Tooltip("このオブジェクトからの相対座標")]
            public Vector3 position;
            [Tooltip("この位置だけの追加遅延（秒）")]
            [Min(0f)] public float delay;
        }

        [Header("エフェクト")]
        [SerializeField] GameObject effectPrefab;
        [Tooltip("出てから消えるまでの秒数。0なら自動で消さない")]
        [SerializeField, Min(0f)] float effectLifeTime = 0.5f;

        [Header("自動生成")]
        [Tooltip("ONならcoolDownごとに自動生成")]
        [SerializeField] bool autoGenerate = true;
        [Tooltip("自動生成の間隔（秒）")]
        [SerializeField, Min(0.01f)] float coolDown = 1f;

        [Header("出す位置")]
        [Tooltip("空なら自身の位置に1つ出す")]
        [SerializeField] SpawnPoint[] spawnPoints;

        [Header("開始タイミングのずれ")]
        [Tooltip("番号順に一定間隔でずらす（i番目は interval × i 秒遅れる）")]
        [SerializeField, Min(0f)] float delayInterval = 0f;
        [Tooltip("0〜この値の範囲でランダムに遅延を加える")]
        [SerializeField, Min(0f)] float randomDelayMax = 0f;

        float timer;

        GameObject[] effects;
        ParticleSystem[] particles;
        float[] lifeTimers;
        float[] delayTimers;
        bool[] isWaiting;

        bool HasPositions => spawnPoints != null && spawnPoints.Length > 0;

        void Awake()
        {
            if (effectPrefab == null)
            {
                Debug.LogError($"{name}: effectPrefab が設定されていません", this);
                enabled = false;
                return;
            }

            int count = HasPositions ? spawnPoints.Length : 1;
            effects = new GameObject[count];
            particles = new ParticleSystem[count];
            lifeTimers = new float[count];
            delayTimers = new float[count];
            isWaiting = new bool[count];

            for (int i = 0; i < count; i++)
            {
                effects[i] = Instantiate(effectPrefab, GetWorldPosition(i), transform.rotation);
                particles[i] = effects[i].GetComponent<ParticleSystem>();
                effects[i].SetActive(false);
            }
        }

        void Update()
        {
            if (autoGenerate)
            {
                timer += Time.deltaTime;
                if (timer >= coolDown)
                {
                    Generate();
                    timer = 0f;
                }
            }

            UpdateDelay();
            UpdateLifeTime();
        }

        /// <summary>全ての位置にエフェクトを出す（それぞれの遅延つき）</summary>
        public void Generate()
        {
            for (int i = 0; i < effects.Length; i++)
            {
                Generate(i);
            }
        }

        /// <summary>指定した番号の位置にエフェクトを出す（遅延つき）</summary>
        public void Generate(int index)
        {
            if (effects == null || index < 0 || index >= effects.Length) return;

            float delay = GetDelay(index);
            if (delay <= 0f)
            {
                Play(index);
            }
            else
            {
                delayTimers[index] = delay;
                isWaiting[index] = true;
            }
        }

        /// <summary>遅延なしで即座に出す</summary>
        public void Play(int index)
        {
            if (effects == null || index < 0 || index >= effects.Length) return;

            isWaiting[index] = false;

            GameObject effect = effects[index];
            effect.transform.SetPositionAndRotation(GetWorldPosition(index), transform.rotation);

            effect.SetActive(false);
            effect.SetActive(true);
            if (particles[index] != null)
            {
                particles[index].Clear(true);
                particles[index].Play(true);
            }

            lifeTimers[index] = effectLifeTime;
        }

        /// <summary>全エフェクトを即座に消し、待機中のものもキャンセル</summary>
        public void StopAll()
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
            {
                isWaiting[i] = false;
                if (effects[i] != null) effects[i].SetActive(false);
            }
        }

        float GetDelay(int index)
        {
            float delay = delayInterval * index;

            if (HasPositions && index < spawnPoints.Length)
            {
                delay += spawnPoints[index].delay;
            }

            if (randomDelayMax > 0f)
            {
                delay += Random.Range(0f, randomDelayMax);
            }

            return delay;
        }

        void UpdateDelay()
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (!isWaiting[i]) continue;

                delayTimers[i] -= Time.deltaTime;
                if (delayTimers[i] <= 0f)
                {
                    Play(i);
                }
            }
        }

        void UpdateLifeTime()
        {
            if (effectLifeTime <= 0f) return;

            for (int i = 0; i < effects.Length; i++)
            {
                if (!effects[i].activeSelf) continue;

                lifeTimers[i] -= Time.deltaTime;
                if (lifeTimers[i] <= 0f)
                {
                    effects[i].SetActive(false);
                }
            }
        }

        Vector3 GetWorldPosition(int index)
        {
            if (!HasPositions) return transform.position;
            return transform.TransformPoint(spawnPoints[index].position);
        }

        void OnDisable()
        {
            StopAll();
        }

        void OnDestroy()
        {
            if (effects == null) return;
            foreach (var e in effects)
            {
                if (e != null) Destroy(e);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (!HasPositions)
            {
                Gizmos.DrawWireSphere(transform.position, 0.2f);
                return;
            }
            foreach (var p in spawnPoints)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(p.position), 0.2f);
            }
        }
    }
}