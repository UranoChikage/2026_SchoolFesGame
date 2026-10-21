using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 当たった物の素材と、ビームを遮るかどうか。岩礁・敵船などのルートに付ける（コライダーは子でもよい）。
    /// 付けなくてもビーム・砲弾は当たるが、その場合は「既定の素材」の演出になり、ビームの遮蔽にもならない。
    /// 海のオブジェクトには「Is Sea」をONにして付ける（水面への着弾はコライダーではなく高さで判定するため、
    /// 海の素材と既定の素材はここから引く）。海は1つだけ。
    /// </summary>
    public class HitSurface : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("素材。当たったときの演出がここで決まる")]
        SurfaceAsset surface;
        [SerializeField]
        [Tooltip("ONにすると、ビームはここで止まる（岩礁など）。OFFなら突き抜けて当たるだけ")]
        bool blocksBeam;

        [Header("海用")]
        [SerializeField]
        [Tooltip("海のオブジェクトならON。水面への着弾の素材として、上の素材が使われる")]
        bool isSea;
        [SerializeField]
        [Tooltip("素材（HitSurface）を持たない物に当たったときの既定の素材。海のオブジェクトに設定する")]
        SurfaceAsset defaultSurface;

        public SurfaceAsset Surface => surface;
        public bool BlocksBeam => blocksBeam;

        /// <summary>海のHitSurface（水面への着弾用）。無ければnull</summary>
        public static HitSurface Sea { get; private set; }
        /// <summary>素材を持たない物に当たったときの既定の素材。無ければnull</summary>
        public static SurfaceAsset DefaultSurface { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Sea = null;
            DefaultSurface = null;
        }

        void OnEnable()
        {
            if (!isSea) return;
            Sea = this;
            DefaultSurface = defaultSurface;
        }

        void OnDisable()
        {
            if (Sea != this) return;
            Sea = null;
            DefaultSurface = null;
        }

        /// <summary>砲弾の着弾演出。当たった物の素材→既定の素材の順に探す。hit は null でもよい</summary>
        public static VfxAsset ResolveCannonVfx(HitSurface hit)
        {
            SurfaceAsset sa = hit != null ? hit.surface : null;
            if (sa != null && sa.CannonHitVfx != null) return sa.CannonHitVfx;
            return DefaultSurface != null ? DefaultSurface.CannonHitVfx : null;
        }
    }
}
