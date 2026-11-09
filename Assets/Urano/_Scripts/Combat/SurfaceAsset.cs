using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 当たった物の「素材」ごとの演出（岩・木・水など）。HitSurface から参照する。
    /// 素材が増えたらここにフィールドを足す。
    /// </summary>
    [CreateAssetMenu(menuName = "Urano/Surface Asset", fileName = "NewSurface")]
    public class SurfaceAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("砲弾が着弾したときに出す爆発の演出。空なら既定の素材の演出")]
        VfxAsset cannonHitVfx;
        [SerializeField]
        [Tooltip("ビームの終点がここに当たっている間、当たった位置に出して追従させる演出（Prefabはループ再生）。空ならアイテム側の既定の演出")]
        VfxAsset beamHitVfx;
        [SerializeField]
        [Tooltip("ビームの演出の大きさの倍率")]
        float vfxScale = 1f;

        public VfxAsset CannonHitVfx => cannonHitVfx;
        public VfxAsset BeamHitVfx => beamHitVfx;
        public float VfxScale => vfxScale;
    }
}
