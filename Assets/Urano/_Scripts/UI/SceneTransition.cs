using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// シーン遷移のロード画面。シーンが開いたら OpenAnimationClip を再生し、
    /// 遷移する前はその逆再生が終わるのを待ってからシーンを読み込む。
    /// 各シーンに1つ置く。遷移は SceneTransition.Load("シーン名") で呼ぶ（置いていないシーンでは即遷移）。
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
        [Tooltip("OpenAnimationClip。シーンが開いた時に再生し、遷移前は逆再生する")]
        AnimationClip openClip;

        [Header("ロード用Image")]
        [SerializeField]
        [Tooltip("開ききった後に非表示にするロード用Image(任意)。GameObjectは消さず、中のImageの表示だけを切るのでAnimatorは止まらない")]
        GameObject loadImage;

        [Header("タイミング")]
        [SerializeField]
        [Tooltip("シーンが開いてから、開くアニメを始めるまでの待ち(秒)")]
        float openDelay = 0.1f;

        public static SceneTransition Current { get; private set; }

        /// <summary>遷移中（開く演出は含まない）。二重遷移の防止に使う</summary>
        public bool IsClosing { get; private set; }

        PlayableGraph graph;
        AnimationClipPlayable clipPlayable;
        bool graphReady;
        float time; // 現在のクリップ再生位置(秒)
        Coroutine opening;
        Graphic[] graphics;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        /// <summary>ロード演出（逆再生）を挟んでシーンを読み込む。SceneTransitionが無いシーンでは即読み込む</summary>
        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (Current != null) Current.Begin(sceneName);
            else SceneManager.LoadScene(sceneName);
        }

        void Awake()
        {
            Current = this;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (loadImage != null) graphics = loadImage.GetComponentsInChildren<Graphic>(true);

            if (animator == null || openClip == null)
            {
                if (openClip == null) Debug.LogWarning("[SceneTransition] OpenAnimationClip が未設定です。演出なしで遷移します", this);
                else Debug.LogWarning("[SceneTransition] Animator が見つかりません。演出なしで遷移します", this);
                return;
            }

            graph = PlayableGraph.Create("SceneTransition");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            clipPlayable = AnimationClipPlayable.Create(graph, openClip);
            AnimationPlayableOutput.Create(graph, "SceneTransition", animator).SetSourcePlayable(clipPlayable);
            graphReady = true;

            // 最初の1フレームから「覆われた状態」にしておく（開く前にシーンが見えてしまうのを防ぐ）
            SetTime(0f);
            SetVisible(true);
        }

        void Start()
        {
            if (graphReady) opening = StartCoroutine(OpenRoutine());
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (graph.IsValid()) graph.Destroy();
        }

        IEnumerator OpenRoutine()
        {
            for (float t = 0f; t < openDelay; t += Time.unscaledDeltaTime) yield return null;

            float length = openClip.length;
            while (time < length)
            {
                SetTime(time + Time.unscaledDeltaTime);
                yield return null;
            }
            SetTime(length);
            SetVisible(false);
            opening = null;
        }

        void Begin(string sceneName)
        {
            if (IsClosing) return;
            IsClosing = true;
            if (opening != null) { StopCoroutine(opening); opening = null; }
            StartCoroutine(CloseRoutine(sceneName));
        }

        IEnumerator CloseRoutine(string sceneName)
        {
            if (graphReady)
            {
                SetVisible(true);
                // 開く時の逆再生：今の位置（開ききっていれば最後）から0秒へ。まだ開いている途中ならそこから戻る
                if (time <= 0f) time = openClip.length;
                while (time > 0f)
                {
                    SetTime(time - Time.unscaledDeltaTime);
                    yield return null;
                }
                SetTime(0f);
            }
            SceneManager.LoadScene(sceneName);
        }

        void SetTime(float t)
        {
            time = Mathf.Clamp(t, 0f, openClip.length);
            clipPlayable.SetTime(time);
            graph.Evaluate(0f);
        }

        void SetVisible(bool visible)
        {
            if (graphics == null) return;
            foreach (Graphic g in graphics) if (g != null) g.enabled = visible;
        }
    }
}
