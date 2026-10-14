using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// 着弾予測マーカー・予測線・カーソルの既定アセットを Assets/Urano/VFX に書き出す。
    /// 書き出したものは自由に編集／差し替えでき、既に存在するファイルは上書きしない。
    /// </summary>
    public static class DefaultVfxAssetGenerator
    {
        const string Root = "Assets/Urano/VFX";

        [MenuItem("Urano/VFX/Generate Default Assets (Marker, Trajectory, Cursor)")]
        static void Generate()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Textures");

            Material markerMat = CreateMaterial(Root + "/Materials/AimMarker.mat", new Color(1f, 0.1f, 0.1f, 0.45f));
            CreateMaterial(Root + "/Materials/Trajectory.mat", Color.white);
            CreateMarkerPrefab(Root + "/Prefabs/AimMarker.prefab", markerMat);
            CreateCrosshair(Root + "/Textures/Crosshair.png");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(Root);
            Debug.Log("[VFX] 既定アセットを書き出しました: " + Root);
        }

        static Material CreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // Sprites/Default は頂点カラー／色で着色でき、どのレンダーパイプラインでも描ける
            var mat = new Material(Shader.Find("Sprites/Default")) { color = color, renderQueue = 3100 }; // 水面(2900)より後に描く
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 直径1m・薄い円盤（コードが爆発半径×2へ拡縮する前提）。コライダー無し
        static void CreateMarkerPrefab(string path, Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "AimMarker";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(1f, 0.05f, 1f);

            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = material;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        // 円＋十字の白いカーソル画像（実際の色は CannonStation が砲手ごとに着色する）
        static void CreateCrosshair(string path)
        {
            if (File.Exists(path)) return;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring = Mathf.Abs(d - 22f) < 2f;
                    bool cross = (Mathf.Abs(dx) < 2f || Mathf.Abs(dy) < 2f) && d < 30f && d > 8f;
                    tex.SetPixel(x, y, ring || cross ? Color.white : Color.clear);
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
