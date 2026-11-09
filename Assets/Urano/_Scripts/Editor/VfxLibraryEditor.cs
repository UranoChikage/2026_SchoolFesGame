using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>VfxLibraryのInspector。プロジェクト内の VfxAsset を集めるボタンと、PrefabからVfxAssetを作るメニューを持つ</summary>
    [CustomEditor(typeof(VfxLibrary))]
    public class VfxLibraryEditor : Editor
    {
        public const string VfxFolder = "Assets/Urano/VFX";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("プロジェクト内の VfxAsset をすべて集める"))
            {
                Collect((VfxLibrary)target);
            }
        }

        static void Collect(VfxLibrary library)
        {
            var found = new List<VfxAsset>();
            foreach (string guid in AssetDatabase.FindAssets("t:VfxAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<VfxAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) found.Add(asset);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var so = new SerializedObject(library);
            SerializedProperty list = so.FindProperty("entries");
            list.ClearArray();
            for (int i = 0; i < found.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
        }

        // 選択したPrefabから VfxAsset を一括作成（Assets/Urano/VFX に置く）
        [MenuItem("Assets/Urano/Create VFX Asset from Prefab")]
        static void CreateFromSelection()
        {
            EnsureFolder();
            VfxAsset last = null;

            foreach (Object obj in Selection.objects)
            {
                if (!(obj is GameObject prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab)) continue;

                string path = AssetDatabase.GenerateUniqueAssetPath($"{VfxFolder}/{prefab.name}.asset");
                var asset = CreateInstance<VfxAsset>();
                var so = new SerializedObject(asset);
                so.FindProperty("prefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, path);
                last = asset;
            }

            if (last == null) return;
            AssetDatabase.SaveAssets();
            Collect(GetOrCreateLibrary());
            Selection.activeObject = last;
            EditorGUIUtility.PingObject(last);
        }

        [MenuItem("Assets/Urano/Create VFX Asset from Prefab", true)]
        static bool CreateFromSelectionValidate()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)) return true;
            }
            return false;
        }

        static VfxLibrary GetOrCreateLibrary()
        {
            string[] guids = AssetDatabase.FindAssets("t:VfxLibrary");
            if (guids.Length > 0) return AssetDatabase.LoadAssetAtPath<VfxLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));

            var library = CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(library, $"{VfxFolder}/VfxLibrary.asset");
            return library;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Urano")) AssetDatabase.CreateFolder("Assets", "Urano");
            if (!AssetDatabase.IsValidFolder(VfxFolder)) AssetDatabase.CreateFolder("Assets/Urano", "VFX");
        }
    }
}
