using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面のプレビュー用モデルの動き
    /// ・待機中はふわふわ上下に浮く
    /// ・決定（CharacterSelectManager.OnPicked）したときに回転しながらジャンプする
    /// ・選んでいる最中は通常の顔、キャラと剣の両方を決めたら喜んだ顔にする（BodyFace があるキャラのみ）
    /// どのモデルが表示されているかは気にせず target を動かすだけ
    /// 同じオブジェクトに SelectModelSwitcher があれば、player と target（ModelRoot）はそちらに合わせる
    /// </summary>
    public class SelectModelMotion : MonoBehaviour
    {
        [SerializeField] CharacterSelectManager manager;
        [Tooltip("同じオブジェクトに SelectModelSwitcher があればそちらの番号を使う")]
        [SerializeField] int player;
        [Tooltip("動かすもの。未設定なら SelectModelSwitcher の ModelRoot、それも無ければ自分")]
        [SerializeField] Transform target;

        [Tooltip("動きの数値（Create > Chambara > SelectModelMotion で作る）")]
        [SerializeField] SelectModelMotionConfig config;

        SelectModelSwitcher switcher;
        Vector3 restPosition;      // target のローカル座標
        Quaternion restRotation;   // target のローカル回転
        float time;
        float jumpTimer = -1f;     // 0〜1 で進む。負ならジャンプしていない

        Transform Target => target != null ? target : transform;
        bool IsReady => manager != null && manager.GetPlayer(player) != null && manager.GetPlayer(player).IsReady;

        void Start()
        {
            if (manager == null) manager = FindAnyObjectByType<CharacterSelectManager>();
            if (TryGetComponent(out switcher))
            {
                player = switcher.Player;
                if (target == null) target = switcher.ModelRoot;
                switcher.OnModelChanged += OnModelChanged;
            }
            if (config == null)
            {
                Debug.LogWarning($"{name}: SelectModelMotionConfig が未設定なので初期値で動かします");
                config = ScriptableObject.CreateInstance<SelectModelMotionConfig>();
            }
            restPosition = Target.localPosition;
            restRotation = Target.localRotation;
            if (manager != null)
            {
                manager.OnPicked += OnPicked;
                manager.OnReadyChanged += OnReadyChanged;
            }
            UpdateFace();   // switcher が先にモデルを出していた場合
        }

        void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnPicked -= OnPicked;
                manager.OnReadyChanged -= OnReadyChanged;
            }
            if (switcher != null) switcher.OnModelChanged -= OnModelChanged;
        }

        void OnPicked(int p, SelectSlot slot)
        {
            if (p != player) return;
            if (slot.Category == SelectCategory.Sword && !config.jumpOnSword) return;
            Jump();
        }

        void OnReadyChanged(int p, bool ready)
        {
            if (p != player) return;
            UpdateFace();
        }

        // 切り替わったモデルにも今の表情を付ける
        void OnModelChanged(SelectModelSwitcher s) => UpdateFace();

        /// <summary>ジャンプさせる（跳んでいる途中でも最初から）</summary>
        public void Jump() => jumpTimer = 0f;

        /// <summary>表示中のモデルの表情を今の状態に合わせる</summary>
        public void UpdateFace()
        {
            if (config == null) return;
            var face = IsReady ? config.readyFace : config.selectingFace;
            foreach (var f in Target.GetComponentsInChildren<BodyFace>()) f.SetFace(face);
        }

        void LateUpdate()
        {
            // 選択画面はシーン読み込み前の演出中も動かしたいので実時間で進める
            float dt = Time.unscaledDeltaTime;
            time += dt;

            float y = Mathf.Sin(time * config.breathSpeed) * config.breathHeight;
            float spin = 0f;

            if (jumpTimer >= 0f)
            {
                jumpTimer += dt / Mathf.Max(config.jumpDuration, 0.01f);
                if (jumpTimer >= 1f)
                {
                    jumpTimer = -1f;
                }
                else
                {
                    y += 4f * config.jumpHeight * jumpTimer * (1f - jumpTimer);   // 放物線
                    spin = 360f * config.jumpSpins * Mathf.SmoothStep(0f, 1f, jumpTimer);   // 踏み切りと着地はゆっくり回る
                }
            }

            Target.localPosition = restPosition + Vector3.up * y;
            Target.localRotation = restRotation * Quaternion.Euler(0f, spin, 0f);
        }
    }
}
