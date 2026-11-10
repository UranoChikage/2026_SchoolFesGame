using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// リザルト全体の進行。懸賞金カウントアップ → BONUSカウントアップ → 懸賞金にBONUSを合算 → RANK表示 → タイトルへ戻るボタン。
    /// 懸賞金 = お宝の合計金額、最終額 = 懸賞金 + 星ボーナス。ランクは最終額で判定する（ボーナスでランクが上がることがある）。
    /// </summary>
    public class ResultSequence : MonoBehaviour
    {
        [Header("懸賞金")]
        [SerializeField] SpriteNumber bountyNumber;
        [SerializeField] RectTransform bountyRoot;
        [SerializeField] float bountyCountTime = 1.6f;

        [Header("BONUS")]
        [SerializeField] ResultBonusSequence bonus;
        [SerializeField]
        [Tooltip("BONUS後、懸賞金へ合算するカウントアップの秒数")]
        float mergeTime = 0.8f;

        [Header("RANK")]
        [SerializeField] Image rankImage;
        [SerializeField] Sprite rankB;
        [SerializeField] Sprite rankA;
        [SerializeField] Sprite rankS;
        [SerializeField] float rankPop = 2f;

        [Header("タイトルへ戻る")]
        [SerializeField] Button titleButton;
        [SerializeField] string titleScene = "";

        [Header("間隔")]
        [SerializeField] float startDelay = 0.6f;
        [SerializeField] float gap = 0.5f;

        [Header("イベント（SE等）")]
        [SerializeField] UnityEvent onBountyDone;
        [SerializeField] UnityEvent onRankShown;

        [Header("テスト用（Resultを単体再生したときだけ使う。Battleから来たときは本物の結果を使う）")]
        [SerializeField] bool useDummyWhenNoResult = true;
        [SerializeField] int dummyTotal = 2500;
        [SerializeField, Range(0, 3)] int dummyStars = 2;
        [SerializeField] int dummyThresholdA = 3000;
        [SerializeField] int dummyThresholdS = 6000;

        void Start()
        {
            if (useDummyWhenNoResult && !GameResult.HasLast)
            {
                GameResult.Set(new GameResult(EndReason.TimeUp, dummyTotal, TreasureRank.B, 0f,
                    dummyStars, dummyThresholdA, dummyThresholdS));
                Debug.Log("[ResultSequence] 結果が無いのでダミーで再生します", this);
            }
            if (bonus != null) bonus.Prepare();
            if (rankImage != null) rankImage.gameObject.SetActive(false);
            if (titleButton != null)
            {
                titleButton.gameObject.SetActive(false);
                titleButton.onClick.AddListener(GoTitle);
            }
            bountyNumber.SetValue(0);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            int total = GameResult.HasLast ? GameResult.Last.Total : 0;
            yield return new WaitForSecondsRealtime(startDelay);

            yield return CountUp(0, total, bountyCountTime);
            onBountyDone?.Invoke();
            yield return new WaitForSecondsRealtime(gap);

            if (bonus != null) yield return bonus.Play();
            int bonusValue = bonus != null ? bonus.Bonus : 0;
            yield return new WaitForSecondsRealtime(gap);

            int final = total + bonusValue;
            if (bonusValue > 0) yield return CountUp(total, final, mergeTime);
            yield return new WaitForSecondsRealtime(gap);

            if (rankImage != null)
            {
                TreasureRank rank = GameResult.HasLast ? GameResult.Last.RankFor(final) : TreasureRank.B;
                rankImage.sprite = rank == TreasureRank.S ? rankS : rank == TreasureRank.A ? rankA : rankB;
                rankImage.gameObject.SetActive(true);
                onRankShown?.Invoke();
                AudioManager.Play(Sfx.ResultFinal);
                AudioManager.Play(rank == TreasureRank.S ? Sfx.RankS : Sfx.RankOther);
                yield return Pop(rankImage.rectTransform, rankPop, 0.5f);
                yield return new WaitForSecondsRealtime(gap);
            }

            if (titleButton != null) titleButton.gameObject.SetActive(true);
        }

        IEnumerator CountUp(int from, int to, float time)
        {
            SoundHandle countSound = AudioManager.Play(Sfx.ResultCountUp);
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / time, 3f); // 最後に向けてゆっくり
                int v = Mathf.RoundToInt(Mathf.Lerp(from, to, k));
                if (v != bountyNumber.Value && bountyRoot != null) bountyRoot.localScale = Vector3.one * 1.06f;
                bountyNumber.SetValue(v);
                if (bountyRoot != null)
                    bountyRoot.localScale = Vector3.Lerp(bountyRoot.localScale, Vector3.one, 14f * Time.unscaledDeltaTime);
                yield return null;
            }
            countSound.Stop();
            bountyNumber.SetValue(to);
            if (bountyRoot != null) bountyRoot.localScale = Vector3.one;
        }

        static IEnumerator Pop(RectTransform target, float peak, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                target.localScale = Vector3.one * Mathf.Lerp(peak, 1f, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        void GoTitle()
        {
            if (!string.IsNullOrEmpty(titleScene)) SceneTransition.Load(titleScene);
            else Debug.LogWarning("[ResultSequence] titleScene が未設定です", this);
        }
    }
}
