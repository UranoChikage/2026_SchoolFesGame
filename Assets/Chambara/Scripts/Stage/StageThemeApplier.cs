using UnityEngine;
using UnityEngine.Rendering;

namespace Chambara
{
    /// <summary>
    /// Awake 時に StageThemeConfig からランダムに1セット選び、Skybox とステージ床のマテリアルを適用する。
    /// テーマの bakeSkybox が ON なら、Start で Skybox をキューブマップに焼き付けて軽量化する
    /// </summary>
    public class StageThemeApplier : MonoBehaviour
    {
        [SerializeField] StageThemeConfig config;
        [Tooltip("床マテリアルを差し替える Renderer")]
        [SerializeField] Renderer[] stageRenderers;

        [SerializeField] bool isDebugMode = false;
        [SerializeField] int debugThemeIndex = 0;

        StageThemeConfig.Theme currentTheme;
        RenderTexture bakedCubemap;
        Material bakedSkyboxMaterial;

        void Awake()
        {
            if (config == null || config.Count == 0) return;

            currentTheme = config.Get(isDebugMode ? debugThemeIndex : Random.Range(0, config.Count));

            if (currentTheme.skybox != null)
            {
                RenderSettings.skybox = currentTheme.skybox;
                // 環境光・反射を Skybox から取っている場合に反映させる
                DynamicGI.UpdateEnvironment();
            }

            if (currentTheme.stageMaterial != null && stageRenderers != null)
            {
                foreach (var r in stageRenderers)
                {
                    if (r != null) r.sharedMaterial = currentTheme.stageMaterial;
                }
            }

            SpawnThemeObjects();
        }

        /// <summary>
        /// テーマ専用の Prefab を、Prefab に保存された位置・回転のまま生成する
        /// </summary>
        void SpawnThemeObjects()
        {
            if (currentTheme.spawnPrefabs == null) return;

            foreach (var prefab in currentTheme.spawnPrefabs)
            {
                if (prefab == null) continue;
                var t = prefab.transform;
                Instantiate(prefab, t.position, t.rotation);
            }
        }

        void Start()
        {
            // 描画パイプラインの準備が済んでから焼き付ける
            if (currentTheme != null && currentTheme.bakeSkybox && currentTheme.skybox != null)
            {
                BakeSkybox();
            }
        }

        /// <summary>
        /// 現在の Skybox (重い手続き型シェーダー) を一度だけキューブマップに描き、以後はそれを表示する
        /// </summary>
        void BakeSkybox()
        {
            var shader = config.BakedSkyboxShader;
            if (shader == null)
            {
                Debug.LogWarning("[StageThemeApplier] Baked Skybox Shader が未設定なので焼き付けをスキップします", this);
                return;
            }

            int size = Mathf.Clamp(config.BakeResolution, 64, 4096);
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
                ? RenderTextureFormat.RGB111110Float
                : RenderTextureFormat.ARGBHalf;

            var desc = new RenderTextureDescriptor(size, size, format, 0)
            {
                dimension = TextureDimension.Cube,
                useMipMap = false,
                autoGenerateMips = false,
                msaaSamples = 1,
            };
            bakedCubemap = new RenderTexture(desc)
            {
                name = "BakedSkybox",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            bakedCubemap.Create();

            // 何も映さず Skybox だけを描く一時カメラ
            var go = new GameObject("SkyboxBakeCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.cullingMask = 0;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            bool ok = cam.RenderToCubemap(bakedCubemap, 63);
            Destroy(go);

            if (!ok)
            {
                Debug.LogWarning("[StageThemeApplier] Skybox の焼き付けに失敗したので、元の Skybox のまま表示します", this);
                ReleaseBaked();
                return;
            }

            bakedSkyboxMaterial = new Material(shader) { name = currentTheme.skybox.name + " (Baked)" };
            bakedSkyboxMaterial.SetTexture("_Cubemap", bakedCubemap);
            RenderSettings.skybox = bakedSkyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        void ReleaseBaked()
        {
            if (bakedCubemap != null)
            {
                bakedCubemap.Release();
                Destroy(bakedCubemap);
                bakedCubemap = null;
            }
            if (bakedSkyboxMaterial != null)
            {
                Destroy(bakedSkyboxMaterial);
                bakedSkyboxMaterial = null;
            }
        }

        void OnDestroy()
        {
            ReleaseBaked();
        }
    }
}
