using UnityEngine;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// 獲得通知1件ぶんの見た目（PickupNotifier が複製して並べる）。Prefabにして PickupNotifier に割り当てる。
    /// 奥から BackSprite（固定）、EffectSprite（回転・脈動・色変化）、iconImage の順に重ねる。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PickupNoticeView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("後ろに置く動かないスプライト（任意）")]
        Image backSprite;
        [SerializeField]
        [Tooltip("回転・脈動するエフェクト用スプライト（任意）。BackSpriteの手前")]
        Image effectSprite;
        [SerializeField]
        [Tooltip("アイテム・お宝のアイコン")]
        Image iconImage;
        [SerializeField]
        [Tooltip("エフェクトスプライトの回転速度(度/秒)")]
        float effectSpinSpeed = 40f;
        [SerializeField]
        [Range(0f, 0.5f)]
        float effectPulse = 0.08f;
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("グラデーションが左端から右端まで進む時間(秒)。右端に着いたら折り返して往復する")]
        float gradientTime = 3f;

        public RectTransform Rect => (RectTransform)transform;

        CanvasGroup group;
        Color effectColor;
        Gradient effectGradient;

        void Awake() => group = GetComponent<CanvasGroup>();

        /// <summary>通知の中身をセットして表示する。gradientがあれば時間で色が変わり、無ければcolor一色（エフェクトスプライトの色）</summary>
        public void Show(Sprite icon, Color color, Gradient gradient)
        {
            gameObject.SetActive(true);
            SetAlpha(1f);
            Rect.localScale = Vector3.zero;
            if (effectSprite != null)
            {
                effectSprite.rectTransform.localRotation = Quaternion.identity;
                SetEffectColor(color);
            }
            effectColor = color;
            effectGradient = gradient;

            if (iconImage != null)
            {
                iconImage.enabled = icon != null;
                iconImage.sprite = icon;
            }
        }

        /// <summary>毎フレーム。age は出てからの秒数</summary>
        public void Tick(float age)
        {
            if (effectSprite == null) return;

            RectTransform rt = effectSprite.rectTransform;
            rt.localRotation = Quaternion.Euler(0f, 0f, -age * effectSpinSpeed);
            rt.localScale = Vector3.one * (1f + Mathf.Sin(age * 6f) * effectPulse);
            SetEffectColor(effectGradient != null ? effectGradient.Evaluate(Mathf.PingPong(age / gradientTime, 1f)) : effectColor);
        }

        // アルファはCanvasGroupで制御するので、色だけ使う
        void SetEffectColor(Color c) => effectSprite.color = new Color(c.r, c.g, c.b, 1f);

        public void SetAlpha(float a) => group.alpha = a;

        public void Hide() => gameObject.SetActive(false);
    }
}
