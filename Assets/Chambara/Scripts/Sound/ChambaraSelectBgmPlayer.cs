using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面（ChambaraSelect）でBGMをループで流す
    /// 開始が確定したら（ZR）、シーンを読み込むまでの時間でフェードアウトする
    /// 選択シーンのどこかに1つ置き、clip に曲を入れる
    /// </summary>
    public class ChambaraSelectBgmPlayer : MonoBehaviour
    {
        [SerializeField] AudioClip clip;
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;
        [Tooltip("シーン開始時にこの時間をかけて音量を上げる（実時間・秒）。0 ならすぐ")]
        [SerializeField] float fadeInTime = 1f;
        [Tooltip("開始が確定してから音量を0にするまで（実時間・秒）。負の値なら CharacterSelectManager の loadDelay に合わせる")]
        [SerializeField] float fadeOutTime = -1f;
        [Tooltip("未設定ならシーンから探す")]
        [SerializeField] CharacterSelectManager manager;

        AudioSource source;
        float fade;          // 0〜1
        bool fadingOut;
        float fadeOutDuration;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;   // 2D
            source.ignoreListenerPause = true;
            if (manager == null) manager = FindFirstObjectByType<CharacterSelectManager>();
        }

        private void OnEnable()
        {
            if (manager != null) manager.OnPhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnPhaseChanged -= OnPhaseChanged;
        }

        private void Start()
        {
            if (clip == null)
            {
                Debug.LogWarning("ChambaraSelectBgmPlayer: 曲が未設定です");
                return;
            }
            source.clip = clip;
            fade = fadeInTime > 0f ? 0f : 1f;
            source.volume = volume * fade;
            source.Play();
        }

        void OnPhaseChanged(SelectPhase phase)
        {
            if (phase != SelectPhase.Loading) return;
            fadingOut = true;
            fadeOutDuration = fadeOutTime >= 0f ? fadeOutTime : (manager != null ? manager.LoadDelay : 0.5f);
        }

        private void Update()
        {
            if (!source.isPlaying) return;
            float dt = Time.unscaledDeltaTime;

            if (fadingOut)
            {
                fade = fadeOutDuration > 0f ? Mathf.MoveTowards(fade, 0f, dt / fadeOutDuration) : 0f;
                if (fade <= 0f) source.Stop();
            }
            else if (fadeInTime > 0f)
            {
                fade = Mathf.MoveTowards(fade, 1f, dt / fadeInTime);
            }
            source.volume = volume * fade;
        }
    }
}
