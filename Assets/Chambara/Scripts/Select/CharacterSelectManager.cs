using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chambara
{
    public enum SelectPhase
    {
        Selecting,  // 選んでいる最中
        Starting,   // 2人とも決まって、開始までの待ち時間（戻るで取り消せる）
        Loading     // 開始が確定して、シーンを読み込むまでの演出時間
    }

    /// <summary>
    /// 2人用キャラ選択画面の流れ
    /// カーソルはキャラ・剣のどちらの枠にも自由に移動でき、決定するとその種類の選択が上書きされる
    /// キャラと剣の両方が決まったプレイヤーは準備完了。2人とも準備完了ならしばらくして試合シーンへ
    /// 見た目は持たないので、演出は各イベントを購読して付ける（SelectPlayerView 参照）
    /// </summary>
    public class CharacterSelectManager : MonoBehaviour
    {
        public const int PlayerCount = ChambaraSelection.PlayerCount;

        public class PlayerState
        {
            public SelectSlot cursor;
            public SelectSlot character;
            public SelectSlot sword;
            public readonly List<SelectCategory> pickOrder = new List<SelectCategory>(); // 戻るで新しい方から取り消す
            public bool IsReady => character != null && sword != null;

            public SelectSlot GetPicked(SelectCategory c) => c == SelectCategory.Character ? character : sword;
            public void SetPicked(SelectCategory c, SelectSlot slot)
            {
                if (c == SelectCategory.Character) character = slot;
                else sword = slot;
            }
        }

        [Header("Slots")]
        [Tooltip("この子がキャラのアイコン（SelectSlot が無ければ自動で付ける）")]
        [SerializeField] Transform characterRoot;
        [Tooltip("この子が剣のアイコン（SelectSlot が無ければ自動で付ける）")]
        [SerializeField] Transform swordRoot;
        [Tooltip("カーソル移動の判定に使う座標系。未設定なら親のCanvas")]
        [SerializeField] RectTransform navigationSpace;

        [Header("Cursor")]
        [Tooltip("最初にカーソルがあるキャラの番号（1P, 2P）")]
        [SerializeField] int[] startCharacterIndex = { 0, -1 };   // -1 は最後
        [Tooltip("キャラを決めたら剣のアイコンへカーソルを移す（剣がまだなら）")]
        [SerializeField] bool jumpToSwordAfterCharacter = true;
        [Tooltip("移動方向からこの角度以内にあるアイコンだけを候補にする")]
        [SerializeField, Range(10f, 89f)] float navigationAngle = 60f;

        [Header("Start")]
        [Tooltip("2人とも準備完了してから開始が確定するまで（実時間）。この間は戻るで取り消せる")]
        [SerializeField] float startDelay = 1.0f;
        [Tooltip("開始が確定してからシーンを読み込むまで（実時間）。暗転などの演出用")]
        [SerializeField] float loadDelay = 0.5f;
        [SerializeField] string gameSceneName = "Chambara";

        [Header("Debug")]
        [Tooltip("キーボードで操作する（1P: WASD/Space/左Shift  2P: テンキー8462/5/0）")]
        [SerializeField] bool useDebugKeyboard = true;

        readonly List<SelectSlot> slots = new List<SelectSlot>();
        readonly PlayerState[] players = new PlayerState[PlayerCount];
        readonly ISelectInput[] inputs = new ISelectInput[PlayerCount];
        SelectPhase phase = SelectPhase.Selecting;
        Coroutine startRoutine;

        public IReadOnlyList<SelectSlot> Slots => slots;
        public SelectPhase Phase => phase;
        public PlayerState GetPlayer(int player) => players[player];
        public bool AllReady => players[0].IsReady && players[1].IsReady;
        public float StartDelay => startDelay;
        public float LoadDelay => loadDelay;

        // ---- 演出用イベント ----
        public event Action<int, SelectSlot, SelectSlot> OnCursorMoved;      // player, from, to
        public event Action<int, SelectSlot> OnPicked;                       // player, slot
        public event Action<int, SelectSlot> OnUnpicked;                     // player, 取り消された slot
        public event Action<int, bool> OnReadyChanged;                       // player, ready
        public event Action<int> OnCursorBlocked;                            // その方向にアイコンが無かった
        public event Action<int> OnBackRequested;                            // 何も選んでいない状態で戻るを押した
        public event Action<SelectPhase> OnPhaseChanged;

        void Awake()
        {
            CollectSlots(characterRoot, SelectCategory.Character);
            CollectSlots(swordRoot, SelectCategory.Sword);

            if (navigationSpace == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null && slots.Count > 0) canvas = slots[0].GetComponentInParent<Canvas>();
                if (canvas != null) navigationSpace = (RectTransform)canvas.rootCanvas.transform;
            }

            for (int i = 0; i < PlayerCount; i++)
            {
                players[i] = new PlayerState();
                if (useDebugKeyboard && inputs[i] == null) inputs[i] = new KeyboardSelectInput(i);
            }
        }

        void Start()
        {
            var characters = slots.FindAll(s => s.Category == SelectCategory.Character);
            if (characters.Count == 0)
            {
                Debug.LogError("キャラのアイコンが見つかりません。characterRoot を設定してください");
                enabled = false;
                return;
            }
            for (int i = 0; i < PlayerCount; i++)
            {
                int index = i < startCharacterIndex.Length ? startCharacterIndex[i] : 0;
                if (index < 0 || index >= characters.Count) index = characters.Count - 1;
                MoveCursorTo(i, characters[index]);
            }
        }

        void CollectSlots(Transform root, SelectCategory category)
        {
            if (root == null) return;
            foreach (Transform child in root)
            {
                if (!child.gameObject.activeSelf) continue;
                var slot = child.GetComponent<SelectSlot>();
                if (slot == null) slot = child.gameObject.AddComponent<SelectSlot>();
                slot.Setup(category);
                slots.Add(slot);
            }
        }

        /// <summary>入力元を差し替える（Joy-Con 用など）</summary>
        public void SetInput(int player, ISelectInput input) => inputs[player] = input;

        void Update()
        {
            if (phase == SelectPhase.Loading) return;

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < PlayerCount; i++)
            {
                var input = inputs[i];
                if (input == null) continue;
                input.Tick(dt);

                if (input.Cancel) Cancel(i);
                else if (input.Confirm) Confirm(i);
                else if (input.Move != Vector2Int.zero) MoveCursor(i, input.Move);
            }
        }

        // ---- 操作 ----

        public void MoveCursor(int player, Vector2Int dir)
        {
            if (phase != SelectPhase.Selecting) return;
            var next = FindNeighbor(players[player].cursor, dir);
            if (next == null) OnCursorBlocked?.Invoke(player);
            else MoveCursorTo(player, next);
        }

        public void MoveCursorTo(int player, SelectSlot slot)
        {
            var p = players[player];
            var from = p.cursor;
            if (from == slot) return;
            p.cursor = slot;
            if (from != null) from.NotifyHoverExit(player);
            slot.NotifyHoverEnter(player);
            OnCursorMoved?.Invoke(player, from, slot);
        }

        public void Confirm(int player)
        {
            // 待ち時間中に決定 → すぐ開始
            if (phase == SelectPhase.Starting)
            {
                BeginLoading();
                return;
            }

            var p = players[player];
            var slot = p.cursor;
            if (slot == null) return;

            bool wasReady = p.IsReady;
            var old = p.GetPicked(slot.Category);
            if (old != null)
            {
                if (old == slot) return;   // 同じものを選び直しても何もしない
                p.pickOrder.Remove(slot.Category);
                old.NotifyUnpicked(player);
                OnUnpicked?.Invoke(player, old);
            }

            p.SetPicked(slot.Category, slot);
            p.pickOrder.Add(slot.Category);
            slot.NotifyPicked(player);
            OnPicked?.Invoke(player, slot);

            if (jumpToSwordAfterCharacter && slot.Category == SelectCategory.Character && p.sword == null)
            {
                var sword = slots.Find(s => s.Category == SelectCategory.Sword);
                if (sword != null) MoveCursorTo(player, sword);
            }

            if (!wasReady && p.IsReady) OnReadyChanged?.Invoke(player, true);
            if (AllReady) BeginStarting();
        }

        public void Cancel(int player)
        {
            var p = players[player];
            if (p.pickOrder.Count == 0)
            {
                if (phase == SelectPhase.Selecting) OnBackRequested?.Invoke(player);
                return;
            }

            bool wasReady = p.IsReady;
            var category = p.pickOrder[p.pickOrder.Count - 1];
            p.pickOrder.RemoveAt(p.pickOrder.Count - 1);
            var slot = p.GetPicked(category);
            p.SetPicked(category, null);
            MoveCursorTo(player, slot);   // 取り消したアイコンにカーソルを戻す
            slot.NotifyUnpicked(player);
            OnUnpicked?.Invoke(player, slot);

            if (wasReady) OnReadyChanged?.Invoke(player, false);
            if (phase == SelectPhase.Starting) CancelStarting();
        }

        // ---- 開始 ----

        void BeginStarting()
        {
            SetPhase(SelectPhase.Starting);
            startRoutine = StartCoroutine(StartingRoutine());
        }

        void CancelStarting()
        {
            if (startRoutine != null) StopCoroutine(startRoutine);
            startRoutine = null;
            SetPhase(SelectPhase.Selecting);
        }

        IEnumerator StartingRoutine()
        {
            yield return new WaitForSecondsRealtime(startDelay);
            startRoutine = null;
            BeginLoading();
        }

        void BeginLoading()
        {
            if (startRoutine != null) StopCoroutine(startRoutine);
            startRoutine = null;

            for (int i = 0; i < PlayerCount; i++)
                ChambaraSelection.Set(i, players[i].character.Id, players[i].sword.Id);

            SetPhase(SelectPhase.Loading);
            StartCoroutine(LoadRoutine());
        }

        IEnumerator LoadRoutine()
        {
            yield return new WaitForSecondsRealtime(loadDelay);
            Debug.Log($"1P: {ChambaraSelection.PlayerModelNames[0]} / {ChambaraSelection.SwordModelNames[0]}  " +
                      $"2P: {ChambaraSelection.PlayerModelNames[1]} / {ChambaraSelection.SwordModelNames[1]}");
            if (!string.IsNullOrEmpty(gameSceneName)) SceneManager.LoadScene(gameSceneName);
        }

        void SetPhase(SelectPhase next)
        {
            if (phase == next) return;
            phase = next;
            OnPhaseChanged?.Invoke(next);
        }

        // ---- カーソル移動先探し ----

        /// <summary>
        /// 画面上の位置から、dir 方向にあるいちばん近いアイコンを探す（並べ方に依存しない）
        /// 横方向にずれているものほど遠い扱いにする
        /// </summary>
        SelectSlot FindNeighbor(SelectSlot from, Vector2Int dir)
        {
            if (from == null || navigationSpace == null) return null;
            Vector2 d = ((Vector2)dir).normalized;
            Vector2 origin = from.GetRectIn(navigationSpace).center;
            float maxTan = Mathf.Tan(navigationAngle * Mathf.Deg2Rad);

            SelectSlot best = null;
            float bestScore = float.MaxValue;
            foreach (var s in slots)
            {
                if (s == from || !s.isActiveAndEnabled) continue;
                Vector2 v = s.GetRectIn(navigationSpace).center - origin;
                float along = Vector2.Dot(v, d);
                if (along <= 1f) continue;
                float side = Mathf.Abs(v.x * d.y - v.y * d.x);
                if (side > along * maxTan) continue;
                float score = along + side * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }
    }
}
