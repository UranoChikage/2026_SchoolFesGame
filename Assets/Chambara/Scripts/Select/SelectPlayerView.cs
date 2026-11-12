using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chambara
{
    /// <summary>
    /// 1人分のカーソル枠と「決めたマーク」の見た目（仮の演出）
    /// プレハブを指定しなければ、色付きの枠とラベルをその場で作る
    /// 演出を差し替えるときは、このクラスと同じように CharacterSelectManager のイベントを購読すればよい
    /// </summary>
    /// 

    public static class ViewName
    {
        public static Dictionary<string, string> MarkNames = new Dictionary<string, string>
        {
            // キャラクター（PlayerModels.asset）
            { "Johnson", "ジョンソン" },
            { "Jennifer", "ジェニファー" },
            { "Thomas", "トーマス" },
            { "Machiko", "マチコ" },
            { "Alien", "？？？" },

            // 武器（SwordModels.asset）
            { "Sinai", "竹刀" },
            { "Sword", "剣" },
            { "Bat", "バット" },
            { "Pikopiko", "ピコピコハンマー" },
            { "LightSword", "ライトソード" },
        };

        /// <summary>表示名を返す。登録が無ければ id をそのまま返す</summary>
        public static string Get(string id) =>
            id != null && MarkNames.TryGetValue(id, out var name) ? name : id;
    }



    public class SelectPlayerView : MonoBehaviour
    {
        [SerializeField] CharacterSelectManager manager;
        [SerializeField] int player;
        [SerializeField] Color color = Color.red;
        [Tooltip("枠やマークを置く場所。未設定なら一番上のCanvas")]
        [SerializeField] RectTransform overlayRoot;

        [Header("Cursor")]
        [Tooltip("カーソル枠のプレハブ（未設定なら作る）。アイコンの大きさに合わせて伸び縮みする")]
        [SerializeField] RectTransform cursorPrefab;
        [SerializeField] float frameThickness = 10f;
        [Tooltip("アイコンより何px大きくするか")]
        [SerializeField] float framePadding = 8f;
        [Tooltip("プレイヤー番号ごとに枠を何px大きくするか（同じアイコンに重なっても両方見えるように）")]
        [SerializeField] float paddingPerPlayer = 16f;
        [SerializeField] float followSpeed = 25f;
        [SerializeField] float moveBounce = 1.12f;

        [Header("Picked Mark")]
        [Tooltip("決めたアイコンに付けるマークのプレハブ（未設定なら作る）")]
        [SerializeField] RectTransform markPrefab;
        [Tooltip("1Pのマークを付ける位置（アイコン内の0〜1）。2Pは左右反転")]
        [SerializeField] Vector2 markAnchor = new Vector2(0f, 1f);

        [Header("Name Text")]
        [Tooltip("カーソルが乗っているキャラクターの名前を出すテキスト（任意）")]
        [SerializeField] TMP_Text characterNameText;
        [Tooltip("カーソルが乗っている武器の名前を出すテキスト（任意）")]
        [SerializeField] TMP_Text swordNameText;
        [Tooltip("まだ一度もカーソルが乗っていないときの表示")]
        [SerializeField] string emptyName = "";

        [Header("Ready")]
        [Tooltip("準備完了で表示するもの（任意）")]
        [SerializeField] GameObject readyObject;

        RectTransform cursor;
        float cursorScale = 1f;
        readonly RectTransform[] marks = new RectTransform[2];   // SelectCategory ごと

        string Label => $"{player + 1}P";
        Vector2 FramePadding => Vector2.one * (framePadding + paddingPerPlayer * player) * 2f;
        Vector2 MarkAnchor => player == 0 ? markAnchor : new Vector2(1f - markAnchor.x, markAnchor.y);

        void Start()
        {
            if (manager == null) manager = FindAnyObjectByType<CharacterSelectManager>();
            if (overlayRoot == null) overlayRoot = (RectTransform)manager.GetComponentInParent<Canvas>().rootCanvas.transform;

            cursor = cursorPrefab != null ? Instantiate(cursorPrefab, overlayRoot) : CreateFrame();
            cursor.SetAsLastSibling();
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i] = markPrefab != null ? Instantiate(markPrefab, overlayRoot) : CreateMark();
                marks[i].gameObject.SetActive(false);
            }
            if (readyObject != null) readyObject.SetActive(false);
            SetNameText(SelectCategory.Character, null);
            SetNameText(SelectCategory.Sword, null);
            ShowCursorName();

            manager.OnCursorMoved += OnCursorMoved;
            manager.OnPicked += OnPicked;
            manager.OnUnpicked += OnUnpicked;
            manager.OnReadyChanged += OnReadyChanged;

            SnapCursor();
        }

        void OnDestroy()
        {
            if (manager == null) return;
            manager.OnCursorMoved -= OnCursorMoved;
            manager.OnPicked -= OnPicked;
            manager.OnUnpicked -= OnUnpicked;
            manager.OnReadyChanged -= OnReadyChanged;
        }

        void LateUpdate()
        {
            // アイコンが動いても（登場アニメなど）付いていく
            var slot = manager.GetPlayer(player).cursor;
            if (slot != null)
            {
                Rect r = slot.GetRectIn(overlayRoot);
                float k = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
                cursor.localPosition = Vector2.Lerp(cursor.localPosition, r.center, k);
                cursor.sizeDelta = Vector2.Lerp(cursor.sizeDelta, r.size + FramePadding, k);
                cursorScale = Mathf.Lerp(cursorScale, 1f, k * 0.5f);
                cursor.localScale = Vector3.one * cursorScale;
            }

            for (int i = 0; i < marks.Length; i++)
            {
                var picked = manager.GetPlayer(player).GetPicked((SelectCategory)i);
                if (picked == null) continue;
                Rect r = picked.GetRectIn(overlayRoot);
                marks[i].localPosition = r.min + Vector2.Scale(r.size, MarkAnchor);
            }
        }

        void SnapCursor()
        {
            var slot = manager.GetPlayer(player).cursor;
            if (slot == null) return;
            Rect r = slot.GetRectIn(overlayRoot);
            cursor.localPosition = r.center;
            cursor.sizeDelta = r.size + FramePadding;
        }

        void OnCursorMoved(int p, SelectSlot from, SelectSlot to)
        {
            if (p != player) return;
            cursorScale = moveBounce;
            ShowCursorName();

            // キャラ⇔武器に移ったら、離れた側は決めたものの名前にする（決めていなければ直前の名前のまま）
            if (from != null && to != null && from.Category != to.Category)
            {
                var picked = manager.GetPlayer(player).GetPicked(from.Category);
                if (picked != null) SetNameText(from.Category, picked);
            }
        }

        void OnPicked(int p, SelectSlot slot)
        {
            if (p != player) return;
            var mark = marks[(int)slot.Category];
            mark.gameObject.SetActive(true);
            mark.SetAsLastSibling();
            cursor.SetAsLastSibling();
            StartCoroutine(Pop(mark));
        }

        void OnUnpicked(int p, SelectSlot slot)
        {
            if (p != player) return;
            // 別のものを選び直したときは OnPicked ですぐ付け直される
            if (manager.GetPlayer(player).GetPicked(slot.Category) == null)
                marks[(int)slot.Category].gameObject.SetActive(false);
        }

        /// <summary>
        /// カーソルが乗っているアイコンの名前を、その種類のテキストに出す
        /// もう一方の種類のテキストは触らない
        /// </summary>
        void ShowCursorName()
        {
            var slot = manager.GetPlayer(player).cursor;
            if (slot != null) SetNameText(slot.Category, slot);
        }

        void SetNameText(SelectCategory category, SelectSlot slot)
        {
            var text = category == SelectCategory.Character ? characterNameText : swordNameText;
            if (text == null) return;
            text.text = slot != null ? ViewName.Get(slot.Id) : emptyName;
        }

        void OnReadyChanged(int p, bool ready)
        {
            if (p != player || readyObject == null) return;
            readyObject.SetActive(ready);
        }

        IEnumerator Pop(RectTransform target)
        {
            const float duration = 0.2f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float x = t / duration;
                target.localScale = Vector3.one * (1f + Mathf.Sin(x * Mathf.PI) * 0.5f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        // ---- 仮の見た目を作る ----

        RectTransform CreateFrame()
        {
            var root = NewRect($"Cursor{Label}", overlayRoot);
            // 上下左右の4辺
            CreateEdge(root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, frameThickness));
            CreateEdge(root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, frameThickness));
            CreateEdge(root, new Vector2(0, 0), new Vector2(0, 1), new Vector2(frameThickness, 0));
            CreateEdge(root, new Vector2(1, 0), new Vector2(1, 1), new Vector2(frameThickness, 0));

            // ラベル（1Pは左上、2Pは右上）
            float side = player == 0 ? 0f : 1f;
            var tab = NewRect("Label", root);
            tab.anchorMin = tab.anchorMax = new Vector2(side, 1f);
            tab.pivot = new Vector2(side, 0f);
            tab.sizeDelta = new Vector2(90, 50);
            tab.gameObject.AddComponent<Image>().color = color;
            AddText(tab, Label, 40);
            return root;
        }

        void CreateEdge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var edge = NewRect("Edge", parent);
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.sizeDelta = size;
            var img = edge.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        RectTransform CreateMark()
        {
            var mark = NewRect($"Mark{Label}", overlayRoot);
            mark.sizeDelta = new Vector2(80, 80);
            var img = mark.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            AddText(mark, Label, 36);
            return mark;
        }

        static void AddText(RectTransform parent, string text, float size)
        {
            var rt = NewRect("Text", parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}
