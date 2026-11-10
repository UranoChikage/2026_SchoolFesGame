using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>AudioLibrary を Resources に作るメニュー</summary>
    public static class AudioLibraryMenu
    {
        const string Folder = "Assets/Urano/Resources";
        const string AssetPath = Folder + "/AudioLibrary.asset";

        [MenuItem("Urano/Audio/Create Audio Library")]
        static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            var asset = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
