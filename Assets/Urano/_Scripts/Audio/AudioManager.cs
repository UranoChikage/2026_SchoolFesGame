using UnityEngine;

namespace Pirates
{
    /// <summary>止められる音（ループ・長い音用）の持ち手。default なら何も起きない</summary>
    public struct SoundHandle
    {
        internal AudioManager.Voice voice;
        internal int token;

        /// <summary>この音を止める。すでに終わっている・別の音に使い回されていれば何もしない</summary>
        public void Stop()
        {
            if (voice != null && voice.token == token) voice.source.Stop();
        }
    }

    /// <summary>
    /// 効果音の再生係。最初に鳴らした時に自動で生成され（シーンに置かなくてよい）、シーンを跨いで残る。
    /// 音の登録は Resources/AudioLibrary.asset（AudioLibrary）で行う。未登録の音は黙って無視する。
    /// 使い方: AudioManager.Play(Sfx.Barrier) / AudioManager.Play(Sfx.CannonFire, 位置) / var h = Play(Sfx.Beam); h.Stop();
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int VoiceCount = 24;
        const string LibraryResource = "AudioLibrary";

        internal class Voice
        {
            public AudioSource source;
            public int token;
            public float startTime;
        }

        static AudioManager instance;

        AudioLibrary library;
        Voice[] voices;
        float[] lastPlayTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => instance = null;

        static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("AudioManager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        void Awake()
        {
            library = Resources.Load<AudioLibrary>(LibraryResource);
            if (library == null)
                Debug.LogWarning("[Audio] Resources/AudioLibrary.asset がありません。メニュー Urano > Audio > Create Audio Library で作成してください。音は鳴りません", this);

            // 位置付きで鳴らすので、音源ごとに子オブジェクトを持つ
            voices = new Voice[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                voices[i] = new Voice { source = src };
            }
            lastPlayTime = new float[System.Enum.GetValues(typeof(Sfx)).Length];
            for (int i = 0; i < lastPlayTime.Length; i++) lastPlayTime[i] = float.NegativeInfinity;
        }

        /// <summary>画面全体に鳴らす（位置なし）</summary>
        public static SoundHandle Play(Sfx id) => Instance.PlayInternal(id, null);

        /// <summary>位置付きで鳴らす。Sound の Spatial Blend が0なら位置は無視される</summary>
        public static SoundHandle Play(Sfx id, Vector3 position) => Instance.PlayInternal(id, position);

        /// <summary>その種類の音をすべて止める（ループ音の止め忘れ対策など）</summary>
        public static void StopAll(Sfx id)
        {
            if (instance == null) return;
            instance.StopAllInternal(id);
        }

        SoundHandle PlayInternal(Sfx id, Vector3? position)
        {
            if (library == null) return default;
            Sound sound = library.Get(id);
            if (sound == null) return default;
            AudioClip clip = sound.PickClip();
            if (clip == null) return default;

            // 単発は最小間隔を空けて団子を防ぐ（ループ音は開始が1回なので制限しない）
            if (!sound.loop && Time.unscaledTime - lastPlayTime[(int)id] < sound.minInterval) return default;
            lastPlayTime[(int)id] = Time.unscaledTime;

            Voice v = AcquireVoice();
            if (v == null) return default;

            AudioSource src = v.source;
            src.Stop();
            src.clip = clip;
            src.volume = sound.volume;
            src.pitch = Random.Range(sound.pitchRange.x, sound.pitchRange.y);
            src.loop = sound.loop;
            src.spatialBlend = position.HasValue ? sound.spatialBlend : 0f;
            src.transform.position = position ?? Vector3.zero;
            v.token++;
            v.startTime = Time.unscaledTime;
            src.Play();
            return new SoundHandle { voice = v, token = v.token };
        }

        // 空きを探す。無ければ一番古い単発音を奪う（ループ音は奪わない）
        Voice AcquireVoice()
        {
            Voice oldest = null;
            foreach (Voice v in voices)
            {
                if (!v.source.isPlaying) return v;
                if (!v.source.loop && (oldest == null || v.startTime < oldest.startTime)) oldest = v;
            }
            return oldest;
        }

        void StopAllInternal(Sfx id)
        {
            Sound sound = library != null ? library.Get(id) : null;
            if (sound == null || sound.clips == null) return;
            foreach (Voice v in voices)
            {
                if (!v.source.isPlaying) continue;
                foreach (AudioClip c in sound.clips)
                {
                    if (c != null && v.source.clip == c) { v.source.Stop(); break; }
                }
            }
        }
    }
}
