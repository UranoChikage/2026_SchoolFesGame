using Common;
using UnityEngine;
using UnityEngine.UI;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面を Joy-Con のジャイロカーソルで操作する（Title の Select と同じ操作感）
    /// カーソルが乗ったアイコンを CharacterSelectManager の選択中にし、Confirm で決定、Cancel で戻る
    /// Joy-Con index i = プレイヤー i。繋がっていないプレイヤーはカーソルを隠し、元の入力（デバッグキーボード）だけで動く
    ///
    /// ■ 操作
    ///   ひねる/振る  … カーソル移動
    ///   スティック左右 … 感度調整（全員共通）
    ///   Confirm      … 決定
    ///   Cancel       … 戻る
    ///   Option       … コントローラー設定（持ち方・人数）を開く
    ///
    /// ■ 置き方
    ///   Canvas の下に付け、manager を割り当てる。cursorPrefab は Image を持つ RectTransform（Anchor/Pivot は中央）
    ///   cursorPrefab が未設定なら四角いカーソルを自動で作る
    /// </summary>
    public class GyroCursorSelect : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] CharacterSelectManager manager;
        [Tooltip("カーソルのプレハブ。プレイヤーごとに複製される。未設定なら自動生成")]
        [SerializeField] RectTransform cursorPrefab;
        [Tooltip("カーソルを置く親。移動範囲もこの大きさになる。未設定ならルートCanvas")]
        [SerializeField] RectTransform cursorParent;

        [Header("操作感")]
        [Tooltip("1度の回転あたりの移動量(px)")]
        [SerializeField] float sensitivity = 15f;
        [SerializeField] float sensitivityMin = 2f;
        [SerializeField] float sensitivityMax = 60f;
        [Tooltip("スティックを倒しきったときの1秒あたりの変化量")]
        [SerializeField] float sensitivityStep = 20f;
        [Tooltip("スティックのこれ以下の傾きは無視する")]
        [SerializeField] float stickDeadZone = 0.3f;
        [Tooltip("ローカル操作を許容する角度の広さ。1.41で±45度、2で±60度")]
        [SerializeField] float yawRelaxFactor = 2f;
        [Tooltip("重力推定の追従速度。小さいほど安定するが遅れる")]
        [SerializeField] float gravityBlend = 0.02f;
        [Tooltip("カーソルの追従の滑らかさ(秒)。小さいほどキビキビ動く")]
        [SerializeField] float smoothing = 0.06f;

        [Header("見た目")]
        [Tooltip("プレイヤーごとのカーソル色（1P, 2P）")]
        [SerializeField] Color[] playerColors = { new Color(1f, 0.35f, 0.35f), new Color(0.35f, 0.6f, 1f) };
        [Tooltip("自動生成するカーソルの大きさ(px)")]
        [SerializeField] float defaultCursorSize = 40f;
        [Tooltip("感度1変化あたりのスケール変化量")]
        [SerializeField] float scaleReaction = 0.02f;
        [Tooltip("スケールが元に戻る速さ(秒)")]
        [SerializeField] float scaleRecovery = 0.12f;
        [Tooltip("スケールの変化幅の上限")]
        [SerializeField] float scaleLimit = 0.35f;

        const int PlayerCount = CharacterSelectManager.PlayerCount;

        class Cursor
        {
            public RectTransform rect;
            public Vector2 position;   // 目標位置（cursorParent のローカル座標）
            public Vector2 velocity;
            public SelectSlot hovered;
            public readonly GyroPointer pointer = new GyroPointer();
            public Vector3 baseScale = Vector3.one;
            public float scaleOffset;
            public float scaleVelocity;
        }

        readonly Cursor[] cursors = new Cursor[PlayerCount];

        void Start()
        {
            if (manager == null) manager = FindAnyObjectByType<CharacterSelectManager>();
            if (cursorParent == null)
            {
                // 置いた GameObject が小さいと移動範囲がそこに制限されるので、既定は画面全体（ルートCanvas）にする
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) cursorParent = (RectTransform)canvas.rootCanvas.transform;
            }
            if (manager == null || cursorParent == null)
            {
                Debug.LogError("GyroCursorSelect: manager か cursorParent が見つかりません");
                enabled = false;
                return;
            }

            for (int i = 0; i < PlayerCount; i++)
            {
                cursors[i] = Create(i);
                // 元の入力（デバッグキーボード）は残したまま、Joy-Con のボタンを足す
                manager.SetInput(i, new JoyconSelectInput(i, manager.GetInput(i)));
            }
        }

        void Update()
        {
            bool selecting = manager.Phase == SelectPhase.Selecting;

            for (int i = 0; i < PlayerCount; i++)
            {
                bool connected = SwitchInput.I.IsConnected(i);
                var c = cursors[i];
                if (c.rect.gameObject.activeSelf != connected)
                {
                    c.rect.gameObject.SetActive(connected);
                    c.hovered = null;
                    if (!connected) c.pointer.Reset();   // 繋ぎ直したら重力を取り直す
                }
            }

            // コントローラー設定はどのプレイヤーからでも呼べる。開いたらこのフレームは何も読まない
            for (int i = 0; i < PlayerCount; i++)
            {
                if (SwitchInput.I.ButtonDown(i, SwitchButton.Option))
                {
                    SwitchInput.I.ShowControllerSupport();
                    ResetAll();
                    return;
                }
            }

            UpdateSensitivity();

            for (int i = 0; i < PlayerCount; i++)
            {
                var c = cursors[i];
                if (!c.rect.gameObject.activeSelf) continue;

                Move(i, c);

                // 乗っているアイコンが変わったときだけ選択中を移す
                // （キーボード操作や「キャラ決定後に剣へ移動」を毎フレーム上書きしないため）
                var hovered = FindHovered(c);
                if (hovered != c.hovered)
                {
                    c.hovered = hovered;
                    if (hovered != null && selecting) manager.MoveCursorTo(i, hovered);
                }
            }
        }

        Cursor Create(int index)
        {
            RectTransform rect;
            if (cursorPrefab != null)
            {
                rect = Instantiate(cursorPrefab, cursorParent);
            }
            else
            {
                var go = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
                rect = (RectTransform)go.transform;
                rect.SetParent(cursorParent, false);
                rect.sizeDelta = Vector2.one * defaultCursorSize;
            }
            rect.name = $"GyroCursor{index + 1}P";
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsLastSibling();   // アイコンより手前に出す

            var image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                if (playerColors.Length > 0) image.color = playerColors[index % playerColors.Length];
            }

            // 初期位置は最初に選択中のアイコン（無ければ左右に振り分け）
            Vector2 start = StartPosition(index);
            rect.localPosition = start;
            rect.gameObject.SetActive(SwitchInput.I.IsConnected(index));

            return new Cursor { rect = rect, position = start, baseScale = rect.localScale };
        }

        Vector2 StartPosition(int index)
        {
            var slot = manager.GetPlayer(index)?.cursor;
            if (slot != null) return slot.GetRectIn(cursorParent).center;
            return cursorParent.rect.center + new Vector2(index == 0 ? -200f : 200f, 0f);
        }

        void Move(int index, Cursor c)
        {
            Vector2 rate = c.pointer.Rate(index, yawRelaxFactor, gravityBlend);
            c.position += rate * sensitivity * Time.deltaTime;

            Rect r = cursorParent.rect;
            c.position = new Vector2(Mathf.Clamp(c.position.x, r.xMin, r.xMax),
                                     Mathf.Clamp(c.position.y, r.yMin, r.yMax));

            c.rect.localPosition = Vector2.SmoothDamp(c.rect.localPosition, c.position, ref c.velocity, smoothing);
        }

        SelectSlot FindHovered(Cursor c)
        {
            Vector2 p = c.rect.localPosition;
            foreach (var s in manager.Slots)
            {
                if (s == null || !s.isActiveAndEnabled) continue;
                if (s.GetRectIn(cursorParent).Contains(p)) return s;
            }
            return null;
        }

        /// <summary>全員ぶん初期化する（持ち方が変わった後など）。カーソルは選択中のアイコンへ戻す</summary>
        void ResetAll()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                var c = cursors[i];
                c.pointer.Reset();
                c.position = StartPosition(i);
                c.velocity = Vector2.zero;
                c.hovered = null;
            }
        }

        /// <summary>スティック左右で感度を調整する。全員共通の値を、誰のスティックでも動かせる</summary>
        void UpdateSensitivity()
        {
            float input = 0f;
            for (int i = 0; i < PlayerCount; i++)
            {
                if (!cursors[i].rect.gameObject.activeSelf) continue;
                float x = SwitchInput.I.Stick(i).x;
                if (Mathf.Abs(x) < stickDeadZone) continue;
                if (Mathf.Abs(x) > Mathf.Abs(input)) input = x;
            }

            if (input != 0f)
            {
                float amount = (Mathf.Abs(input) - stickDeadZone) / (1f - stickDeadZone);
                float before = sensitivity;
                sensitivity += Mathf.Sign(input) * amount * sensitivityStep * Time.deltaTime;
                sensitivity = Mathf.Clamp(sensitivity, sensitivityMin, sensitivityMax);

                float delta = sensitivity - before;
                if (delta != 0f)
                {
                    foreach (var c in cursors)
                        c.scaleOffset = Mathf.Clamp(c.scaleOffset + delta * scaleReaction, -scaleLimit, scaleLimit);
                }
            }

            foreach (var c in cursors)
            {
                if (!c.rect.gameObject.activeSelf) continue;
                c.scaleOffset = Mathf.SmoothDamp(c.scaleOffset, 0f, ref c.scaleVelocity, scaleRecovery);
                c.rect.localScale = c.baseScale * (1f + c.scaleOffset);
            }
        }

        /// <summary>Joy-Con の Confirm/Cancel を元の入力に足す（移動はジャイロカーソル側で行う）</summary>
        class JoyconSelectInput : ISelectInput
        {
            readonly int player;
            readonly ISelectInput fallback;

            public Vector2Int Move { get; private set; }
            public bool Confirm { get; private set; }
            public bool Cancel { get; private set; }

            public JoyconSelectInput(int player, ISelectInput fallback)
            {
                this.player = player;
                this.fallback = fallback;
            }

            public void Tick(float dt)
            {
                fallback?.Tick(dt);
                Move = fallback?.Move ?? Vector2Int.zero;
                Confirm = (fallback?.Confirm ?? false) || SwitchInput.I.ButtonDown(player, SwitchButton.Confirm);
                Cancel = (fallback?.Cancel ?? false) || SwitchInput.I.ButtonDown(player, SwitchButton.Cancel);
            }
        }
    }
}
