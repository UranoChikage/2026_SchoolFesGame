using System;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// チャンバラの効果音をまとめて設定する
    /// 空振り（共通）・相殺（共通）・攻撃を当てた音（武器ごと）
    /// 鳴らすのは ChambaraSoundPlayer
    /// </summary>
    [CreateAssetMenu(fileName = "ChambaraSound", menuName = "Chambara/SoundConfig")]
    public class ChambaraSoundConfig : ScriptableObject
    {
        [Header("空振り（共通）")]
        [SerializeField] SoundEntry whiff = new SoundEntry();

        [Header("相殺（共通）")]
        [SerializeField] SoundEntry clash = new SoundEntry();

        [Header("移動（共通）。1歩踏み出すたびに鳴る")]
        [SerializeField] SoundEntry step = new SoundEntry();

        [Header("攻撃を当てた音（武器ごと）")]
        [Tooltip("武器名は SwordModels の Name と同じにする（Sinai, Sword, Bat, Pikopiko, LightSword など）")]
        [SerializeField] WeaponSound[] weaponHits;
        [Tooltip("weaponHits に無い武器のときに鳴らす音（任意）")]
        [SerializeField] SoundEntry defaultHit = new SoundEntry();

        public SoundEntry Whiff => whiff;
        public SoundEntry Clash => clash;
        public SoundEntry Step => step;

        /// <summary>武器名に対応するヒット音。登録が無ければ defaultHit</summary>
        public SoundEntry GetHit(string weaponName)
        {
            if (weaponHits != null)
            {
                foreach (var w in weaponHits)
                {
                    if (w != null && w.weaponName == weaponName && w.sound.HasClip) return w.sound;
                }
            }
            return defaultHit;
        }
    }

    [Serializable]
    public class WeaponSound
    {
        public string weaponName = "Sword";
        public SoundEntry sound = new SoundEntry();
    }

    /// <summary>
    /// 1種類の効果音。clips を複数入れるとランダムで鳴らす
    /// </summary>
    [Serializable]
    public class SoundEntry
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("鳴らすたびにピッチを ±この値 だけランダムにずらす")]
        [Range(0f, 0.5f)] public float pitchRandom = 0.05f;

        public bool HasClip
        {
            get
            {
                if (clips == null) return false;
                foreach (var c in clips) if (c != null) return true;
                return false;
            }
        }

        public AudioClip PickClip()
        {
            if (!HasClip) return null;
            AudioClip c;
            do c = clips[UnityEngine.Random.Range(0, clips.Length)]; while (c == null);
            return c;
        }
    }
}
