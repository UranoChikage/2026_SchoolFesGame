using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// Skybox とステージ床マテリアルのセット一覧。適用するのは StageThemeApplier
    /// </summary>
    [CreateAssetMenu(fileName = "StageTheme", menuName = "Chambara/StageThemeConfig")]
    public class StageThemeConfig : ScriptableObject
    {
        [System.Serializable]
        public class Theme
        {
            [Tooltip("インスペクター上の識別用（処理には使わない）")]
            public string name;
            public Material skybox;
            public Material stageMaterial;
        }

        [SerializeField] Theme[] themes;

        public int Count => themes != null ? themes.Length : 0;
        public Theme Get(int index) => themes[index];
    }
}
