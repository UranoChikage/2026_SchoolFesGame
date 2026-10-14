using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// 船の航跡・水しぶきのプレハブを Assets/Urano/VFX に書き出す。既にあるファイルは上書きしない。
    /// 書き出した後は、粒の大きさ・寿命・色などを自由に調整できる。
    /// </summary>
    public static class BoatWakeGenerator
    {
        const string Root = "Assets/Urano/VFX";

        [MenuItem("Urano/VFX/Generate Boat Wake Prefab")]
        static void Generate()
        {
            Ensure(Root);
            Ensure(Root + "/Materials");
            Ensure(Root + "/Prefabs");
            Ensure(Root + "/Textures");

            Texture2D soft = CreateSoftCircle(Root + "/Textures/SoftCircle.png");
            Material mat = CreateMaterial(Root + "/Materials/WakeFoam.mat", soft);

            string prefabPath = Root + "/Prefabs/BoatWake.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Debug.Log("[VFX] BoatWake.prefab は既にあります（上書きしません）");
                return;
            }

            var root = new GameObject("BoatWake");
            ParticleSystem wake = CreateWake(root.transform, mat);
            ParticleSystem spray = CreateSpray(root.transform, mat);
            var comp = root.AddComponent<BoatWake>();
            var so = new SerializedObject(comp);
            so.FindProperty("wake").objectReferenceValue = wake;
            so.FindProperty("bowSpray").objectReferenceValue = spray;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(prefabPath);
            Debug.Log("[VFX] BoatWake.prefab を書き出しました。船の子にして、Wake=船尾 / BowSpray=船首 の位置に動かしてください");
        }

        // 船尾：水面に寝かせた泡の板を出し続け、その場に残して広がりながら消す
        static ParticleSystem CreateWake(Transform parent, Material mat)
        {
            ParticleSystem ps = NewSystem("Wake", parent, mat, new Vector3(0f, 0.15f, -3f));
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 出した場所に残る＝船の後ろに帯ができる
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.6f);
            main.maxParticles = 300;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 0f, 0.5f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f)); // 広がる

            SetFade(ps);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // 水面に寝かせる
            return ps;
        }

        // 船首：上へはねて落ちる小さなしぶき
        static ParticleSystem CreateSpray(Transform parent, Material mat)
        {
            ParticleSystem ps = NewSystem("BowSpray", parent, mat, new Vector3(0f, 0.3f, 3f));
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 200;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 上向き

            SetFade(ps);
            return ps;
        }

        static ParticleSystem NewSystem(string name, Transform parent, Material mat, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Local; // 船の拡縮に粒の大きさを引きずられない

            var emission = ps.emission;
            emission.rateOverTime = 0f; // BoatWake が速度に応じて設定する

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // 寿命の後半で透明になって消える
        static void SetFade(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static Material CreateMaterial(string path, Texture2D tex)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // 水面(Transparent=3000)より後ろに描く。Sprites/Default は頂点色で着色／フェードでき、どのパイプラインでも描ける
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, renderQueue = 3100 };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 中心が白く縁が透明なやわらかい円
        static Texture2D CreateSoftCircle(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Ensure(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
