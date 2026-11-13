using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// Awake 時に StageThemeConfig からランダムに1セット選び、Skybox とステージ床のマテリアルを適用する
    /// </summary>
    public class StageThemeApplier : MonoBehaviour
    {
        [SerializeField] StageThemeConfig config;
        [Tooltip("床マテリアルを差し替える Renderer")]
        [SerializeField] Renderer[] stageRenderers;

        [SerializeField] bool isDebugMode = false;
        [SerializeField] int debugThemeIndex = 0;

        void Awake()
        {
            if (config == null || config.Count == 0) return;

            var theme = config.Get(isDebugMode ?debugThemeIndex:Random.Range(0, config.Count));

            if (theme.skybox != null)
            {
                RenderSettings.skybox = theme.skybox;
                // 環境光・反射を Skybox から取っている場合に反映させる
                DynamicGI.UpdateEnvironment();
            }

            if (theme.stageMaterial != null && stageRenderers != null)
            {
                foreach (var r in stageRenderers)
                {
                    if (r != null) r.sharedMaterial = theme.stageMaterial;
                }
            }
        }
    }
}
