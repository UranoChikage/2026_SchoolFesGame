using UnityEngine;

namespace Pirates
{
    using UnityEngine;
    using UnityEditor;
    using System.IO;

    public static class ParticleTextureGenerator
    {
        const int Size = 128;

        [MenuItem("Tools/Particle Texture/Soft Circle")]
        static void SoftCircle()
        {
            Generate("Assets/SoftCircle.png", d =>
            {
                float a = Mathf.Clamp01(1f - d);
                return a * a; // 外側ほど柔らかく
            });
        }

        [MenuItem("Tools/Particle Texture/Ring")]
        static void Ring()
        {
            Generate("Assets/Ring.png", d =>
            {
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) / 0.15f);
                return a * a;
            });
        }

        static void Generate(string path, System.Func<float, float> alphaFunc)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            float r = Size / 2f;

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alphaFunc(d))); // 色は白、透明度だけ変える
                }
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.Refresh();

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
