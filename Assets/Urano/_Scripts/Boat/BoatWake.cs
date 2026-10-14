using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 船の航跡（船尾の泡の帯）と船首の水しぶき。速度に応じて量を変え、「進んでいる」感じを出す。
    /// プレハブは Urano/VFX/Generate Boat Wake Prefab で作り、船の子にして位置を合わせる。
    /// </summary>
    public class BoatWake : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("船尾から出す航跡")]
        ParticleSystem wake;
        [SerializeField]
        [Tooltip("船首の水しぶき")]
        ParticleSystem bowSpray;

        [SerializeField]
        [Tooltip("最高速度のときの航跡の毎秒の粒数")]
        float wakeMaxRate = 30f;
        [SerializeField]
        [Tooltip("最高速度のときの水しぶきの毎秒の粒数")]
        float sprayMaxRate = 40f;
        [SerializeField]
        [Tooltip("この速度比(0〜0.5)未満では出さない。止まりかけで泡が出続けないようにする")]
        [Range(0f, 0.5f)] float minSpeedRatio = 0.1f;

        BoatController boat;
        ParticleSystem.EmissionModule wakeEmission;
        ParticleSystem.EmissionModule sprayEmission;

        void Awake()
        {
            boat = GetComponentInParent<BoatController>();
            if (wake != null) wakeEmission = wake.emission;
            if (bowSpray != null) sprayEmission = bowSpray.emission;
        }

        void Update()
        {
            float ratio = 0f;
            if (boat != null && boat.ControlEnabled && !boat.IsSunk)
                ratio = Mathf.Clamp01(boat.ForwardSpeed / Mathf.Max(0.01f, boat.MaxSpeed));
            if (ratio < minSpeedRatio) ratio = 0f;

            if (wake != null) wakeEmission.rateOverTime = wakeMaxRate * ratio;
            if (bowSpray != null) sprayEmission.rateOverTime = sprayMaxRate * ratio * ratio; // 高速ほど急に増える
        }
    }
}
