using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Pirates
{
    /// <summary>
    /// シーン遷移のロード画面。シーンが開いたら OpenAnimationClip を再生し、
    /// クリップの長さが経ったらロード用ImageをSetActive(false)にする。
    /// 遷移する前はロード用ImageをSetActive(true)にして、その逆再生が終わるのを待ってからシーンを読み込む。
    /// 各シーンに1つ置く。遷移は SceneTransition.Load("シーン名") で呼ぶ（置いていないシーンでは即遷移）。
    /// ロード画面が開ききるまで GameFlow は導入演出(Timeline)を始めない（SceneTransition.IsOpening を参照）。
    /// クリップの0秒＝画面が覆われた状態、最後＝開ききった状態にしておく。
    /// 再生はタイムスケールの影響を受けない（Time.timeScale=0でも動く）。
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        [Header("アニメーション")]
        [SerializeField]
        [Tooltip("クリップを再生する対象のAnimator（コントローラは不要）。ロード用Image自身か、その親に付ける")]
        Animator animator;
        [SerializeField]
        [Tooltip("OpenAnimationClip。シーンが開いた時に再生し、遷移前は逆再生する。この長さがロード用Imageを出しておく時間になる")]
        AnimationClip openClip;

        [Header("ロード用Image")]
        [SerializeField]
        [Tooltip("ロード用ImageのGameObject。開く間だけ表示し、開ききったらSetActive(false)、遷移前にSetActive(true)にする")]
        GameObject loadImage;

        [Header("タイミング")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("開くアニメのここまで進んだら、導入演出(Timeline)を始めてよい。1=開ききってから")]
        float releaseRatio = 0.8f;
        [SerializeField]
        [Tooltip("シーンが開いてから、開くアニメを始めるまでの待ち(秒)")]
        float openDelay = 0.1f;

        public static SceneTransition Current { get; private set; }

        /// <summary>ロード画面が開き始めてから、導入演出を始めてよい位置(releaseRatio)に達するまで。trueの間は導入演出などを始めない</summary>
        public static bool IsOpening => Current != null && Current.opening;

        /// <summary>遷移中（閉じる演出）。二重遷移の防止に使う</summary>
        public bool IsClosing { get; private set; }

        PlayableGraph graph;
        AnimationClipPlayable clipPlayable;
        float time; // 現在のクリップ再生位置(秒)
        bool opening;    // 導入演出を待たせている間
        bool imageShown; // ロード用Imageが出ている間（開ききるまで）
        Coroutine openRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        /// <summary>ロード演出（逆再生）を挟んでシーンを読み込む。SceneTransitionが無いシーンでは即読み込む</summary>
        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (Current != null) Current.Begin(sceneName);
            else SceneManager.LoadScene(sceneName);
        }

        bool CanPlay => animator != null && openClip != null;

        void Awake()
        {
            Current = this;
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (!CanPlay)
            {
                Debug.LogWarning(openClip == null
                    ? "[SceneTransition] OpenAnimationClip が未設定です。演出なしで遷移します"
                    : "[SceneTransition] Animator が見つかりません。演出なしで遷移します", this);
                if (loadImage != null) loadImage.SetActive(false);
                return;
            }

            // 最初の1フレームから「覆われた状態」にしておく（開く前にシーンが見えてしまうのを防ぐ）
            opening = true;
            imageShown = true;
            if (loadImage != null) loadImage.SetActive(true);
            BuildGraph();
            SetTime(0f);
        }

        void Start()
        {
            if (CanPlay) openRoutine = StartCoroutine(OpenRoutine());
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            DestroyGraph();
        }

        IEnumerator OpenRoutine()
        {
            for (float t = 0f; t < openDelay; t += Time.unscaledDeltaTime) yield return null;

            float length = openClip.length;
            while (time < length)
            {
                SetTime(time + Time.unscaledDeltaTime);
                if (opening && time >= length * releaseRatio) opening = false; // ここからTimelineを始めてよい
                yield return null;
            }
            SetTime(length);

            // クリップの長さが経ったら、ロード用Imageを消す
            DestroyGraph();
            if (loadImage != null) loadImage.SetActive(false);
            opening = false;
            imageShown = false;
            openRoutine = null;
        }

        void Begin(string sceneName)
        {
            if (IsClosing) return;
            IsClosing = true;
            if (openRoutine != null) { StopCoroutine(openRoutine); openRoutine = null; }
            StartCoroutine(CloseRoutine(sceneName));
        }

        IEnumerator CloseRoutine(string sceneName)
        {
            if (CanPlay)
            {
                // 開く時の逆再生：今の位置（開ききっていれば最後）から0秒へ。まだ開いている途中ならそこから戻る
                bool wasOpen = !imageShown;
                opening = false;
                imageShown = true;
                if (loadImage != null) loadImage.SetActive(true);
                if (!graph.IsValid()) BuildGraph();
                if (wasOpen) time = openClip.length;
                SetTime(time);

                while (time > 0f)
                {
                    SetTime(time - Time.unscaledDeltaTime);
                    yield return null;
                }
                SetTime(0f);
            }
            SceneManager.LoadScene(sceneName);
        }

        // グラフは演出の間だけ持つ。ロード用ImageがAnimatorごと非アクティブになっても、次の再生で作り直せる
        void BuildGraph()
        {
            graph = PlayableGraph.Create("SceneTransition");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            clipPlayable = AnimationClipPlayable.Create(graph, openClip);
            AnimationPlayableOutput.Create(graph, "SceneTransition", animator).SetSourcePlayable(clipPlayable);
        }

        void DestroyGraph()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        void SetTime(float t)
        {
            time = Mathf.Clamp(t, 0f, openClip.length);
            clipPlayable.SetTime(time);
            graph.Evaluate(0f);
        }
    }
}
