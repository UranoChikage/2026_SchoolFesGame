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
        const float ExplosionHeight = 20f; // 爆発判定の上下の広がり(m)
        const float RangeGizmoDuration = 5f; // 爆発範囲の表示を残す時間(秒)
        static readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
        static readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();

        Vector3 velocity;
        float explosionRadius;
        float damage;
        LayerMask hitMask;
        BoatBuoyancy water;
        Transform owner;
        VfxAsset explosionVfx;
        CannonShooter source;
        float lifeTime;
        ParticleSystem[] particles;
        Renderer[] bodyRenderers;
        Collider[] bodyColliders;
        float fadeEndTime;
        bool finishing;

        void Awake()
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
            // 球の見た目（パーティクル以外）。着弾後は隠して、煙だけ残すのに使う
            var list = new List<Renderer>();
            foreach (Renderer rd in GetComponentsInChildren<Renderer>(true))
            {
                if (!(rd is ParticleSystemRenderer)) list.Add(rd);
            }
            bodyRenderers = list.ToArray();
            bodyColliders = GetComponentsInChildren<Collider>(true);
        }

        public void Launch(Vector3 initialVelocity, float explosionRadius, float damage,
            LayerMask hitMask, BoatBuoyancy water, Transform owner, VfxAsset explosionVfx, float lifeTime, CannonShooter source = null)
        {
            velocity = initialVelocity;
            this.explosionRadius = explosionRadius;
            this.damage = damage;
            this.hitMask = hitMask;
            this.water = water;
            this.owner = owner;
            this.explosionVfx = explosionVfx;
            this.lifeTime = lifeTime;
            this.source = source;

            // 前回の着弾後の状態を戻す
            finishing = false;
            foreach (Renderer rd in bodyRenderers) rd.enabled = true;
            foreach (Collider c in bodyColliders) c.enabled = true;

            // 前回の着弾で切った煙の発生を戻す
            foreach (ParticleSystem ps in particles)
            {
                // Stop/Play は使わない（Stopしたものは再利用時にPlayが効かなくなる）。ループ再生のまま、発生だけ入れ替える
                var emission = ps.emission;
                emission.enabled = true;
            }
        }

        void Update()
        {
            // 着弾後は、残っている煙が消えるまで待ってからプールへ返す
            if (finishing)
            {
                if (Time.time >= fadeEndTime) PoolManager.Release(gameObject);
                return;
            }

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
            if (lifeTime <= 0f) Finish();
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
            if (explosionVfx != null) explosionVfx.Play(point, explosionRadius * 2f);
            DrawExplosionRange(point);

            // 範囲内のIDamageableに1回ずつダメージ（コライダーが複数あっても重複させない）
            damaged.Clear();
            // 着地点から水平半径r（マーカーの円と同じ）で判定する。高さ方向は広く取った縦長のカプセル
            Vector3 up = Vector3.up * ExplosionHeight;
            Collider[] colliders = Physics.OverlapCapsule(point + up, point - up, explosionRadius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (Collider c in colliders)
            {
                if (owner != null && c.transform.IsChildOf(owner)) continue;
                // 浮いているアイテムに当たったら、撃った砲手が取得する
                if (source != null)
                {
                    var pickup = c.GetComponentInParent<ItemPickup>();
                    if (pickup != null) pickup.TryCollect(source);
                }

                var target = c.GetComponentInParent<IDamageable>();
                if (target != null && damaged.Add(target))
                {
                    Debug.Log($"[Hit] {(owner != null ? owner.name : "不明")} の砲弾が {((Component)target).name} に命中 (ダメージ {damage})", (Component)target);
                    target.TakeDamage(damage, point);
                }
            }

            Finish();
        }

        // 球を消し、新しい煙を止める。すでに出ている煙は寿命が尽きるまで残してからプールへ返す
        void Finish()
        {
            finishing = true;
            foreach (Renderer rd in bodyRenderers) rd.enabled = false;
            foreach (Collider c in bodyColliders) c.enabled = false;

            float longest = 0f;
            foreach (ParticleSystem ps in particles)
            {
                var emission = ps.emission;
                emission.enabled = false; // 新しい煙だけ止める（システムは止めないので、出ている煙は寿命まで残る）
                longest = Mathf.Max(longest, ps.main.startLifetime.constantMax);
            }
            fadeEndTime = Time.time + longest;
        }

        // 爆発の効果範囲を、一定時間シーンビューに残す（Gizmosを有効にすると見える）
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void DrawExplosionRange(Vector3 point)
        {
            const int Segments = 32;
            Vector3 prev = point + new Vector3(explosionRadius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector3 next = point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * explosionRadius;
                Debug.DrawLine(prev, next, Color.red, RangeGizmoDuration);
                prev = next;
            }
            Debug.DrawLine(point, point + Vector3.up * 3f, Color.red, RangeGizmoDuration);
        }
    }
}
