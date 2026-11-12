using UnityEngine;
using UnityEngine.UI;

namespace Title
{
    /// <summary>
    /// 選ばれているゲームの紹介文を、レガシー Text に出す（切り替わるときはフェードする）。
    ///
    /// ■ 置き方
    ///   紹介文用の Text(Legacy) にこれを付け、Expander に SelectLayout を割り当てる。
    ///   descriptions は SelectLayout の子と同じ順（左→右）。
    ///   日本語を出すので、Text の Font には NotoSansJP-Bold.ttf など日本語対応のものを指定すること。
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class SelectDescription : MonoBehaviour
    {
        [Tooltip("どのパネルが選ばれているかを読む相手")]
        [SerializeField] SelectPanelExpander expander;

        [Tooltip("パネルと同じ順の紹介文。1〜2行に収める（改行はそのまま入れてよい）")]
        [TextArea(2, 3)]
        [SerializeField] string[] descriptions =
        {
            "2〜4人 ／ 協力プレイ\n舵輪を回して荒波を越えろ！仲間と力を合わせ、大海原のお宝を奪え！",
            "2人専用 ／ 対戦プレイ\nJoy-Conを振って斬りかかれ！一瞬で勝負が決まる、短期決戦のチャンバラ！",
        };

        [Tooltip("切り替えの片道にかける秒数（消えて、現れるので合計はこの2倍）")]
        [SerializeField] float fadeTime = 0.12f;

        Text label;
        int shown = -1;      // いま表示している番号
        int wanted = -1;     // 表示したい番号
        float alpha;         // 0〜1
        Color baseColor;

        void Awake()
        {
            label = GetComponent<Text>();
            baseColor = label.color;
            label.text = "";
            SetAlpha(0f);
        }

        void Update()
        {
            if (expander != null) wanted = expander.SelectedIndex;

            // 表示中の文が違うなら、まず消す。消えきったら差し替えて、また出す
            if (wanted != shown)
            {
                alpha = Mathf.MoveTowards(alpha, 0f, Time.deltaTime / Mathf.Max(fadeTime, 0.0001f));
                if (alpha <= 0f)
                {
                    shown = wanted;
                    label.text = (shown >= 0 && shown < descriptions.Length) ? descriptions[shown] : "";
                }
            }
            else if (shown >= 0)
            {
                alpha = Mathf.MoveTowards(alpha, 1f, Time.deltaTime / Mathf.Max(fadeTime, 0.0001f));
            }

            SetAlpha(alpha);
        }

        void SetAlpha(float a)
        {
            Color c = baseColor;
            c.a = baseColor.a * a;
            label.color = c;
        }
    }
}
