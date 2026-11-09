using System;
using UnityEngine;

namespace Chambara
{
    public enum SelectCategory { Character, Sword }

    /// <summary>
    /// 選択画面のアイコン1つ分。id は ModelList の名前と同じにする
    /// CharacterSelectManager の characterRoot / swordRoot の子に無ければ自動で付く（id は空なら GameObject 名）
    /// </summary>
    public class SelectSlot : MonoBehaviour
    {
        [SerializeField] string id;
        [SerializeField] SelectCategory category;
        [Tooltip("カーソルの枠を合わせる範囲。未設定なら自分")]
        [SerializeField] RectTransform frameTarget;

        public string Id => string.IsNullOrEmpty(id) ? gameObject.name : id;
        public SelectCategory Category => category;
        public RectTransform FrameTarget => frameTarget != null ? frameTarget : (RectTransform)transform;

        // アイコンごとの演出用（引数はプレイヤー番号 0/1）
        public event Action<int> OnHoverEnter;
        public event Action<int> OnHoverExit;
        public event Action<int> OnPicked;
        public event Action<int> OnUnpicked;

        public void Setup(SelectCategory category)
        {
            this.category = category;
        }

        /// <summary>space の座標系での、この枠の矩形</summary>
        public Rect GetRectIn(RectTransform space)
        {
            var corners = new Vector3[4];
            FrameTarget.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var c in corners)
            {
                Vector2 p = space.InverseTransformPoint(c);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        internal void NotifyHoverEnter(int player) => OnHoverEnter?.Invoke(player);
        internal void NotifyHoverExit(int player) => OnHoverExit?.Invoke(player);
        internal void NotifyPicked(int player) => OnPicked?.Invoke(player);
        internal void NotifyUnpicked(int player) => OnUnpicked?.Invoke(player);
    }
}
