using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 放物線を描いて飛び、コライダーか水面に着弾したら範囲爆発する砲弾。
    /// 移動は自前の積分で、フレーム間をレイで判定するので高速でもすり抜けない。
    /// </summary>
    public class Cannonball : MonoBehaviour
    {
        const int HitBufferSize = 16;
        static readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
        static readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();

        Vector3 velocity;
        float explosionRadius;
        float damage;
        LayerMask hitMask;
        BoatBuoyancy water;
        Transform owner;
        GameObject explosionPrefab;
        float lifeTime;

        public void Launch(Vector3 initialVelocity, float explosionRadius, float damage,
            LayerMask hitMask, BoatBuoyancy water, Transform owner, GameObject explosionPrefab, float lifeTime)
        {
            velocity = initialVelocity;
            this.explosionRadius = explosionRadius;
            this.damage = damage;
            this.hitMask = hitMask;
            this.water = water;
            this.owner = owner;
            this.explosionPrefab = explosionPrefab;
            this.lifeTime = lifeTime;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 start = transform.position;
            velocity += Physics.gravity * dt;
            Vector3 end = start + velocity * dt;

            // コライダーへの着弾（発射元の船は無視）
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length > 0f && TryFindHit(start, delta / length, length, out Vector3 hitPoint))
            {
                Explode(hitPoint);
                return;
            }

            // 水面への着弾
            if (water != null && end.y <= water.waterY)
            {
                float t = Mathf.Clamp01((start.y - water.waterY) / Mathf.Max(0.0001f, start.y - end.y));
                Explode(Vector3.Lerp(start, end, t));
                return;
            }

            transform.position = end;

            lifeTime -= dt;
            if (lifeTime <= 0f) PoolManager.Release(gameObject);
        }

        bool TryFindHit(Vector3 origin, Vector3 direction, float length, out Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hitBuffer, length, hitMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            point = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hitBuffer[i];
                if (owner != null && h.collider.transform.IsChildOf(owner)) continue;
                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    point = h.point;
                    found = true;
                }
            }
            return found;
        }

        void Explode(Vector3 point)
        {
            if (explosionPrefab != null)
            {
                PoolManager.Spawn(explosionPrefab, point, Quaternion.identity, 5f);
            }

            // 範囲内のIDamageableに1回ずつダメージ（コライダーが複数あっても重複させない）
            damaged.Clear();
            Collider[] colliders = Physics.OverlapSphere(point, explosionRadius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (Collider c in colliders)
            {
                if (owner != null && c.transform.IsChildOf(owner)) continue;
                var target = c.GetComponentInParent<IDamageable>();
                if (target != null && damaged.Add(target)) target.TakeDamage(damage, point);
            }

            PoolManager.Release(gameObject);
        }
    }
}
