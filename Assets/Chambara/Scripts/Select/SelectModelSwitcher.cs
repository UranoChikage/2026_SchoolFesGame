using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面で、1人分のプレビュー用モデル（キャラと手に持つ剣）を切り替える
    /// CharacterSelectManager.OnCursorMoved を購読し、カーソルが乗ったアイコンの id（= ModelList の名前）のモデルを出す
    /// ・キャラのアイコンに乗ったとき：そのキャラに切り替える（剣は決定済みならそれ、未決定なら今のまま）
    /// ・剣のアイコンに乗ったとき：その剣に切り替える（キャラは決定済みならそれ、未決定なら今のまま）
    /// 一度出したモデルは消さずに使い回す
    /// 動き（浮遊・ジャンプなど）は持たないので、modelRoot かその親を SelectModelMotion などで動かす
    /// </summary>
    public class SelectModelSwitcher : MonoBehaviour
    {
        [SerializeField] CharacterSelectManager manager;
        [SerializeField] int player;

        [Header("Models")]
        [SerializeField] ModelList playerModels;
        [SerializeField] ModelList swordModels;
        [Tooltip("剣のアイコンに乗るまで持たせておく剣（空なら持たせない）")]
        [SerializeField] string initialSwordId;

        [Header("Placement")]
        [Tooltip("モデルを置く親。+Z が正面になる。未設定なら子に \"Models\" を作る（カメラなど他の子と一緒に動かないように）")]
        [SerializeField] Transform modelRoot;
        [SerializeField] Vector3 modelOffset = Vector3.zero;
        [Tooltip("試合シーンと同じく Y90 で modelRoot の +Z を向く")]
        [SerializeField] Vector3 modelEuler = new Vector3(0f, 90f, 0f);
        [Tooltip("出したモデルのレイヤーを modelRoot と同じにする（別カメラで映す用）")]
        [SerializeField] bool useRootLayer = true;

        [Header("Sword")]
        [Tooltip("剣を持たせる手のパーツ名（モデルの子から探す）")]
        [SerializeField] string handName = "hand.R";
        [Tooltip("手の位置からのずれ（modelRoot の座標系）")]
        [SerializeField] Vector3 swordOffset = Vector3.zero;
        [Tooltip("剣の向き（modelRoot の座標系）。剣は刃が +Y に伸びている前提")]
        [SerializeField] Vector3 swordEuler = new Vector3(30f, 0f, 0f);
        [Tooltip("剣の大きさ（modelRoot に対する倍率）。試合シーンの Player.prefab の剣の pivot と同じ 0.24")]
        [SerializeField] float swordScale = 0.24f;

        class CharacterEntry
        {
            public GameObject model;
            public Transform swordHolder;
        }

        readonly Dictionary<string, CharacterEntry> characters = new Dictionary<string, CharacterEntry>();
        readonly Dictionary<string, GameObject> swords = new Dictionary<string, GameObject>();
        string characterId, swordId;
        CharacterEntry currentCharacter;
        GameObject currentSword;

        public int Player => player;
        public string CharacterId => characterId;
        public string SwordId => swordId;
        public GameObject CurrentCharacter => currentCharacter?.model;
        public GameObject CurrentSword => currentSword;

        /// <summary>表示中のキャラか剣が変わった</summary>
        public event Action<SelectModelSwitcher> OnModelChanged;

        /// <summary>モデルを置く親。モーションはこれを動かす</summary>
        public Transform ModelRoot => Root;
        Transform Root
        {
            get
            {
                if (modelRoot == null)
                {
                    modelRoot = new GameObject("Models").transform;
                    modelRoot.SetParent(transform, false);
                    modelRoot.gameObject.layer = gameObject.layer;
                }
                return modelRoot;
            }
        }

        void Start()
        {
            if (manager == null) manager = FindAnyObjectByType<CharacterSelectManager>();
            if (manager == null)
            {
                Debug.LogError("CharacterSelectManager が見つかりません");
                enabled = false;
                return;
            }
            swordId = initialSwordId;
            manager.OnCursorMoved += OnCursorMoved;

            // manager の Start で最初のカーソル位置が先に決まっていた場合
            var cursor = manager.GetPlayer(player)?.cursor;
            if (cursor != null) Show(cursor);
            else Apply();
        }

        void OnDestroy()
        {
            if (manager != null) manager.OnCursorMoved -= OnCursorMoved;
        }

        void OnCursorMoved(int p, SelectSlot from, SelectSlot to)
        {
            if (p != player || to == null) return;
            Show(to);
        }

        /// <summary>slot に合わせて表示を切り替える</summary>
        public void Show(SelectSlot slot)
        {
            var state = manager.GetPlayer(player);
            if (slot.Category == SelectCategory.Character)
            {
                characterId = slot.Id;
                if (state.sword != null) swordId = state.sword.Id;
            }
            else
            {
                swordId = slot.Id;
                if (state.character != null) characterId = state.character.Id;
            }
            Apply();
        }

        /// <summary>id を直接指定して切り替える（空・null なら非表示）</summary>
        public void Show(string character, string sword)
        {
            characterId = character;
            swordId = sword;
            Apply();
        }

        void Apply()
        {
            var nextCharacter = GetCharacter(characterId);
            var nextSword = GetSword(swordId);
            if (nextCharacter == currentCharacter && nextSword == currentSword) return;

            if (currentCharacter != null && currentCharacter != nextCharacter) currentCharacter.model.SetActive(false);
            if (currentSword != null && currentSword != nextSword) currentSword.SetActive(false);

            currentCharacter = nextCharacter;
            currentSword = nextSword;

            if (currentCharacter != null) currentCharacter.model.SetActive(true);
            if (currentSword != null)
            {
                // 剣は今のキャラの手に付け替える。キャラがいなければ出さない
                bool visible = currentCharacter != null;
                if (visible) currentSword.transform.SetParent(currentCharacter.swordHolder, false);
                currentSword.SetActive(visible);
            }

            OnModelChanged?.Invoke(this);
        }

        CharacterEntry GetCharacter(string id)
        {
            if (string.IsNullOrEmpty(id) || playerModels == null) return null;
            if (characters.TryGetValue(id, out var entry)) return entry;

            GameObject prefab = playerModels.GetModel(id);
            if (prefab != null)
            {
                var model = Instantiate(prefab, Root);
                model.name = prefab.name;
                model.transform.localPosition = modelOffset;
                model.transform.localRotation = Quaternion.Euler(modelEuler);
                SetupLayer(model);

                entry = new CharacterEntry { model = model, swordHolder = CreateSwordHolder(model.transform) };
                model.SetActive(false);
            }
            characters[id] = entry;   // 見つからなかったものも null で覚えておき、何度もエラーを出さない
            return entry;
        }

        GameObject GetSword(string id)
        {
            if (string.IsNullOrEmpty(id) || swordModels == null) return null;
            if (swords.TryGetValue(id, out var sword)) return sword;

            GameObject prefab = swordModels.GetModel(id);
            if (prefab != null)
            {
                sword = Instantiate(prefab, Root, false);
                sword.name = prefab.name;
                SetupLayer(sword);
                sword.SetActive(false);
            }
            swords[id] = sword;
            return sword;
        }

        /// <summary>手の位置に剣を付ける空のオブジェクトを作る。手が無ければモデルの原点</summary>
        Transform CreateSwordHolder(Transform model)
        {
            var holder = new GameObject("SwordHolder").transform;
            Transform hand = FindChild(model, handName);
            Vector3 handPos = model.position;
            if (hand != null)
            {
                handPos = hand.TryGetComponent(out Renderer r) ? r.bounds.center : hand.position;
            }
            else
            {
                Debug.LogWarning($"{model.name} に {handName} が見つかりません。剣はモデルの原点に付けます");
            }

            // 手のパーツの向き・スケールに左右されないよう、親はモデルにして向きは modelRoot 基準で決める
            holder.SetParent(model, true);
            holder.position = handPos + Root.TransformVector(swordOffset);
            holder.rotation = Root.rotation * Quaternion.Euler(swordEuler);
            // SetParent でワールドのスケールは 1 のまま保たれているので、そこに倍率をかける
            holder.localScale *= Root.lossyScale.x * swordScale;
            holder.gameObject.layer = model.gameObject.layer;
            return holder;
        }

        void SetupLayer(GameObject go)
        {
            if (!useRootLayer) return;
            int layer = Root.gameObject.layer;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }
    }
}
