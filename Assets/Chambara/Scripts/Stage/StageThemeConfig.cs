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
            [Tooltip("ON: 開始時に Skybox をキューブマップへ1回だけ焼き付けて表示する（描画コストがほぼ0になる代わりに、空の動きが止まる）")]
            public bool bakeSkybox;
            [Tooltip("このテーマのときに Awake で生成する Prefab。位置・回転は Prefab に保存された値（ワールド座標）をそのまま使う")]
            public GameObject[] spawnPrefabs;
        }

        [SerializeField] Theme[] themes;

        [Header("Skybox Bake")]
        [Tooltip("焼き付けたキューブマップを表示するシェーダー (Custom/Skybox/SkyBakedCubemap)")]
        [SerializeField] Shader bakedSkyboxShader;
        [Tooltip("キューブマップ1面の解像度。大きいほどくっきりするがメモリと焼き付け時間が増える (1536 で約 56MB)")]
        [SerializeField] int bakeResolution = 1536;

        public int Count => themes != null ? themes.Length : 0;
        public Theme Get(int index) => themes[index];
        public Shader BakedSkyboxShader => bakedSkyboxShader;
        public int BakeResolution => bakeResolution;
    }
}
