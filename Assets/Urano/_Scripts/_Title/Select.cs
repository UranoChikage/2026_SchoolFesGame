using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Common;

namespace Title
{
    /// <summary>
    /// Joy-Conのジャイロでカーソルを動かし、決定ボタンでシーンへ移動するデモ。
    /// 繋がっている人数ぶんカーソルを出し、誰か1人が決定した時点で遷移する。
    /// SwitchInput だけに依存するので、各ゲームの GameInput とは独立して動く。
    ///
    /// ■ 方式：プレイヤー空間ジャイロ（Jibb Smart / GyroWiki）
    ///   角速度をマウスの移動量として積算する。姿勢→オイラー角は使わない。
    ///   重力は「ヨーとロールをどちら向きに合成するか」の判定にだけ使うので、
    ///   重力の推定誤差がカーソル位置の誤差にならない。
    ///   縦持ち（ひねる操作＝ロール）でも横持ちでも同じコードで動く。
    ///
    /// ■ 操作
    ///   ひねる/振る  … カーソル移動
    ///   Confirm      … 決定（カーソルがボタンに乗っているとき）
    ///   Cancel       … カーソルを画面中央に戻す
    ///   Option       … コントローラー設定（持ち方・人数）を開く
    ///
    /// ■ 置き方
    ///   Canvas の下に空のGameObjectを作ってこれを付け、cursorPrefab と buttons を割り当てる。
    ///   cursorPrefab は Image を持つ RectTransform（Anchorは中央）。シーンに置かず、Prefab化しておく。
    ///   buttons は画面に並べた順（左→右）に入れること。
    /// </summary>
    public class Select : MonoBehaviour
    {

        [Header("参照")]
        [Tooltip("カーソルのプレハブ。人数ぶん複製される。Anchorは中央にすること")]
        [SerializeField] RectTransform cursorPrefab;

        [Tooltip("カーソルを置く親。未設定ならこのオブジェクトの親を使う")]
        [SerializeField] RectTransform cursorParent;

        [Tooltip("選択肢。画面に並べた順に入れる")]
        [SerializeField] RectTransform[] buttons;

        [Tooltip("buttons と同じ順で、移動先のシーン名")]
        [SerializeField] string[] sceneNames;

        [Header("操作感")]
        [Tooltip("1度の回転あたりの移動量(px)")]
        [SerializeField] float sensitivity = 15f;

        [Tooltip("スティックで調整できる下限")]
        [SerializeField] float sensitivityMin = 2f;

        [Tooltip("スティックで調整できる上限")]
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
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color hoverColor = new Color(1f, 0.85f, 0.4f);
        [Header("感度調整のリアクション")]
        [Tooltip("感度1変化あたりのスケール変化量")]
        [SerializeField] float scaleReaction = 0.02f;

        [Tooltip("スケールが元に戻る速さ(秒)")]
        [SerializeField] float scaleRecovery = 0.12f;

        [Tooltip("スケールの変化幅の上限")]
        [SerializeField] float scaleLimit = 0.35f;


        [Tooltip("プレイヤーごとのカーソル色。足りない分は先頭から使い回す")]
        [SerializeField]
        Color[] playerColors =
        {
            new Color(1f, 0.35f, 0.35f),  // 赤
            new Color(0.35f, 0.6f, 1f),   // 青
            new Color(0.4f, 0.9f, 0.5f),  // 緑
            new Color(1f, 0.85f, 0.35f),  // 黄
        };

        const int MaxCursors = 8;

        /// <summary>カーソル1人ぶん</summary>
        class Cursor
        {
            public RectTransform rect;
            public Vector2 position;
            public Vector2 velocity;
            public int hovered = -1;
            public readonly GyroPointer pointer = new GyroPointer();
            public Vector3 baseScale = Vector3.one;  // プレハブ本来のスケール
            public float scaleOffset;                // 元のスケールからのずれ
            public float scaleVelocity;              // SmoothDamp用
        }

