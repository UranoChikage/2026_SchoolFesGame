using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// CharacterSelectManager のイベントに合わせて ChambaraSelectSoundConfig の効果音を鳴らす
    /// 選択シーンのどこかに1つ置き、config に ChambaraSelectSoundConfig を入れる
    /// 決定と同時にカーソルが動く（剣へ自動移動・取り消しで戻る）ときなどに音が重ならないよう、
    /// 1フレーム分のイベントをまとめてから優先度の高い音だけを鳴らす
    /// </summary>
    public class ChambaraSelectSoundPlayer : MonoBehaviour
    {
        [SerializeField] ChambaraSelectSoundConfig config;
        [Tooltip("未設定ならシーンから探す")]
        [SerializeField] CharacterSelectManager manager;
        [Tooltip("ON: 試合開始の音をシーン切り替え後も最後まで鳴らす")]
        [SerializeField] bool keepStartSoundAcrossScene = true;
        [SerializeField] int voiceCount = 4;   // 同時に鳴らせる数

        AudioSource[] sources;
        int next;

        // このフレームに起きたこと
        bool moved, picked, unpicked, readied, started;

        private void Awake()
        {
            sources = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < sources.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;   // 2D
                sources[i] = src;
            }
            if (manager == null) manager = FindFirstObjectByType<CharacterSelectManager>();
        }

        private void OnEnable()
        {
            if (manager == null) return;
            manager.OnCursorMoved += OnCursorMoved;
            manager.OnPicked += OnPicked;
            manager.OnUnpicked += OnUnpicked;
            manager.OnReadyChanged += OnReadyChanged;
            manager.OnPhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.OnCursorMoved -= OnCursorMoved;
            manager.OnPicked -= OnPicked;
            manager.OnUnpicked -= OnUnpicked;
            manager.OnReadyChanged -= OnReadyChanged;
            manager.OnPhaseChanged -= OnPhaseChanged;
        }

        // 最初のカーソル配置（from == null）では鳴らさない
        void OnCursorMoved(int player, SelectSlot from, SelectSlot to) { if (from != null) moved = true; }
        void OnPicked(int player, SelectSlot slot) => picked = true;
        void OnUnpicked(int player, SelectSlot slot) => unpicked = true;
        void OnReadyChanged(int player, bool ready) { if (ready) readied = true; }
        void OnPhaseChanged(SelectPhase phase) { if (phase == SelectPhase.Loading) started = true; }

        private void LateUpdate()
        {
            if (config != null)
            {
                if (started) PlayStart();
                else if (readied) Play(config.Ready);          // 準備完了は決定も兼ねる
                else if (picked) Play(config.Pick);
                else if (unpicked) Play(config.Unpick);
                else if (moved) Play(config.Move);
            }
            moved = picked = unpicked = readied = started = false;
        }

        void PlayStart()
        {
            AudioClip clip = Play(config.GameStart);
            if (clip == null || !keepStartSoundAcrossScene) return;
            // シーンを読み込んでも消えないようにし、鳴り終わったら消す
            OnDisable();
            manager = null;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Destroy(gameObject, clip.length + 0.1f);
        }

        AudioClip Play(SoundEntry sound)
        {
            if (sound == null) return null;
            AudioClip clip = sound.PickClip();
            if (clip == null) return null;

            AudioSource src = sources[next];
            next = (next + 1) % sources.Length;
            src.Stop();
            src.clip = clip;
            src.volume = sound.volume;
            src.pitch = 1f + Random.Range(-sound.pitchRandom, sound.pitchRandom);
            src.Play();
            return clip;
        }
    }
}
