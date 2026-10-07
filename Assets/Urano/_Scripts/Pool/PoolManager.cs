using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// Prefab単位でインスタンスを使い回す全体共通のプール。
    /// 砲弾・エフェクトなど Instantiate/Destroy を繰り返すものはここ経由で生成・返却する。
    /// </summary>
    public static class PoolManager
    {
        static readonly Dictionary<GameObject, Queue<PooledObject>> pools = new Dictionary<GameObject, Queue<PooledObject>>();
        static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain Reload無効時にエディタ再生をまたいで残らないようにする
            pools.Clear();
            root = null;
        }

        /// <summary>プールからPrefabのインスタンスを取り出して有効化する。autoReleaseDelay&gt;0なら指定秒後に自動で返却</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float autoReleaseDelay = 0f)
        {
            PooledObject pooled = Dequeue(prefab);
            if (pooled == null)
            {
                GameObject go = Object.Instantiate(prefab, position, rotation, Root);
                pooled = go.GetComponent<PooledObject>();
                if (pooled == null) pooled = go.AddComponent<PooledObject>();
                pooled.Prefab = prefab;
            }

            Transform t = pooled.transform;
            t.SetPositionAndRotation(position, rotation);
            pooled.OnSpawn(autoReleaseDelay);
            pooled.gameObject.SetActive(true);
            return pooled.gameObject;
        }

        /// <summary>Prefabのコンポーネントを指定してSpawnする</summary>
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, float autoReleaseDelay = 0f) where T : Component
        {
            return Spawn(prefab.gameObject, position, rotation, autoReleaseDelay).GetComponent<T>();
        }

        /// <summary>プールへ返却する。プール管理外のオブジェクトはDestroyする</summary>
        public static void Release(GameObject instance)
        {
            if (instance == null) return;

            var pooled = instance.GetComponent<PooledObject>();
            if (pooled == null || pooled.Prefab == null)
            {
                Object.Destroy(instance);
                return;
            }
            if (pooled.IsPooled) return; // 二重返却防止

            pooled.IsPooled = true;
            instance.SetActive(false);
            instance.transform.SetParent(Root, false);
            if (!pools.TryGetValue(pooled.Prefab, out var queue))
            {
                queue = new Queue<PooledObject>();
                pools[pooled.Prefab] = queue;
            }
            queue.Enqueue(pooled);
        }

        static PooledObject Dequeue(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out var queue)) return null;
            while (queue.Count > 0)
            {
                PooledObject pooled = queue.Dequeue();
                if (pooled != null) return pooled; // シーン遷移などで破棄されたものは捨てる
            }
            return null;
        }

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("[PoolRoot]");
                    Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }
    }
}
