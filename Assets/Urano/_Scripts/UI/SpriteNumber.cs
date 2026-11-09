using UnityEngine;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// 0〜9のSpriteで整数を表示する。桁ごとにImageを並べるだけの軽い部品。
    /// 全桁を同じ大きさ(digitSize)で並べるので、カウントアップ中も桁が動かない。
    /// </summary>
    [RequireComponent(typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter))]
    public class SpriteNumber : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("0〜9のSprite（添字=数字）")]
        Sprite[] digits = new Sprite[10];
        [SerializeField] Vector2 digitSize = new Vector2(128f, 128f);
        [SerializeField]
        [Tooltip("桁の間隔。画像に余白があるときはマイナスで詰める")]
        float spacing;
        [SerializeField]
        [Tooltip("桁数をこの数に揃える（先頭は0埋めせず非表示。0以下なら桁数なり）")]
        int minDigits = 1;
        [SerializeField]
        Color color = Color.white;

        readonly System.Collections.Generic.List<Image> images = new System.Collections.Generic.List<Image>();
        int shown = -1;
        Color tint = Color.white;

        public int Value => shown;

        void Awake()
        {
            var layout = GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>全桁の色（Spriteに乗算）。桁が増えても引き継ぐ</summary>
        public void SetColor(Color color)
        {
            tint = color;
            foreach (Image img in images) img.color = color;
        }

        public void SetValue(int value)
        {
            value = Mathf.Max(0, value);
            if (value == shown) return;
            shown = value;

            string text = value.ToString();
            if (text.Length < minDigits) text = text.PadLeft(minDigits, '0');

            while (images.Count < text.Length) images.Add(CreateImage());
            for (int i = 0; i < images.Count; i++)
            {
                bool used = i < text.Length;
                images[i].gameObject.SetActive(used);
                if (used) images[i].sprite = digits[text[i] - '0'];
            }

            // 桁数が変わった瞬間に自分と親(BONUS+数字の並び)のレイアウトを確定させる。
            // これをしないと次フレームまで古い幅のままで、隣の「+」に数字が重なって見える
            if (isActiveAndEnabled)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
                if (transform.parent is RectTransform parent) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            }
        }

        Image CreateImage()
        {
            var go = new GameObject("Digit", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = digitSize;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = color;
            return img;
        }
    }
}
