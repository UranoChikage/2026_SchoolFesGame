using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// PoolManagerが生成したインスタンスに自動で付くマーカー。元Prefabの記憶と、一定時間後の自動返却を担当する。
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        public GameObject Prefab { get; internal set; }
        public bool IsPooled { get; internal set; }

        float releaseTime = -1f;

        internal void OnSpawn(float autoReleaseDelay)
        {
            IsPooled = false;
            releaseTime = autoReleaseDelay > 0f ? Time.time + autoReleaseDelay : -1f;
        }

        void Update()
        {
            if (releaseTime >= 0f && Time.time >= releaseTime)
            {
                releaseTime = -1f;
                PoolManager.Release(gameObject);
            }
        }
    }
}
