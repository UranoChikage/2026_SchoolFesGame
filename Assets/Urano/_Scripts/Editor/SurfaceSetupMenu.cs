using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// 砲弾の着弾演出を「素材（SurfaceAsset）」方式へ移す初期設定。
    /// 既存の爆発の演出(CannonBallExplosion)から、既定の素材と海の素材を作り、
    /// 開いているシーンの海（SeaFollower）に HitSurface を付けて割り当てる。既にあるものは上書きしない。
    /// </summary>
    public static class SurfaceSetupMenu
    {
        const string Folder = "Assets/Urano/Surfaces";
        const string ExplosionVfxPath = "Assets/Urano/VFXAssets/CannonBallExplosion.asset";

        [MenuItem("Urano/VFX/Setup Surfaces (Default, Sea)")]
        static void Setup()
        {
            var explosion = AssetDatabase.LoadAssetAtPath<VfxAsset>(ExplosionVfxPath);
            if (explosion == null) Debug.LogWarning($"[Surface] {ExplosionVfxPath} が見つかりません。SurfaceAsset の Cannon Hit Vfx は空のままです");

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Urano", "Surfaces");
            SurfaceAsset defaultSurface = CreateSurface(Folder + "/Surface_Default.asset", explosion);
            SurfaceAsset seaSurface = CreateSurface(Folder + "/Surface_Sea.asset", explosion);

            var follower = Object.FindFirstObjectByType<SeaFollower>();
            if (follower == null)
            {
                Debug.LogWarning("[Surface] 開いているシーンに SeaFollower（海）が見つかりません。シーンを開いてからもう一度実行してください");
                return;
            }

            HitSurface hs = follower.GetComponent<HitSurface>();
            if (hs == null) hs = Undo.AddComponent<HitSurface>(follower.gameObject);

            var so = new SerializedObject(hs);
            Assign(so.FindProperty("surface"), seaSurface);
            Assign(so.FindProperty("defaultSurface"), defaultSurface);
            so.FindProperty("isSea").boolValue = true;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(follower.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = follower.gameObject;
            Debug.Log("[Surface] 既定の素材と海の素材を作り、海に HitSurface を付けました。シーンを保存してください", follower);
        }

        static SurfaceAsset CreateSurface(string path, VfxAsset cannonVfx)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SurfaceAsset>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<SurfaceAsset>();
            var so = new SerializedObject(asset);
            so.FindProperty("cannonHitVfx").objectReferenceValue = cannonVfx;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void Assign(SerializedProperty prop, Object value)
        {
            if (prop.objectReferenceValue == null) prop.objectReferenceValue = value;
        }
    }
}
