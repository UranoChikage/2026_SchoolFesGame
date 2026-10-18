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
        [Tooltip("ビームの半径(m)。この範囲の船が沈む")]
        float beamRadius = 3f;

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
        public float BarrierRadius => barrierRadius;
    }
}
