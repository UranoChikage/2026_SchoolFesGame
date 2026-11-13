using UnityEngine;

namespace Title
{
    /// <summary>
    /// 選ばれているゲームの紹介文を切り替える。
    /// 紹介文はあらかじめ全部 Canvas に出しておき（文字を作り直さない）、CanvasGroup のアルファだけで見せる/隠す。
    /// 文字の差し替えが無いので、動的フォントの字形生成やメッシュ再構築が起きない。
    ///
    /// ■ 置き方
    ///   空の GameObject にこれを付け、紹介文の Text を子に並べる（パネルと同じ順で左→右）。
    ///   子の CanvasGroup は自動で集める（無ければ追加する）ので、groups の設定は不要。
    ///   Expander に SelectLayout を割り当てる。
    ///   日本語を出すので、各 Text の Font には NotoSansJP-Bold.ttf など日本語対応のものを指定すること。
    /// </summary>
    public class SelectDescription : MonoBehaviour
    {
        [Tooltip("どのパネルが選ばれているかを読む相手")]
        [SerializeField] SelectPanelExpander expander;

        [Tooltip("切り替えにかける秒数（出る側と消える側が同時に動く）")]
        [SerializeField] float fadeTime = 0.12f;

        CanvasGroup[] groups;

        void Awake()
        {
            int count = transform.childCount;
            groups = new CanvasGroup[count];
            for (int i = 0; i < count; i++)
            {
                var child = transform.GetChild(i);
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                groups[i] = group;
            }

            if (count == 0)
                Debug.LogWarning("SelectDescription: 子に紹介文がありません。Text を子として並べてください", this);
        }

        void Update()
        {
            int wanted = expander != null ? expander.SelectedIndex : -1;
            float step = Time.deltaTime / Mathf.Max(fadeTime, 0.0001f);

            for (int i = 0; i < groups.Length; i++)
            {
                float target = i == wanted ? 1f : 0f;
                if (groups[i].alpha != target)
                    groups[i].alpha = Mathf.MoveTowards(groups[i].alpha, target, step);
            }
        }
    }
}