        readonly Cursor[] cursors = new Cursor[MaxCursors];
        RectTransform parent;
        int spawned;     // 生成済みカーソル数
        bool decided;    // 二重遷移の防止

        void Start()
        {
            parent = cursorParent != null ? cursorParent : transform.parent as RectTransform;

            if (cursorPrefab == null || parent == null)
            {
                Debug.LogError("Select: cursorPrefab か 親Canvas が未設定");
                enabled = false;
                return;
            }

            ApplyColor();
        }

        void Update()
        {
            if (decided) return;

            Sync();
            // コントローラー設定はどのプレイヤーからでも呼べる。
            // アプレットは同期的に画面を奪うので、開いたらこのフレームは何も読まない
            for (int i = 0; i < spawned; i++)
            {
                if (!cursors[i].rect.gameObject.activeSelf) continue;
                if (SwitchInput.I.ButtonDown(i, SwitchButton.Option))
                {
                    SwitchInput.I.ShowControllerSupport();
                    ResetAll();
                    return;
                }
            }

            UpdateSensitivity();

            // コントローラー設定はどのプレイヤーからでも呼べる。
            // アプレットは同期的に画面を奪うので、開いたらこのフレームは何も読まない
            for (int i = 0; i < spawned; i++)
            {
                if (!cursors[i].rect.gameObject.activeSelf) continue;
                if (SwitchInput.I.ButtonDown(i, SwitchButton.Option))
                {
                    SwitchInput.I.ShowControllerSupport();
                    ResetAll();
                    return;
                }
            }

            for (int i = 0; i < spawned; i++)
            {
                Cursor c = cursors[i];
                if (!c.rect.gameObject.activeSelf) continue;

                if (SwitchInput.I.ButtonDown(i, SwitchButton.Cancel)) ResetCursor(c);

                Move(i, c);
                c.hovered = FindHovered(c);

                if (c.hovered >= 0 && SwitchInput.I.ButtonDown(i, SwitchButton.Confirm))
                {
                    Decide(c.hovered);
                    return;
                }
            }

            ApplyColor();

        }

        #region カーソルの生成・接続追従

        /// <summary>接続数に合わせてカーソルを増やす／余ったものを隠す</summary>
        void Sync()
        {
            int count = Mathf.Min(SwitchInput.I.ConnectedCount, MaxCursors);

            // 足りなければ作る（抜き差しを繰り返しても作り直さないよう、減ったときは隠すだけ）
            while (spawned < count)
            {
                cursors[spawned] = Create(spawned);
                spawned++;
            }

            for (int i = 0; i < spawned; i++)
            {
                bool active = i < count;
                if (cursors[i].rect.gameObject.activeSelf != active)
                {
                    cursors[i].rect.gameObject.SetActive(active);
                    if (!active)
                    {
                        cursors[i].hovered = -1;
                        cursors[i].pointer.Reset();   // 繋ぎ直したら重力を取り直す
                    }
                }
            }
        }

        Cursor Create(int index)
        {

            RectTransform rect = Instantiate(cursorPrefab, parent);
            rect.name = $"Cursor{index}";
            rect.gameObject.SetActive(true);

            // 初期位置を少しずつ横にずらして、重なって見えないようにする
            Vector2 start = new Vector2((index - (MaxCursors - 1) * 0.5f) * 60f, -200f);
            rect.anchoredPosition = start;

            var image = rect.GetComponent<Image>();
            if (image != null && playerColors.Length > 0)
                image.color = playerColors[index % playerColors.Length];

            return new Cursor { rect = rect, position = start, baseScale = rect.localScale };
        }

        #endregion

        #region ジャイロ → カーソル位置（プレイヤー空間）

        /// <summary>角速度をカーソルの移動量として積算する（動かし方は共通の GyroPointer）</summary>
        void Move(int index, Cursor c)
        {
            Vector2 rate = c.pointer.Rate(index, yawRelaxFactor, gravityBlend);
            c.position += rate * sensitivity * Time.deltaTime;
            c.position = Clamp(c.position);

            c.rect.anchoredPosition =
                Vector2.SmoothDamp(c.rect.anchoredPosition, c.position, ref c.velocity, smoothing);
        }

