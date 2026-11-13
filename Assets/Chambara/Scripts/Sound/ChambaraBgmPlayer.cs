using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// バトル中に ChambaraBgmConfig の曲をループで流す。スロー中（timeScale が 1 より小さい間）は音量を下げる
    /// 吹っ飛びを見せ終わってスコアが入ってから、次のラウンドが始まるまで（勝利演出も含む）も音量を下げる
    /// バトルシーンのどこかに1つ置き、config に ChambaraBgmConfig を入れる
    /// </summary>
    public class ChambaraBgmPlayer : MonoBehaviour
    {
        [SerializeField] ChambaraBgmConfig config;

        AudioSource source;
        float fadeIn;      // 開始時のフェードイン（0→1）
        float slowRate = 1f;   // スローによる音量の割合（今の値）
        float resultRate = 1f; // スコア表示・勝利演出による音量の割合（今の値）
        bool inResult;         // スコアが入ってから次のラウンドが始まるまで

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;   // 2D
            source.ignoreListenerPause = true;
        }

        private void Start()
        {
            if (config == null || config.Clip == null)
            {
                Debug.LogWarning("ChambaraBgmPlayer: config か曲が未設定です");
                return;
            }
            source.clip = config.Clip;
            fadeIn = config.FadeInTime > 0f ? 0f : 1f;
            ApplyVolume();
            source.Play();

            var m = ChambaraManager.Instance;
            if (m != null)
            {
                m.OnRoundResult += OnRoundResult;
                m.OnGameStateChanged += OnGameStateChanged;
            }
        }

        private void OnDestroy()
        {
            var m = ChambaraManager.Instance;
            if (m == null) return;
            m.OnRoundResult -= OnRoundResult;
            m.OnGameStateChanged -= OnGameStateChanged;
        }

        void OnRoundResult() => inResult = true;

        // 次のラウンドが始まったら戻す
        void OnGameStateChanged(GameState state)
        {
            if (state is GameState.Waiting or GameState.Ready) inResult = false;
        }

        private void Update()
        {
            if (config == null || !source.isPlaying) return;

            // スロー演出・ヒットストップ中は timeScale が下がるので、実時間で音量を追いかける
            float dt = Time.unscaledDeltaTime;
            if (config.FadeInTime > 0f) fadeIn = Mathf.MoveTowards(fadeIn, 1f, dt / config.FadeInTime);

            float target = Time.timeScale < 0.99f ? config.SlowVolumeRate : 1f;
            slowRate = config.SlowFadeTime > 0f
                ? Mathf.MoveTowards(slowRate, target, dt / config.SlowFadeTime)
                : target;

            float resultTarget = inResult ? config.ResultVolumeRate : 1f;
            resultRate = config.ResultFadeTime > 0f
                ? Mathf.MoveTowards(resultRate, resultTarget, dt / config.ResultFadeTime)
                : resultTarget;

            ApplyVolume();
        }

        // スローとスコア表示が重なったときは低い方に合わせる
        void ApplyVolume() => source.volume = config.Volume * fadeIn * Mathf.Min(slowRate, resultRate);
    }
}
