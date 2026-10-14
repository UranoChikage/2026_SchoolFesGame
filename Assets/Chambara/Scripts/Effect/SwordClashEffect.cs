using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 相打ち・ガード成功のときに、2本の剣がぶつかった位置へエフェクトを出す
    /// シーンのどこかに1つ置く
    /// </summary>
    public class SwordClashEffect : MonoBehaviour
    {
        [SerializeField] GameObject tieEffect;     // 相打ち
        [SerializeField] GameObject guardEffect;   // ガード成功
        [SerializeField] float lifetime = 1f;      // 出してから消すまで（ゲーム内時間。スロー中はゆっくり再生される）

        private void Start()
        {
            ChambaraManager.Instance.OnTieClash += OnTieClash;
            ChambaraManager.Instance.OnGuarded += OnGuarded;
        }

        private void OnDestroy()
        {
            if (ChambaraManager.Instance == null) return;
            ChambaraManager.Instance.OnTieClash -= OnTieClash;
            ChambaraManager.Instance.OnGuarded -= OnGuarded;
        }

        private void OnTieClash() => Spawn(tieEffect, 0, 1);

        private void OnGuarded(int attacker) => Spawn(guardEffect, attacker, 1 - attacker);

        void Spawn(GameObject prefab, int a, int b)
        {
            if (prefab == null) return;
            var players = ChambaraManager.Instance.Players;
            Sword swordA = players[a].GetComponentInChildren<Sword>();
            Sword swordB = players[b].GetComponentInChildren<Sword>();
            if (swordA == null || swordB == null) return;

            swordA.GetBlade(out var rootA, out var tipA);
            swordB.GetBlade(out var rootB, out var tipB);
            ClosestPoints(rootA, tipA, rootB, tipB, out var pa, out var pb);

            var effect = Instantiate(prefab, (pa + pb) / 2f, Quaternion.identity);
            Destroy(effect, lifetime);
        }

        // 線分 p1-q1 と p2-q2 のそれぞれで、もう一方に一番近い点
        static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a < 1e-6f && e < 1e-6f) { s = t = 0f; }
            else if (a < 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e < 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-6f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;   // 平行なら根元から
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }
    }
}
