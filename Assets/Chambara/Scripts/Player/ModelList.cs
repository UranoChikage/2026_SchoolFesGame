using UnityEngine;

namespace Chambara
{
    [CreateAssetMenu(fileName = "ModelList", menuName = "Chambara/ModelList")]
    public class ModelList :ScriptableObject
    {
        [SerializeField] PlayerModelData[] playerModels;

        public GameObject GetModel(string name)
        {
            foreach (var model in playerModels)
            {
                if (model.name == name)
                {
                    return model.model;
                }
            }
            //Debug.LogError($"そんなモデル名はねぇ");
            return null;
        }

        /// <summary>画面に出す名前（日本語名）。DisplayName が空なら Name をそのまま返す</summary>
        public string GetDisplayName(string name)
        {
            foreach (var model in playerModels)
            {
                if (model.name == name)
                {
                    return string.IsNullOrEmpty(model.displayName) ? model.name : model.displayName;
                }
            }
            return name;
        }

    }

    [System.Serializable]
    public class PlayerModelData
    {
        [SerializeField] private string Name = "Johnson";
        [SerializeField, Tooltip("画面に出す名前（日本語名）。空なら Name を出す")] private string DisplayName;
        [SerializeField] private GameObject Model;
        public string name => Name;
        public string displayName => DisplayName;
        public GameObject model => Model;
    }
}
