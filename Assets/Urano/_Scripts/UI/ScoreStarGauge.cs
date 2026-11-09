using UnityEngine;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// 金額を見せない獲得スコア表示。ゲージが溜まると星が1つ点き、3つ点く（=ランクS）と星がキラキラ光る。
    /// 星はランクに対応（1=B到達, 2=A到達, 3=S到達）。ゲージはSランクまでの全体に対する進み具合（星はその途中の各ランクの位置で点く）。
    /// ゲージは Image Type=Filled (Horizontal) にしておく。
    /// </summary>
    public class ScoreStarGauge : MonoBehaviour
    {
        [Header("ゲージ")]
        [SerializeField]
        [Tooltip("中身のゲージ（Image Type=Filled）。fillAmountを動かす")]
        Image gaugeFill;
        [SerializeField]
        [Tooltip("ゲージが追いつく速さ。大きいほど素早い")]
        float gaugeSpeed = 6f;

        [Header("星（左から1,2,3）")]
        [SerializeField] Image[] stars = new Image[3];
        [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] Color litColor = Color.white;
        [SerializeField]
        [Tooltip("星が点く瞬間にポンと膨らむ大きさ(倍)")]
        float popScale = 1.5f;
        [SerializeField] float popTime = 0.35f;

        [Header("最大（星3つ）のキラキラ")]
        [SerializeField]
        [Tooltip("キラキラの色（星の色がこれと点灯色の間を明滅する）")]
        Color sparkleColor = new Color(1f, 0.95f, 0.5f);
        [SerializeField] float sparkleSpeed = 6f;
        [SerializeField]
        [Range(0f, 0.4f)]
        float sparkleScale = 0.12f;
        [SerializeField]
        [Tooltip("最大の間ずっと再生するパーティクル（任意）。星の上に置く")]
        ParticleSystem sparkleParticles;

        TreasureWallet wallet;
        float shownFill;
        int shownStars;
        float[] popAge;

        void Awake() => popAge = new float[Mathf.Max(stars.Length, 1)];

        void Start()
        {
            wallet = TreasureWallet.Current;
            if (wallet != null) wallet.StarsChanged += OnStarsChanged;
            shownStars = wallet != null ? wallet.Stars : 0;
            shownFill = wallet != null ? wallet.TotalProgress : 0f;
            ApplyParticles();
        }

        void OnDestroy()
        {
            if (wallet != null) wallet.StarsChanged -= OnStarsChanged;
        }

        void OnStarsChanged(int count)
        {
            // 増えた星だけポンと弾ませる
            for (int i = shownStars; i < count && i < popAge.Length; i++) popAge[i] = 0.0001f;
            shownStars = count;
            ApplyParticles();
        }

        void ApplyParticles()
        {
            if (sparkleParticles == null) return;
            if (shownStars >= TreasureWallet.MaxStars) { if (!sparkleParticles.isPlaying) sparkleParticles.Play(); }
            else sparkleParticles.Stop();
        }

        void Update()
        {
            if (wallet == null) return;

            bool max = shownStars >= TreasureWallet.MaxStars;

            // ゲージは全体(Sランクまで)に対する割合。ゲーム開始で金額が0に戻ったら空へ戻る
            float target = wallet.TotalProgress;
            shownFill = Mathf.Lerp(shownFill, target, 1f - Mathf.Exp(-gaugeSpeed * Time.deltaTime));
            if (gaugeFill != null) gaugeFill.fillAmount = shownFill;

            for (int i = 0; i < stars.Length; i++)
            {
                Image s = stars[i];
                if (s == null) continue;

                Color c = i < shownStars ? litColor : emptyColor;
                float scale = 1f;

                if (popAge[i] > 0f)
                {
                    popAge[i] += Time.deltaTime;
                    float t = popAge[i] / popTime;
                    if (t >= 1f) popAge[i] = 0f;
                    else scale = 1f + (popScale - 1f) * Mathf.Sin(t * Mathf.PI);
                }

                if (max)
                {
                    // 星ごとに位相をずらして順に光らせる
                    float w = 0.5f + 0.5f * Mathf.Sin(Time.time * sparkleSpeed - i * 1.2f);
                    c = Color.Lerp(litColor, sparkleColor, w);
                    scale *= 1f + sparkleScale * w;
                }

                s.color = c;
                s.rectTransform.localScale = Vector3.one * scale;
            }
        }
    }
}
