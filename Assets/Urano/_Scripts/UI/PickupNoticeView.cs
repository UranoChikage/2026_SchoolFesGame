using UnityEngine;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// 獲得通知1件ぶんの見た目（PickupNotifier が複製して並べる）。Prefabにして PickupNotifier に割り当てる。
    /// 宝箱のモデルは、遠く離れた専用ステージとカメラで自分専用の RenderTexture に描く（通知が同時に複数出るので1件ずつ持つ）。
    /// カメラは通知が出ている間だけ動く。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PickupNoticeView : MonoBehaviour
    {
        public struct ModelSettings
        {
            public int textureSize;
            public float distance;
            public float fov;
            public Vector3 euler;
            public float openDelay;
        }

        static readonly int OpenHash = Animator.StringToHash("Open");
        const float StageHeight = -5000f; // 本編から十分離れた場所
        const float StageSpacing = 50f;   // 通知同士のステージの間隔（カメラの映る範囲より大きく）
        static int stageCount;

        [SerializeField]
        [Tooltip("後ろに置く動かないスプライト（任意）")]
        Image backSprite;
        [SerializeField]
        [Tooltip("回転・脈動するエフェクト用スプライト（任意）。BackSpriteの手前")]
        Image effectSprite;
        [SerializeField]
        [Tooltip("アイテム・モデル未設定の宝箱のアイコン")]
        Image iconImage;
        [SerializeField]
        [Tooltip("宝箱モデルを映す RawImage（任意）")]
        RawImage modelImage;
        [SerializeField]
        [Tooltip("エフェクトスプライトの回転速度(度/秒)")]
        float effectSpinSpeed = 40f;
        [SerializeField]
        [Range(0f, 0.5f)]
        float effectPulse = 0.08f;
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("グラデーションが左端から右端まで進む時間(秒)。右端に着いたら折り返して往復する")]
        float gradientTime = 3f;

        public RectTransform Rect => (RectTransform)transform;

        CanvasGroup group;
        GameObject stage;
        Camera stageCamera;
        RenderTexture renderTexture;
        readonly System.Collections.Generic.Dictionary<GameObject, GameObject> models = new System.Collections.Generic.Dictionary<GameObject, GameObject>();
        GameObject curModel;
        Animator curAnimator;
        float openDelay;
        bool opened;
        Color effectColor;
        Gradient effectGradient;

        void Awake() => group = GetComponent<CanvasGroup>();

        void OnDestroy()
        {
            if (renderTexture != null) renderTexture.Release();
            if (stage != null) Destroy(stage);
        }

        /// <summary>通知の中身をセットして表示する。modelがあればモデル、無ければアイコン。gradientがあれば時間で色が変わり、無ければcolor一色（エフェクトスプライトの色）</summary>
        public void Show(Sprite icon, GameObject model, Color color, Gradient gradient, ModelSettings settings)
        {
            gameObject.SetActive(true);
            opened = false;
            openDelay = settings.openDelay;
            SetAlpha(1f);
            Rect.localScale = Vector3.zero;
            if (effectSprite != null)
            {
                effectSprite.rectTransform.localRotation = Quaternion.identity;
                SetEffectColor(color);
            }
            effectColor = color;
            effectGradient = gradient;

            bool useModel = model != null && modelImage != null;
            if (useModel) ShowModel(model, settings);

            if (modelImage != null) modelImage.enabled = useModel;
            if (iconImage != null)
            {
                iconImage.enabled = !useModel && icon != null;
                iconImage.sprite = icon;
            }
        }

        /// <summary>毎フレーム。age は出てからの秒数</summary>
        public void Tick(float age)
        {
            if (effectSprite != null)
            {
                RectTransform rt = effectSprite.rectTransform;
                rt.localRotation = Quaternion.Euler(0f, 0f, -age * effectSpinSpeed);
                rt.localScale = Vector3.one * (1f + Mathf.Sin(age * 6f) * effectPulse);
                SetEffectColor(effectGradient != null ? effectGradient.Evaluate(Mathf.PingPong(age / gradientTime, 1f)) : effectColor);
            }

            if (!opened && curAnimator != null && age >= openDelay)
            {
                opened = true;
                curAnimator.SetTrigger(OpenHash);
            }
        }

        // アルファはCanvasGroupで制御するので、色だけ使う
        void SetEffectColor(Color c) => effectSprite.color = new Color(c.r, c.g, c.b, 1f);

        /// <summary>モデルごと（CanvasGroupにRawImageも含まれる）フェードする</summary>
        public void SetAlpha(float a) => group.alpha = a;

        public void Hide()
        {
            if (stageCamera != null) stageCamera.enabled = false;
            if (curModel != null) curModel.SetActive(false);
            curModel = null;
            curAnimator = null;
            gameObject.SetActive(false);
        }

        void ShowModel(GameObject prefab, ModelSettings s)
        {
            EnsureStage(s);

            if (!models.TryGetValue(prefab, out GameObject instance) || instance == null)
            {
                instance = Instantiate(prefab, stage.transform);
                instance.transform.localPosition = new Vector3(0f, 0f, s.distance);
                instance.transform.localRotation = Quaternion.Euler(s.euler);
                models[prefab] = instance;
            }

            if (curModel != null && curModel != instance) curModel.SetActive(false);
            curModel = instance;
            curModel.SetActive(true);
            curAnimator = curModel.GetComponentInChildren<Animator>(true);
            if (curAnimator != null) curAnimator.Rebind(); // 閉じた状態から始める
            stageCamera.enabled = true;
        }

        void EnsureStage(ModelSettings s)
        {
            if (stage != null) return;

            stage = new GameObject("[PickupModelStage]");
            stage.transform.position = new Vector3(stageCount++ * StageSpacing, StageHeight, 0f);

            var camObj = new GameObject("StageCamera");
            camObj.transform.SetParent(stage.transform, false);
            stageCamera = camObj.AddComponent<Camera>();
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 透明で抜く
            stageCamera.fieldOfView = s.fov;
            stageCamera.nearClipPlane = 0.1f;
            stageCamera.farClipPlane = s.distance * 4f;
            stageCamera.allowHDR = false;
            stageCamera.allowMSAA = false;

            renderTexture = new RenderTexture(s.textureSize, s.textureSize, 16, RenderTextureFormat.ARGB32);
            stageCamera.targetTexture = renderTexture;
            stageCamera.enabled = false;
            modelImage.texture = renderTexture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => stageCount = 0;
    }
}
