using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// ChambaraManager のイベントに合わせて ChambaraSoundConfig の効果音を鳴らす
    /// バトルシーンのどこかに1つ置き、config に ChambaraSoundConfig を入れる
    /// カメラの切り替え（吹っ飛びの繰り返し演出など）で音が聞こえなくならないよう、
    /// AudioListener はこのオブジェクトに1つだけ持ち、カメラ側の AudioListener は止める（効果音は2Dなので位置は関係ない）
    /// </summary>
    public class ChambaraSoundPlayer : MonoBehaviour
    {
        [SerializeField] ChambaraSoundConfig config;
        [Tooltip("ON: スロー中は音もゆっくり（低く）鳴らす")]
        [SerializeField] bool followTimeScale = false;
        [SerializeField] int voiceCount = 8;   // 同時に鳴らせる数

        AudioSource[] sources;
        int next;

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
            if (!TryGetComponent<AudioListener>(out _)) gameObject.AddComponent<AudioListener>();
        }

        // 自分以外の AudioListener を止める。2P用カメラはラウンド開始時に複製されるので、状態が変わるたびに呼ぶ
        void DisableOtherListeners()
        {
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.gameObject != gameObject) l.enabled = false;
            }
        }

        void OnGameStateChanged(GameState state) => DisableOtherListeners();

        private void Start()
        {
            BodyRig.OnKnockbackPlayed += OnKnockback;
            PlayerBehaviour.OnStepped += OnStepped;
            DisableOtherListeners();
            var m = ChambaraManager.Instance;
            if (m == null) return;
            m.OnGameStateChanged += OnGameStateChanged;
            m.OnWhiff += OnWhiff;
            m.OnTieClash += OnTieClash;
        }

        private void OnDestroy()
        {
            BodyRig.OnKnockbackPlayed -= OnKnockback;
            PlayerBehaviour.OnStepped -= OnStepped;   // static なので外し忘れるとシーンをまたいで残る
            var m = ChambaraManager.Instance;
            if (m == null) return;
            m.OnGameStateChanged -= OnGameStateChanged;
            m.OnWhiff -= OnWhiff;
            m.OnTieClash -= OnTieClash;
        }

        void OnWhiff(int attacker) => Play(config != null ? config.Whiff : null);

        void OnTieClash() => Play(config != null ? config.Clash : null);

        void OnStepped(PlayerBehaviour player, bool forward) => Play(config != null ? config.Step : null);

        // 吹っ飛ぶたび（リプレイのやり直しも含む）に、吹っ飛ばした側の武器の音を鳴らす
        void OnKnockback(PlayerBehaviour knocked)
        {
            if (config == null || ChambaraManager.Instance == null) return;
            var players = ChambaraManager.Instance.Players;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p != null && p != knocked)
                {
                    Play(config.GetHit(p.SwordName));
                    return;
                }
            }
        }

        void Play(SoundEntry sound)
        {
            if (sound == null) return;
            AudioClip clip = sound.PickClip();
            if (clip == null) return;

            AudioSource src = sources[next];
            next = (next + 1) % sources.Length;
            src.Stop();
            src.clip = clip;
            src.volume = sound.volume;
            float pitch = 1f + Random.Range(-sound.pitchRandom, sound.pitchRandom);
            if (followTimeScale) pitch *= Mathf.Max(Time.timeScale, 0.01f);
            src.pitch = pitch;
            src.Play();
        }
    }
}
