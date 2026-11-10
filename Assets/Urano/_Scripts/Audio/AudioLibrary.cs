using System;
using UnityEngine;

namespace Pirates
{
    /// <summary>効果音の種類。コードからは AudioManager.Play(Sfx.xxx) で鳴らす</summary>
    public enum Sfx
    {
        CannonFire,
        CannonExplosion,
        Sunk,
        TreasurePickup,
        Beam,
        Barrier,
        GameOver,
        TimeUp,
        ResultCountUp,
        ResultFinal,
        RankS,
        RankOther,
    }

    /// <summary>1つの効果音の設定。クリップが空なら鳴らない（エラーにもならない）</summary>
    [Serializable]
    public class Sound
    {
        [Tooltip("複数入れると鳴らすたびにランダムで選ぶ")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("ピッチのランダム幅。同じ音が重なっても単調になりにくい。1,1で固定")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        [Tooltip("ONならStopされるまでループ（ビーム・集計カウントアップ向け）。OFFならクリップ1回分")]
        public bool loop;
        [Range(0f, 1f)]
        [Tooltip("0=画面全体に同じ音量(2D) / 1=位置で音量・左右が変わる(3D)。位置付きで鳴らす音（砲撃・爆発・沈没）向け")]
        public float spatialBlend;
        [Tooltip("同じ音を鳴らす最小間隔(秒)。敵が大量でも音が団子にならないようにする")]
        public float minInterval = 0.03f;

        public bool HasClip
        {
            get
            {
                if (clips == null) return false;
                foreach (AudioClip c in clips) if (c != null) return true;
                return false;
            }
        }

        public AudioClip PickClip()
        {
            if (!HasClip) return null;
            AudioClip c;
            do { c = clips[UnityEngine.Random.Range(0, clips.Length)]; } while (c == null);
            return c;
        }
    }

    /// <summary>
    /// 効果音の一覧。Resources/AudioLibrary.asset に置くと AudioManager が自動で読む。
    /// 作成はメニュー Urano > Audio > Create Audio Library。
    /// </summary>
    [CreateAssetMenu(menuName = "Urano/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Header("戦闘")]
        public Sound cannonFire = new Sound { spatialBlend = 0.5f };
        public Sound cannonExplosion = new Sound { spatialBlend = 0.5f };
        public Sound sunk = new Sound { spatialBlend = 0.5f };

        [Header("アイテム・お宝")]
        public Sound treasurePickup = new Sound();
        [Tooltip("ビームを撃っている間。長いクリップならLoopをOFFのままでOK（1回再生）")]
        public Sound beam = new Sound();
        public Sound barrier = new Sound();

        [Header("ゲーム進行")]
        public Sound gameOver = new Sound();
        public Sound timeUp = new Sound();

        [Header("リザルト")]
        [Tooltip("懸賞金・BONUSのカウントアップ中。短いチック音ならLoopをOFF、長い音ならLoopをON")]
        public Sound resultCountUp = new Sound();
        [Tooltip("RANKが出た瞬間（ランク別の音と同時に鳴る）")]
        public Sound resultFinal = new Sound();
        public Sound rankS = new Sound();
        [Tooltip("S以外（A・B）のとき")]
        public Sound rankOther = new Sound();

        public Sound Get(Sfx id)
        {
            switch (id)
            {
                case Sfx.CannonFire: return cannonFire;
                case Sfx.CannonExplosion: return cannonExplosion;
                case Sfx.Sunk: return sunk;
                case Sfx.TreasurePickup: return treasurePickup;
                case Sfx.Beam: return beam;
                case Sfx.Barrier: return barrier;
                case Sfx.GameOver: return gameOver;
                case Sfx.TimeUp: return timeUp;
                case Sfx.ResultCountUp: return resultCountUp;
                case Sfx.ResultFinal: return resultFinal;
                case Sfx.RankS: return rankS;
                case Sfx.RankOther: return rankOther;
                default: return null;
            }
        }
    }
}
