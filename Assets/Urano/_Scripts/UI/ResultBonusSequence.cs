using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// リザルトの星ボーナス演出。「BONUS +[数字]」を星の数だけカウントアップし、
    /// 数え終わったら「!!」を弾ませて締める。星の数は GameResult.Last.Stars から読む。
    /// 見た目は全部Sprite（BONUS・+・数字・!!）。
    /// </summary>
    public class ResultBonusSequence : MonoBehaviour
    {
        [Header("表示部品")]
        [SerializeField] SpriteNumber number;
        [SerializeField] Graphic exclamation;
        [SerializeField]
        [Tooltip("BONUS+数字 の並び全体。数字が変わるたびに弾ませる")]
        RectTransform row;
        [SerializeField]
        [Tooltip("星の数だけ点灯させる（任意。左から）")]
        Image[] stars;
        [SerializeField]
        [Tooltip("星がBONUS表示から定位置へ飛ぶ秒数")]
        float starFlyTime = 0.45f;
        [SerializeField] float starStartScale = 1.8f;
        [Header("集計後の消え方")]
        [SerializeField] float holdTime = 0.6f;
        [SerializeField]
        [Tooltip("消えるまでに上へ上がる距離(px)")]
        float riseDistance = 120f;
        [SerializeField] float fadeTime = 1.2f;
        [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.25f);

        [Header("ボーナス")]
        [SerializeField] int bonusPerStar = 1000;
        [SerializeField] float startDelay = 0.8f;
        [SerializeField]
        [Tooltip("星1つ分を数え上げる秒数")]
        float countTime = 0.7f;
        [SerializeField] float betweenStars = 0.25f;

        [Header("色（星を数えるごとに数字の色が変わる）")]
        [SerializeField]
        [Tooltip("添字0=星1つ目を数えている間、1=2つ目、2=3つ目。足りない分は最後の色")]
        Color[] starColors =
        {
            Color.white,
            new Color(1f, 0.92f, 0.35f),
            new Color(1f, 0.55f, 0.15f),
        };
        [SerializeField]
        [Tooltip("点灯した星の色。添字0=星1つ目、1=2つ目、2=3つ目。足りない分は最後の色")]
        Color[] litStarColors =
        {
            Color.white,
            new Color(1f, 0.92f, 0.35f),
            new Color(1f, 0.55f, 0.15f),
        };
        [SerializeField]
        [Tooltip("色が切り替わる時の補間の速さ(大きいほど即座)")]
        float colorLerpSpeed = 12f;
        [Header("Animationの設定")]
        [SerializeField]
        [Tooltip("星が3つそろったときに引くAnimatorのトリガー名")]
        string animationTrigger = "Full";
        [SerializeField]
        [Tooltip("星ごとのAnimator（上のトリガーを持つもの）。3つそろったときに全部のトリガーを引く。空なら星(Stars)それぞれのImageから自動で探す")]
        Animator[] starAnimators;
        [Header("Popアニメ")]
        [SerializeField]
        [Tooltip("数字が1つ変わるたびの一瞬の拡大率（1=なし）")]
        float tickPop = 1.12f;
        [SerializeField]
        [Tooltip("拡大から戻る速さ(大きいほど素早く戻る)")]
        float tickReturnSpeed = 14f;
        [SerializeField]
        [Tooltip("星1つ数え終わったときの拡大率。実際は × starPopRatio 倍まで膨らむ")]
        float finishPop = 1.5f;
        [SerializeField]
        [Tooltip("星1つ数え終わったPopの強さ（finishPopに対する割合）")]
        float starPopRatio = 0.8f;
        [SerializeField] float starPopTime = 0.25f;
        [SerializeField]
        [Tooltip("最後の「!!」で出すPopの時間")]
        float finishPopTime = 0.45f;

        [Header("イベント（SE等をつなぐ）")]
        [SerializeField] UnityEvent onStarCounted;
        [SerializeField] UnityEvent onFinished;

        /// <summary>数え終わった後のボーナス合計（星なしなら0）</summary>
        public int Bonus { get; private set; }

        int starCount;
        CanvasGroup group;
        Color currentColor = Color.white;
        Graphic[] tintTargets;
        Vector2 rowHome;

        /// <summary>星の数を読み、表示を初期状態（非表示・0）にする。ResultSequence が最初に呼ぶ</summary>
        public void Prepare()
        {
            starCount = GameResult.HasLast ? GameResult.Last.Stars : 0;
            Bonus = starCount * bonusPerStar;

            // 数字以外の子（BONUS・+・!!）も同じ色に合わせる。数字の桁は SpriteNumber が自分で塗る
            var list = new System.Collections.Generic.List<Graphic>();
            foreach (Graphic g in row.GetComponentsInChildren<Graphic>(true))
                if (g.GetComponentInParent<SpriteNumber>() != number) list.Add(g);
            tintTargets = list.ToArray();
            ApplyColor(ColorFor(0));
            number.SetValue(0);
            if (exclamation != null) exclamation.gameObject.SetActive(false);
            if (stars != null)
                foreach (Image s in stars) if (s != null) s.color = emptyColor;

            group = row.GetComponent<CanvasGroup>();
            if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            rowHome = row.anchoredPosition;
            row.gameObject.SetActive(false);
        }

        /// <summary>カウントアップ演出。終わるまで待てるコルーチン（星なしなら即終了）</summary>
        public IEnumerator Play()
        {
            if (starCount <= 0) { onFinished?.Invoke(); yield break; }
            row.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(startDelay);

            int from = 0;
            for (int i = 0; i < starCount; i++)
            {
                if (stars != null && i < stars.Length && stars[i] != null) StartCoroutine(FlyStar(stars[i], i));
                int to = (i + 1) * bonusPerStar;
                Color target = ColorFor(i);

                SoundHandle countSound = AudioManager.Play(Sfx.ResultCountUp);
                for (float t = 0f; t < countTime; t += Time.unscaledDeltaTime)
                {
                    int v = Mathf.RoundToInt(Mathf.Lerp(from, to, t / countTime));
                    if (v != number.Value) { number.SetValue(v); row.localScale = Vector3.one * tickPop; }
                    ApplyColor(Color.Lerp(currentColor, target, colorLerpSpeed * Time.unscaledDeltaTime));
                    row.localScale = Vector3.Lerp(row.localScale, Vector3.one, tickReturnSpeed * Time.unscaledDeltaTime);
                    yield return null;
                }
                countSound.Stop();
                number.SetValue(to);
                ApplyColor(target);
                onStarCounted?.Invoke();
                // 星が3つそろった（飛んでくる演出が終わった）瞬間にアニメを再生
                if (i + 1 >= TreasureWallet.MaxStars) PlayFullAnimation();
                yield return Pop(finishPop * starPopRatio, starPopTime);
                from = to;
                yield return new WaitForSecondsRealtime(betweenStars);
            }

            if (exclamation != null) exclamation.gameObject.SetActive(true);
            onFinished?.Invoke();
            yield return Pop(finishPop, finishPopTime);

            // 集計が済んだら、ゆっくり上がりながら消える
            yield return new WaitForSecondsRealtime(holdTime);
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                float k = t / fadeTime;
                row.anchoredPosition = rowHome + Vector2.up * (riseDistance * k);
                group.alpha = 1f - k;
                yield return null;
            }
            group.alpha = 0f;
            row.gameObject.SetActive(false);
            row.anchoredPosition = rowHome;
        }

        void PlayFullAnimation()
        {
            if (string.IsNullOrEmpty(animationTrigger)) return;

            // 指定が無ければ、各星のImage（なければ親）についているAnimatorを使う
            if (starAnimators == null || starAnimators.Length == 0)
            {
                var found = new System.Collections.Generic.List<Animator>();
                if (stars != null)
                    foreach (Image s in stars)
                    {
                        Animator a = s != null ? s.GetComponentInParent<Animator>() : null;
                        if (a != null && !found.Contains(a)) found.Add(a);
                    }
                starAnimators = found.ToArray();
            }

            foreach (Animator a in starAnimators)
            {
                if (a == null) continue;
                a.ResetTrigger(animationTrigger);
                a.SetTrigger(animationTrigger);
            }
        }

        void ApplyColor(Color c)
        {
            currentColor = c;
            number.SetColor(c);
            if (tintTargets != null)
                foreach (Graphic g in tintTargets) if (g != null) g.color = c;
        }

        Color ColorFor(int starIndex) =>
            starColors == null || starColors.Length == 0 ? Color.white : starColors[Mathf.Min(starIndex, starColors.Length - 1)];

        // 星をBONUS表示の位置から、もともとの定位置へ飛ばして点灯させる
        IEnumerator FlyStar(Image star, int starIndex)
        {
            RectTransform rt = star.rectTransform;
            Vector3 home = rt.position;
            Vector3 homeScale = rt.localScale;
            Color lit = litStarColors == null || litStarColors.Length == 0
                ? Color.white : litStarColors[Mathf.Min(starIndex, litStarColors.Length - 1)];
            star.color = lit;
            for (float t = 0f; t < starFlyTime; t += Time.unscaledDeltaTime)
            {
                float k = t / starFlyTime;
                float e = 1f - (1f - k) * (1f - k);
                rt.position = Vector3.Lerp(row.position, home, e);
                rt.localScale = homeScale * Mathf.Lerp(starStartScale, 1f, e);
                yield return null;
            }
            rt.position = home;
            rt.localScale = homeScale;
        }

        // 一気に膨らんでから戻る
        IEnumerator Pop(float peak, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                row.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(t / time * Mathf.PI));
                yield return null;
            }
            row.localScale = Vector3.one;
        }
    }
}
