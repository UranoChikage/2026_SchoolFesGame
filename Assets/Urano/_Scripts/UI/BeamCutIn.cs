using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// アイテム（ビーム、バリアなど全般）を使った砲手のカットインを出す。Canvas に置いた、砲手ごとの Animator 付きオブジェクトを
    /// 「一度消して、出し直す」ことで、毎回アニメを頭から再生する。
    /// （再生中に SetActive(true) だけしても、すでに有効なので頭からにならないため）
    ///
    /// ■ 置き方
    ///   Canvas の下の空のオブジェクトにこれを付け、cutIns に砲手1, 2, 3 の順でカットインを入れる。
    ///   再生が終わる時刻はアニメの長さから自動で求め、過ぎたら自動で消す。
    ///   Animator は、最初に再生されるステート（デフォルト）がカットインの動きになっていること。
    /// </summary>
    public class BeamCutIn : MonoBehaviour
    {
        [Tooltip("砲手の順（砲手番号1, 2, 3）のカットイン。Animator を持つ GameObject")]
        [SerializeField] GameObject[] cutIns;

        [Tooltip("アニメが終わってから消すまでの余白(秒)")]
        [SerializeField] float hideMargin = 0.05f;

        /// <summary>シーンに1つ。無ければ null（呼ぶ側は気にしなくてよい）</summary>
        public static BeamCutIn Current { get; private set; }

        float[] hideTime;   // 各カットインを消す時刻（Time.unscaledTime）。0以下は出していない

        void Awake()
        {
            Current = this;
            hideTime = new float[cutIns != null ? cutIns.Length : 0];

            for (int i = 0; i < hideTime.Length; i++)
            {
                if (cutIns[i] == null) continue;
                cutIns[i].SetActive(false);

                // ビームはカメラ演出などで Time.timeScale が変わることがあるので、止まらないようにする
                var animator = cutIns[i].GetComponent<Animator>();
                if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>この砲手のカットインを頭から再生する。範囲外の番号は何もしない</summary>
        public static void Play(int stationNumber)
        {
            if (Current != null) Current.PlayInternal(stationNumber - 1);
        }

        void PlayInternal(int index)
        {
            if (index < 0 || index >= hideTime.Length || cutIns[index] == null) return;

            GameObject go = cutIns[index];
            go.SetActive(false); // 再生中でも頭から再生するため、一度消す
            go.SetActive(true);

            hideTime[index] = Time.unscaledTime + ClipLength(go) + hideMargin;
        }

        void Update()
        {
            for (int i = 0; i < hideTime.Length; i++)
            {
                if (hideTime[i] <= 0f || Time.unscaledTime < hideTime[i]) continue;
                hideTime[i] = 0f;
                if (cutIns[i] != null) cutIns[i].SetActive(false);
            }
        }

        // Animator に入っているクリップのうち一番長いものの長さ。取れなければ 2 秒
        static float ClipLength(GameObject go)
        {
            var animator = go.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return 2f;

            float longest = 0f;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.length > longest) longest = clip.length;

            return longest > 0f ? longest : 2f;
        }
    }
}
