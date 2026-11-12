using UnityEngine;
using UnityEngine.UI;

namespace Title
{
    /// <summary>
    /// HorizontalLayoutGroup に並べたパネルのうち、選ばれた1枚を広げ、残りを狭くする。
    /// 幅は LayoutElement.flexibleWidth の比率で決め、SmoothDamp で滑らかに動かす。
    ///
    /// ■ 置き方
    ///   HorizontalLayoutGroup を持つ SelectLayout に付けるだけ。
    ///   子のパネルから LayoutElement を自動で集める（無ければ追加する）ので、設定は不要。
    ///   Select の Expander 欄にこれを割り当てると、カーソルが乗ったパネルが広がる。
    ///
    /// ■ 選ばれていないとき
    ///   最後に選んだパネルを保持する（JoyConだと「どちらにも触れていない」状態が作りにくいため）。
    ///   最初の1回が来るまでは 1:1 のまま。
    /// </summary>
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public class SelectPanelExpander : MonoBehaviour
    {
        [Header("幅の比率")]
        [Tooltip("選ばれたパネルの重み")]
        [SerializeField] float selectedWeight = 1.7f;

        [Tooltip("選ばれなかったパネルの重み")]
        [SerializeField] float otherWeight = 0.5f;

        [Tooltip("広がる/狭まるまでの目安の秒数")]
        [SerializeField] float smoothTime = 0.25f;

        LayoutElement[] panels;
        float[] weights;
        float[] velocities;
        int selected = -1;

        void Awake()
        {
            int count = transform.childCount;
            panels = new LayoutElement[count];
            weights = new float[count];
            velocities = new float[count];

            for (int i = 0; i < count; i++)
            {
                var child = transform.GetChild(i);
                var element = child.GetComponent<LayoutElement>();
                if (element == null) element = child.gameObject.AddComponent<LayoutElement>();

                // Image は画像の大きさを preferredWidth として返すので、0 にして比率だけで幅を決める
                element.minWidth = 0f;
                element.preferredWidth = 0f;
                element.flexibleWidth = 1f;

                panels[i] = element;
                weights[i] = 1f;
            }
        }

        /// <summary>いま広がっているパネルの番号。まだ選ばれていなければ -1</summary>
        public int SelectedIndex => selected;

        /// <summary>このパネルを広げる。負の値は無視（最後の選択を保持）</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= panels.Length) return;
            selected = index;
        }

        void Update()
        {
            for (int i = 0; i < panels.Length; i++)
            {
                float target = selected < 0 ? 1f : (i == selected ? selectedWeight : otherWeight);
                weights[i] = Mathf.SmoothDamp(weights[i], target, ref velocities[i], smoothTime);
                panels[i].flexibleWidth = weights[i];
            }
        }
    }
}
