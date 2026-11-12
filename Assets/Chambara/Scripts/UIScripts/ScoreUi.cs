using System.Collections;
using UnityEngine;
using UnityEngine.UI;
// using TMPro; // 旧システム用

namespace Chambara
{
    #region 旧システム（テキスト＋Animator）
    // [System.Serializable]
    // public class ScoreUiData
    // {
    //     [SerializeField] private TextMeshProUGUI beforeScore;
    //     [SerializeField] private TextMeshProUGUI afterScore;
    //
    //     [SerializeField] private Animator animator;
    //     private const string TriggerName = "Action";
    //
    //     public void SetScore(int before, int after)
    //     {
    //         beforeScore.text = before.ToString();
    //         afterScore.text = after.ToString();
    //     }
    //
    //     public void PlayAnimation()
    //     {
    //         animator.SetTrigger(TriggerName);
    //     }
    // }
    #endregion

    /// <summary>
    /// 1プレイヤー分のスコア表示（アイコン1種類 × 2枠）
    /// </summary>
    [System.Serializable]
    public class ScoreIconData
    {
        [Tooltip("このプレイヤーのアイコン（全枠共通）")]
        [SerializeField] private Sprite icon;
        [Tooltip("アイコンを表示する枠（左から1本目、2本目）")]
        [SerializeField] private Image[] slots = new Image[2];

        public int Count => slots != null ? slots.Length : 0;

        public Image GetIcon(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length) return null;
            return slots[index];
        }

        // 全枠にアイコンを反映
        public void SetSprite(Sprite sprite)
        {
            icon = sprite;
            ApplySprite();
        }

