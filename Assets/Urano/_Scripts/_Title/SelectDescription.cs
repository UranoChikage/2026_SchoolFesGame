using UnityEngine;

namespace Title
{
    /// <summary>
    /// 選ばれているゲームの紹介文を、CanvasGroup のアルファで切り替える。
    /// 紹介文はあらかじめ全部 Canvas に出しておく（文字を差し替えないので重くならない）。
    ///
    /// ■ 置き方
    ///   このスクリプトを適当なオブジェクトに付け、groups にパネルと同じ順（左→右）で CanvasGroup を入れる。
    ///   Expander に SelectLayout を割り当てる。
    /// </summary>
    public class SelectDescription : MonoBehaviour
    {
        [Tooltip("どのパネルが選ばれているかを読む相手")]
        [SerializeField] SelectPanelExpander expander;

        [Tooltip("パネルと同じ順の紹介文")]
        [SerializeField] CanvasGroup[] groups;

        [Tooltip("切り替えにかける秒数")]
        [SerializeField] float fadeTime = 0.12f;

        void Awake()
        {
            foreach (var g in groups) if (g != null) g.alpha = 0f;
        }

        void Update()
        {
            int wanted = expander != null ? expander.SelectedIndex : -1;
            float step = Time.deltaTime / Mathf.Max(fadeTime, 0.0001f);

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null) continue;
                groups[i].alpha = Mathf.MoveTowards(groups[i].alpha, i == wanted ? 1f : 0f, step);
            }
        }
    }
}
