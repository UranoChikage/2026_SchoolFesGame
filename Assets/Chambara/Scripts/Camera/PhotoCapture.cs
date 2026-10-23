namespace Chambara
{
    using System.IO;
    using UnityEngine;
    using UnityEngine.Rendering;

    public class PhotoCapture : MonoBehaviour
    {
        [SerializeField] Camera photoCamera;
        [SerializeField] int width = 1920;
        [SerializeField] int height = 1080;
        [SerializeField] bool transparent = true;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var tex = Capture();
                Save(tex);
            }
        }

        public Texture2D Capture()
        {
            // Linear色空間で色が褪せないよう sRGB を明示
            var rt = new RenderTexture(width, height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 8;

            var prevTarget = photoCamera.targetTexture;
            var prevClear = photoCamera.clearFlags;
            var prevBg = photoCamera.backgroundColor;

            if (transparent)
            {
                photoCamera.clearFlags = CameraClearFlags.SolidColor;
                photoCamera.backgroundColor = new Color(0, 0, 0, 0);
            }

            photoCamera.targetTexture = rt;
            Render(photoCamera, rt);

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            photoCamera.targetTexture = prevTarget;
            photoCamera.clearFlags = prevClear;
            photoCamera.backgroundColor = prevBg;
            rt.Release();
            Destroy(rt);

            return tex;
        }

        static void Render(Camera cam, RenderTexture rt)
        {
            // URP/HDRPでは Camera.Render() が警告を出すので SubmitRenderRequest を使う
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req))
                RenderPipeline.SubmitRenderRequest(cam, req);
            else
                cam.Render();
        }

        public string Save(Texture2D tex)
        {
            var path = Path.Combine(Application.persistentDataPath,
                $"photo_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"saved: {path}");
            return path;
        }
    }
}
