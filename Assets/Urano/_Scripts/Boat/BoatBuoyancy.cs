using UnityEngine;

namespace Pirates
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatBuoyancy : MonoBehaviour
    {
        [Header("船底のプローブ")]
        public Transform[] probes;

        [Header("水面")]
        public float waterY = 0f; // 水面の高さ

        [Header("浮力の強さ(ρ・g・面積を吸収)")]
        public float buoyancyCoefficient = 1f; // 浮力の強さ

        [Header("水の抵抗")]
        public float linerDrag = 0.5f; // 水の抵抗の強さ
        public float angularDrag = 0.5f; // 水の回転抵抗の強さ

        Rigidbody rb;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            int submerged = 0;// 浸水しているプローブの数
            foreach (var probe in probes)
            {
                float depth = waterY - probe.position.y; // プローブの水面からの深さ
                if (depth <= 0f) continue; // 水面より上にあるプローブは無視
                submerged++;
                //深いほど強い浮力をかける

                float d = Mathf.Clamp(depth, 0f, 1f);
                Vector3 force = Vector3.up * buoyancyCoefficient * d;
                rb.AddForceAtPosition(force, probe.position);
            }
            // 水の抵抗をかける
            if (submerged > 0)
            {
                float ratio = (float)submerged / probes.Length; // 浸水している割合
                rb.linearVelocity *= 1f - linerDrag * ratio * Time.fixedDeltaTime; // 水の抵抗をかける
                rb.angularVelocity *= 1f - angularDrag * ratio * Time.fixedDeltaTime; // 水の回転抵抗をかける

            }
        }

        private void OnDrawGizmos()
        {
            if (probes == null) return;
            foreach (var probe in probes)
            {
                if (probe == null) continue;
                bool wet = probe.position.y < waterY;
                Gizmos.color = wet ? Color.cyan : Color.yellow;
                Gizmos.DrawSphere(probe.position, 0.1f);
            }
        }
    }
}
