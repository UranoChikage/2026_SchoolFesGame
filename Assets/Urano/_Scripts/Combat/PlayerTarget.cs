using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 「敵が狙う対象」の目印。プレイヤー船に付けておくと、敵は Inspector 設定なしでこれを見つけて狙う。
    /// 水面（BoatBuoyancy）も同じオブジェクトから取得して共有する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerTarget : MonoBehaviour
    {
        public static PlayerTarget Current { get; private set; }

        public Rigidbody Body { get; private set; }
        public BoatBuoyancy Water { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Water = GetComponentInChildren<BoatBuoyancy>();
            Current = this;
        }

        // 被弾したら全員のコントローラーを振動させる
        ShipHealth health;

        void OnEnable()
        {
            health = GetComponent<ShipHealth>();
            if (health != null) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        void OnDamaged(DamageInfo info) => GameVibration.HighAll(GameVibration.MediumPulse);

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
