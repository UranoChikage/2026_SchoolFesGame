using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// バトルのBGMの設定。鳴らすのは ChambaraBgmPlayer
    /// </summary>
    [CreateAssetMenu(fileName = "ChambaraBgm", menuName = "Chambara/BgmConfig")]
    public class ChambaraBgmConfig : ScriptableObject
    {
        [SerializeField] AudioClip clip;
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;

        [Header("スロー中")]
        [Tooltip("スロー中の音量（volume に掛ける割合）。0 で無音")]
        [SerializeField, Range(0f, 1f)] float slowVolumeRate = 0.3f;
        [Tooltip("音量を下げる・戻すのにかける時間（実時間・秒）")]
        [SerializeField] float slowFadeTime = 0.3f;

        [Header("スコアが入ってから・勝利演出中（次のラウンドが始まると戻る）")]
        [Tooltip("音量（volume に掛ける割合）。0 で無音")]
        [SerializeField, Range(0f, 1f)] float resultVolumeRate = 0.3f;
        [Tooltip("音量を下げる・戻すのにかける時間（実時間・秒）")]
        [SerializeField] float resultFadeTime = 0.5f;

        [Header("開始")]
        [Tooltip("シーン開始時にこの時間をかけて音量を上げる（実時間・秒）。0 ならすぐ")]
        [SerializeField] float fadeInTime = 1f;

        public AudioClip Clip => clip;
        public float Volume => volume;
        public float SlowVolumeRate => slowVolumeRate;
        public float SlowFadeTime => slowFadeTime;
        public float FadeInTime => fadeInTime;
        public float ResultVolumeRate => resultVolumeRate;
        public float ResultFadeTime => resultFadeTime;
    }
}
