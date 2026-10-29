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
            Debug.LogError($"‚»‚ñ‚Èƒ‚ƒfƒ‹–¼‚Í‚Ë‚¥");
            return null;
        }

    }

    [System.Serializable]
    public class PlayerModelData
    {
        [SerializeField] private string Name = "Johnson";
        [SerializeField] private GameObject Model;
        public string name => Name;
        public GameObject model => Model;
    }
}
