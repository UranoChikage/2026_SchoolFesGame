using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面（ChambaraSelect）の効果音をまとめて設定する
    /// 鳴らすのは ChambaraSelectSoundPlayer
    /// </summary>
    [CreateAssetMenu(fileName = "ChambaraSelectSound", menuName = "Chambara/SelectSoundConfig")]
    public class ChambaraSelectSoundConfig : ScriptableObject
    {
        [Header("カーソル移動")]
        [SerializeField] SoundEntry move = new SoundEntry();

        [Header("決定（キャラ・剣を選んだ）")]
        [SerializeField] SoundEntry pick = new SoundEntry();

        [Header("取り消し（戻る）")]
        [SerializeField] SoundEntry unpick = new SoundEntry();

        [Header("準備完了（キャラと剣の両方が決まった）")]
        [SerializeField] SoundEntry ready = new SoundEntry();

        [Header("試合開始（開始が確定した）")]
        [SerializeField] SoundEntry start = new SoundEntry();

        public SoundEntry Move => move;
        public SoundEntry Pick => pick;
        public SoundEntry Unpick => unpick;
        public SoundEntry Ready => ready;
        public SoundEntry GameStart => start;
    }
}
