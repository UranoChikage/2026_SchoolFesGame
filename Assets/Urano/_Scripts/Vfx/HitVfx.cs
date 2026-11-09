using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 船がダメージを受けた(HPが減った)ときに、被弾位置で演出を出す。破片メッシュや火花のパーティクルは VfxAsset 側に仕込む。
    /// 敵船にもプレイヤー船にも付けられる。ShipHealth は自動で取得する。
    /// </summary>
    [RequireComponent(typeof(ShipHealth))]
    public class HitVfx : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("被弾時に出す演出（破片のメッシュ＋パーティクルなど）。Assets/Urano/VFX のVfxAsset")]
        VfxAsset vfx;
        [SerializeField]
        [Tooltip("演出の大きさの倍率")]
        float scale = 1f;
        [SerializeField]
        [Tooltip("着弾点を船の中心へ寄せる割合。0=着弾点そのまま、1=船の中心。着弾点が海面で破片が水中に出るとき、少し上げる")]
        [Range(0f, 1f)]
        float pullToShip = 0.3f;

        ShipHealth health;

        void Awake() => health = GetComponent<ShipHealth>();

        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(DamageInfo info)
        {
            if (vfx == null) return;
            Vector3 p = Vector3.Lerp(info.HitPoint, transform.position, pullToShip);
            vfx.Play(p, scale);
        }
    }
}