        public void ApplySprite()
        {
            if (icon == null) return;
            for (int i = 0; i < Count; i++)
            {
                if (slots[i] != null) slots[i].sprite = icon;
            }
        }
    }

    public class ScoreUi : MonoBehaviour
    {
        #region 旧システム
        // [SerializeField] private ScoreUiData player1ScoreUi;
        // [SerializeField] private ScoreUiData player2ScoreUi;
        #endregion

        [Header("アイコン")]
        [SerializeField] private ScoreIconData player1Icons;
        [SerializeField] private ScoreIconData player2Icons;

        [Header("キャラクター別アイコン")]
        [Tooltip("キャラクター名ごとのアイコン（CharacterIcons.asset）。見つからないときは各プレイヤーの icon のまま")]
        [SerializeField] private CharacterIconList characterIcons;

        [Header("色")]
        [Tooltip("取得済みアイコンの色")]
        [SerializeField] private Color filledColor = Color.white;
        [Tooltip("未取得アイコンの色")]
        [SerializeField] private Color emptyColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Header("取得演出")]
        [Tooltip("取得時の開始スケール（基底サイズに対する倍率）")]
        [SerializeField] private float popStartScale = 2.0f;
        [Tooltip("基底サイズに戻るまでの時間（秒）")]
        [SerializeField] private float popDuration = 0.4f;
        [Tooltip("timeScaleの影響を受けないようにする")]
        [SerializeField] private bool useUnscaledTime = true;

        [SerializeField] private GameObject parent;

        private Vector3[] p1BaseScales;
        private Vector3[] p2BaseScales;
        private Coroutine[] p1Pops;
        private Coroutine[] p2Pops;

        // キャラクターに合わせたアイコンを反映済みか（1P, 2P）
        private readonly bool[] characterIconApplied = new bool[2];

        private void Awake()
        {
            p1BaseScales = CacheBaseScales(player1Icons);
            p2BaseScales = CacheBaseScales(player2Icons);
            p1Pops = new Coroutine[player1Icons.Count];
            p2Pops = new Coroutine[player2Icons.Count];

            player1Icons.ApplySprite();
            player2Icons.ApplySprite();
        }

        private void Start()
        {
            parent.SetActive(false);
            ChambaraManager.Instance.OnGameStateChanged += OnGameStateChanged;

            UpdateScore(0, 0, 0);

            // プレイヤーは ChambaraManager.Start で生成されるので、生成を待ってからキャラクター別アイコンを反映
            StartCoroutine(WaitAndApplyCharacterIcons());
        }

        private IEnumerator WaitAndApplyCharacterIcons()
        {
            while (!(characterIconApplied[0] && characterIconApplied[1]))
            {
                ApplyCharacterIcons();
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (ChambaraManager.Instance != null) ChambaraManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }

        // 次のラウンドが始まったら隠す
        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.Ready) Hide();
        }

        public void Hide()
        {
            StopAllPops();
            parent.SetActive(false);
        }

        #region 旧システム
        // //winner is 1 for player 1 and 2 for player 2
        // public void UpdateScore(int score1,int score2,int winner)
        // {
        //     parent.SetActive(true);
        //
        //     if (winner == 1)
        //     {
        //         player1ScoreUi.SetScore(score1 - 1, score1);
        //         player1ScoreUi.PlayAnimation();
        //
        //         player2ScoreUi.SetScore(score2, score2);
        //     }
        //     else if (winner == 2)
        //     {
        //         player2ScoreUi.SetScore(score2 - 1, score2);
        //         player2ScoreUi.PlayAnimation();
        //
        //         player1ScoreUi.SetScore(score1, score1);
        //     }
        // }
        #endregion

        /// <summary>
        /// スコア表示を更新する。winner は 1P=1 / 2P=2（それ以外は演出なし）
        /// 今回取得した枠だけ拡大→基底サイズの演出が入る
        /// </summary>
        public void UpdateScore(int score1, int score2, int winner)
        {
            parent.SetActive(true);
            StopAllPops();
            ApplyCharacterIcons();

            ApplyIcons(player1Icons, p1BaseScales, score1);
            ApplyIcons(player2Icons, p2BaseScales, score2);

            if (winner == 1) PlayPop(1, score1 - 1);
            else if (winner == 2) PlayPop(2, score2 - 1);
        }

        /// <summary>
        /// 指定プレイヤー（1 or 2）のアイコンSpriteを変更する
        /// </summary>
        public void SetIconSprite(int player, Sprite sprite)
        {
            GetIcons(player)?.SetSprite(sprite);
            // 手動で変えたら、キャラクター別アイコンで上書きしない
            if (player == 1 || player == 2) characterIconApplied[player - 1] = true;
        }

        /// <summary>
        /// 指定プレイヤー（1 or 2）のアイコンを、キャラクター名に対応するものに変更する
        /// </summary>
        public void SetIconByCharacter(int player, string characterName)
        {
            Sprite sprite = FindCharacterIcon(characterName);
            if (sprite != null) SetIconSprite(player, sprite);
        }

        /// <summary>
        /// キャラクター名に対応するアイコンを返す（無ければ null）
        /// </summary>
        public Sprite FindCharacterIcon(string characterName)
        {
            return characterIcons != null ? characterIcons.GetIcon(characterName) : null;
        }

        // 各プレイヤーのキャラクターに合わせてアイコンを変える（プレイヤー生成後に1回だけ）
        private void ApplyCharacterIcons()
        {
            PlayerBehaviour[] players = ChambaraManager.Instance != null ? ChambaraManager.Instance.Players : null;
            if (players == null) return;

            for (int i = 0; i < characterIconApplied.Length && i < players.Length; i++)
            {
                if (characterIconApplied[i] || players[i] == null) continue;
                string modelName = players[i].ModelName;
                if (string.IsNullOrEmpty(modelName)) continue;

                characterIconApplied[i] = true;
                Sprite sprite = FindCharacterIcon(modelName);
                if (sprite != null) GetIcons(i + 1).SetSprite(sprite);
                else Debug.LogWarning($"{name}: キャラクター \"{modelName}\" のアイコンが characterIcons に無いので、{i + 1}P の icon のままにします");
            }
        }

        /// <summary>
        /// 全アイコンを未取得（灰色）状態に戻す
        /// </summary>
        public void ResetIcons()
        {
            StopAllPops();
            ApplyIcons(player1Icons, p1BaseScales, 0);
            ApplyIcons(player2Icons, p2BaseScales, 0);
        }

        // ---- 内部処理 ----

        private ScoreIconData GetIcons(int player)
        {
            if (player == 1) return player1Icons;
            if (player == 2) return player2Icons;
            return null;
        }

        private static Vector3[] CacheBaseScales(ScoreIconData data)
        {
            var scales = new Vector3[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                Image icon = data.GetIcon(i);
                scales[i] = icon != null ? icon.rectTransform.localScale : Vector3.one;
            }
            return scales;
        }

        // score 個分を取得済み色、残りを灰色にし、スケールを基底に戻す
        private void ApplyIcons(ScoreIconData data, Vector3[] baseScales, int score)
        {
            for (int i = 0; i < data.Count; i++)
            {
                Image icon = data.GetIcon(i);
                if (icon == null) continue;
                icon.color = i < score ? filledColor : emptyColor;
                icon.rectTransform.localScale = baseScales[i];
            }
        }

        private void PlayPop(int player, int index)
        {
            ScoreIconData data = GetIcons(player);
            Image icon = data?.GetIcon(index);
            if (icon == null) return;

            Vector3[] baseScales = player == 1 ? p1BaseScales : p2BaseScales;
            Coroutine[] pops = player == 1 ? p1Pops : p2Pops;

            pops[index] = StartCoroutine(PopRoutine(icon.rectTransform, baseScales[index]));
        }

        private IEnumerator PopRoutine(RectTransform target, Vector3 baseScale)
        {
            Vector3 startScale = baseScale * popStartScale;
            target.localScale = startScale;

            float t = 0f;
            while (t < popDuration)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float p = Mathf.Clamp01(t / popDuration);
                // EaseOutCubic：最初は速く縮み、最後にゆっくりハマる
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                target.localScale = Vector3.LerpUnclamped(startScale, baseScale, eased);
                yield return null;
            }
            target.localScale = baseScale;
        }

        private void StopAllPops()
        {
            StopPops(p1Pops, player1Icons, p1BaseScales);
            StopPops(p2Pops, player2Icons, p2BaseScales);
        }

        private void StopPops(Coroutine[] pops, ScoreIconData data, Vector3[] baseScales)
        {
            if (pops == null) return;
            for (int i = 0; i < pops.Length; i++)
            {
                if (pops[i] != null) StopCoroutine(pops[i]);
                pops[i] = null;
                Image icon = data.GetIcon(i);
                if (icon != null) icon.rectTransform.localScale = baseScales[i];
            }
        }
    }
}
