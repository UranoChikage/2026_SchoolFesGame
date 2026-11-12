using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラクター名ごとのスコアアイコン（ScoreUi が使う）
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterIcons", menuName = "Chambara/CharacterIconList")]
    public class CharacterIconList : ScriptableObject
    {
        [Tooltip("キャラクター名（PlayerModels の名前と同じにする）とアイコン")]
        [SerializeField] private CharacterIconEntry[] entries;

        /// <summary>
        /// キャラクター名に対応するアイコンを返す（無ければ null）
        /// </summary>
        public Sprite GetIcon(string characterName)
        {
            if (entries == null || string.IsNullOrEmpty(characterName)) return null;
            foreach (var entry in entries)
            {
                if (entry != null && entry.CharacterName == characterName) return entry.Icon;
            }
            return null;
        }
    }

    /// <summary>
    /// キャラクター名とアイコンの対応
    /// </summary>
    [System.Serializable]
    public class CharacterIconEntry
    {
        [Tooltip("キャラクター名（PlayerModels の名前と同じにする）")]
        [SerializeField] private string characterName;
        [SerializeField] private Sprite icon;

        public string CharacterName => characterName;
        public Sprite Icon => icon;
    }
}
