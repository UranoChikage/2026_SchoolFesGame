using UnityEngine;

namespace Pirates
{
    /// <summary>お宝1種類の定義。見た目と金額を持つ。</summary>
    [CreateAssetMenu(menuName = "Pirates/Treasure", fileName = "Treasure")]
    public class TreasureAsset : ScriptableObject
    {
        [SerializeField]
        string displayName = "お宝";
        [SerializeField]
        [Tooltip("加算される金額")]
        int value = 100;
        [SerializeField]
        [Tooltip("UIなどに出すアイコン")]
        Sprite icon;
        [SerializeField]
        [Tooltip("沈没位置に出す見た目（任意）")]
        VfxAsset dropVfx;
        [SerializeField]
        [Tooltip("獲得通知のUIに出す宝箱モデル（任意）。Animatorの Open トリガーで開く。未設定ならアイコンを出す")]
        GameObject displayModel;
        [SerializeField]
        [Min(0f)]
        [Tooltip("ドロップ抽選の重み。大きいほど出やすい")]
        float weight = 1f;

        [SerializeField]
        [Tooltip("獲得通知の後ろのエフェクトの色。通知が出ている間に左端→右端→左端…と往復して変化する")]
        Gradient noticeColor = new Gradient();

        public string DisplayName => displayName;
        public int Value => value;
        public Sprite Icon => icon;
        public Gradient NoticeColor => noticeColor;
        public GameObject DisplayModel => displayModel;
        public float Weight => weight;

        public void PlayDropVfx(Vector3 pos)
        {
            if (dropVfx != null) dropVfx.Play(pos);
        }
    }
}
