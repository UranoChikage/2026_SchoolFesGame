using UnityEngine;

namespace Pirates
{
    public enum ItemKind
    {
        [Tooltip("数秒間、カーソル位置に出続け、当たった船は一撃で沈む")]
        Beam,
        [Tooltip("数秒間、船全体を敵の砲撃から守る")]
        Barrier,
    }

    /// <summary>アイテム1種類の定義（大砲ビーム／船大福バリアなど）。数値は全てここで調整する。</summary>
    [CreateAssetMenu(menuName = "Pirates/Item", fileName = "Item")]
    public class ItemAsset : ScriptableObject
    {
        [SerializeField]
        string displayName = "アイテム";
        [SerializeField]
        ItemKind kind = ItemKind.Beam;
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("効果の持続秒数")]
        float duration = 5f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("ドロップ抽選の重み。大きいほど出やすい")]
        float weight = 1f;
        [SerializeField]
        Sprite icon;
        [SerializeField]
        [Tooltip("ドロップ品・効果の仮の色（専用の見た目を作るまでの代用）")]
        Color color = Color.cyan;

        [Header("ビーム用")]
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("着弾点(カーソル位置)の判定の半径(m)。この範囲の船が沈む")]
        float beamRadius = 3f;
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("ビームの通り道の判定の半径(m)。砲口から着弾点までの間に通った船も沈む。見た目の太さにも使う")]
        float beamWidth = 1f;
        [Header("ビームの見た目（空なら仮の見た目）")]
        [SerializeField]
        [Tooltip("ビーム本体のPrefab。LineRenderer（2点・Use World Space）入りか、メッシュのモデル。モデルの場合は最も長い軸が長さ方向になり、長さ=ビームの長さ、太さ=Beam Width×2 に伸縮する")]
        GameObject beamPrefab;
        [SerializeField]
        [Tooltip("着弾点の判定範囲の円のPrefab。直径1mの薄い円盤にしておく（Beam Radius×2 に拡縮される）")]
        GameObject beamAreaPrefab;
        [SerializeField]
        [Tooltip("ビームを撃ち始めたとき、砲口に一度だけ出す演出（砲口フラッシュ）")]
        VfxAsset beamMuzzleVfx;
        [SerializeField]
        [Tooltip("ビームの終点（遮蔽や水面に当たっている位置）に1つだけ出して追従させる既定の演出。Prefabはループ再生にしておく。当たった物に HitSurface（素材）があれば、そちらが優先")]
        VfxAsset beamHitVfx;

        [Header("ビームの伸び縮み")]
        [SerializeField]
        [Min(1f)]
        [Tooltip("ビームが砲口から着弾点へ伸びる速さ(m/秒)。伸びきるまで判定は出ない")]
        float beamExtendSpeed = 80f;
        [SerializeField]
        [Min(1f)]
        [Tooltip("使用時間が終わってから、ビームが根元から着弾点へ縮む速さ(m/秒)。縮む間は判定なし")]
        float beamRetractSpeed = 120f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("ビーム使用中にカメラを引く距離(m)。0なら引かない")]
        float cameraZoomOut = 6f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("ビーム使用中のカメラの揺れ幅(m)。0なら揺らさない")]
        float cameraShake = 0.15f;

        [Header("バリア用")]
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("バリアの見た目の半径(m)（当たり判定ではなく見た目）")]
        float barrierRadius = 6f;

        public string DisplayName => displayName;
        public ItemKind Kind => kind;
        public float Duration => duration;
        public float Weight => weight;
        public Sprite Icon => icon;
        public Color Color => color;
        public float BeamRadius => beamRadius;
        public float BeamWidth => beamWidth;
        public GameObject BeamPrefab => beamPrefab;
        public GameObject BeamAreaPrefab => beamAreaPrefab;
        public VfxAsset BeamMuzzleVfx => beamMuzzleVfx;
        public VfxAsset BeamHitVfx => beamHitVfx;
        public float BeamExtendSpeed => beamExtendSpeed;
        public float BeamRetractSpeed => beamRetractSpeed;
        public float CameraZoomOut => cameraZoomOut;
        public float CameraShake => cameraShake;
        public float BarrierRadius => barrierRadius;
    }
}