        /// <summary>カーソルを画面中央に戻す</summary>
        void ResetCursor(Cursor c)
        {
            c.position = Vector2.zero;
            c.velocity = Vector2.zero;
        }

        /// <summary>全員ぶん初期化する（持ち方が変わった後など）</summary>
        void ResetAll()
        {
            for (int i = 0; i < spawned; i++)
            {
                cursors[i].pointer.Reset();
                cursors[i].position = Vector2.zero;
                cursors[i].velocity = Vector2.zero;
                cursors[i].hovered = -1;
            }
        }

        /// <summary>カーソルを親の内側に収める</summary>
        Vector2 Clamp(Vector2 p)
        {
            Vector2 half = parent.rect.size * 0.5f;
            return new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
        }

        #endregion

        #region 選択

        /// <summary>そのカーソルが乗っているボタン。無ければ -1</summary>
        int FindHovered(Cursor c)
        {
            Vector2 p = c.rect.anchoredPosition;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                Rect r = buttons[i].rect;
                r.position += buttons[i].anchoredPosition;
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        /// <summary>誰か1人でも乗っていればハイライトする</summary>
        void ApplyColor()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var image = buttons[i].GetComponent<Image>();
                if (image == null) continue;

                bool hovered = false;
                for (int j = 0; j < spawned; j++)
                {
                    if (cursors[j].hovered == i) { hovered = true; break; }
                }
                image.color = hovered ? hoverColor : normalColor;
            }
        }

        void Decide(int button)
        {
            if (button < 0 || button >= sceneNames.Length) return;

            string scene = sceneNames[button];
            if (string.IsNullOrEmpty(scene))
            {
                Debug.LogWarning($"Select: シーン名が未設定 (index {button})");
                return;
            }

            decided = true;
            Pirates.SceneTransition.Load(scene);
        }
        /// <summary>
        /// スティック左右で感度を調整する。全員共通の値を、誰のスティックでも動かせる。
        /// 変化量ぶんカーソルのスケールを押し上げ、手応えとして見せる。
        /// </summary>
        void UpdateSensitivity()
        {
            float input = 0f;

            // 最も大きく倒しているプレイヤーの入力を採用する（複数人が触っても暴れない）
            for (int i = 0; i < spawned; i++)
            {
                if (!cursors[i].rect.gameObject.activeSelf) continue;

                float x = SwitchInput.I.Stick(i).x;
                if (Mathf.Abs(x) < stickDeadZone) continue;
                if (Mathf.Abs(x) > Mathf.Abs(input)) input = x;
            }

            if (input != 0f)
            {
                // デッドゾーンぶんを引いて 0〜1 に伸ばし直す
                float amount = (Mathf.Abs(input) - stickDeadZone) / (1f - stickDeadZone);

                float before = sensitivity;
                sensitivity += Mathf.Sign(input) * amount * sensitivityStep * Time.deltaTime;
                sensitivity = Mathf.Clamp(sensitivity, sensitivityMin, sensitivityMax);

                // 上下限に張り付いたら変化量は0になり、拡大したりするのは止まります。
                float delta = sensitivity - before;
                if (delta != 0f)
                {
                    for (int i = 0; i < spawned; i++)
                    {
                        cursors[i].scaleOffset = Mathf.Clamp(
                            cursors[i].scaleOffset + delta * scaleReaction, -scaleLimit, scaleLimit);
                    }
                }
            }

            UpdateScale();

        }

        void UpdateScale()
        {
            for (int i = 0; i < spawned; i++)
            {
                Cursor c = cursors[i];
                if (!c.rect.gameObject.activeSelf) continue;

                c.scaleOffset = Mathf.SmoothDamp(c.scaleOffset, 0f, ref c.scaleVelocity, scaleRecovery);
                c.rect.localScale = c.baseScale * (1f + c.scaleOffset);
            }
        }
        #endregion
    }
}