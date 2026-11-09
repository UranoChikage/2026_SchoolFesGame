using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// ビームの見た目（ビーム本体・着弾点の円）の既定Prefabを Assets/Urano/VFX に書き出し、
    /// ビームの ItemAsset の空いているスロットへ割り当てる。既にあるファイルは上書きしない。
    /// 書き出したPrefab／マテリアルは自由に編集／差し替えできる。
    /// </summary>
    public static class BeamAssetGenerator
    {
        const string Root = "Assets/Urano/VFX";

        [MenuItem("Urano/VFX/Generate Beam Prefabs (Line, Area)")]
        static void Generate()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");

            Material beamMat = CreateMaterial(Root + "/Materials/Beam.mat", new Color(0f, 1f, 1f, 0.5f));
            Material areaMat = CreateMaterial(Root + "/Materials/BeamArea.mat", new Color(0f, 1f, 1f, 0.25f));
            GameObject line = CreateLinePrefab(Root + "/Prefabs/BeamLine.prefab", beamMat);
            GameObject area = CreateAreaPrefab(Root + "/Prefabs/BeamArea.prefab", areaMat);

            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemAsset"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.Kind != ItemKind.Beam) continue;

                var so = new SerializedObject(item);
                bool changed = AssignIfEmpty(so.FindProperty("beamPrefab"), line);
                changed |= AssignIfEmpty(so.FindProperty("beamAreaPrefab"), area);
                if (!changed) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[VFX] ビームのPrefabを書き出しました: {Root}/Prefabs（ビームのアイテム {assigned} 件に割り当て）");
        }

        static bool AssignIfEmpty(SerializedProperty prop, Object value)
        {
            if (prop == null || prop.objectReferenceValue != null) return false;
            prop.objectReferenceValue = value;
            return true;
        }

        static Material CreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(Shader.Find("Sprites/Default")) { color = color, renderQueue = 3100 }; // 海(3000)より後に描く
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 2点のLineRenderer（ワールド座標）。位置と太さの倍率はコードが毎フレーム渡す
        static GameObject CreateLinePrefab(string path, Material material)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("BeamLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.widthMultiplier = 1f;
            lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            lr.sharedMaterial = material;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // 直径1m・薄い円盤（コードがビームの判定半径×2へ拡縮する前提）。コライダー無し
        static GameObject CreateAreaPrefab(string path, Material material)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "BeamArea";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(1f, 0.02f, 1f);

            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = material;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
