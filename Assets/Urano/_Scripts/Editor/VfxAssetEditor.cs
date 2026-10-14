using UnityEditor;
using UnityEngine;

namespace Pirates.EditorTools
{
    /// <summary>
    /// VfxAssetのInspector。下部のプレビュー欄のヘッダー（AudioClipの再生ボタンと同じ場所）に再生／停止／ループを置く。
    /// ParticleSystemを経過時間で進めて描画するので、ゲームを起動せずに見た目を確認できる。
    /// ドラッグで回転、ホイールでズーム。
    /// </summary>
    [CustomEditor(typeof(VfxAsset))]
    public class VfxAssetEditor : Editor
    {
        PreviewRenderUtility preview;
        GameObject instance;
        GameObject instancePrefab;
        ParticleSystem[] systems;

        bool playing;
        bool loop = true;
        double startTime;
        float elapsed;

        float yaw = 20f;
        float pitch = 20f;
        float distance = 8f;

        void OnDisable() => Cleanup();

        void Cleanup()
        {
            if (instance != null) DestroyImmediate(instance);
            instance = null;
            instancePrefab = null;
            systems = null;
            if (preview != null) preview.Cleanup();
            preview = null;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var asset = (VfxAsset)target;
            if (asset.Prefab == null) return;

            EditorGUILayout.Space();
            if (GUILayout.Button("Prefab から再生時間を自動設定"))
            {
                serializedObject.Update();
                serializedObject.FindProperty("lifetime").floatValue = Mathf.Ceil(EstimateDuration(asset.Prefab) * 10f) / 10f;
                serializedObject.ApplyModifiedProperties();
            }
        }

        // ParticleSystemの 開始遅延＋再生時間＋粒の寿命 の最大値
        static float EstimateDuration(GameObject prefab)
        {
            float max = 0.5f;
            foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                float t = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
                if (t > max) max = t;
            }
            return max;
        }

        // ---- プレビュー ----

        public override bool HasPreviewGUI() => true;
        public override GUIContent GetPreviewTitle() => new GUIContent("VFX Preview");
        public override bool RequiresConstantRepaint() => playing;

        // プレビュー欄のヘッダー（AudioClipの再生ボタンと同じ位置）
        public override void OnPreviewSettings()
        {
            GUIStyle button = GUI.skin.FindStyle("preButton") ?? EditorStyles.miniButton;

            if (GUILayout.Button(new GUIContent("▶", "再生"), button, GUILayout.Width(30f))) Play();
            if (GUILayout.Button(new GUIContent("■", "停止"), button, GUILayout.Width(30f))) Stop();
            loop = GUILayout.Toggle(loop, new GUIContent("Loop", "ループ再生"), button, GUILayout.Width(40f));
        }

        void Play()
        {
            if (!EnsureInstance()) return;
            startTime = EditorApplication.timeSinceStartup;
            elapsed = 0f;
            playing = true;
        }

        void Stop()
        {
            playing = false;
            elapsed = 0f;
            if (systems == null) return;
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                ps.Simulate(0f, false, true);
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            Repaint();
        }

        bool EnsureInstance()
        {
            var asset = (VfxAsset)target;
            if (asset.Prefab == null) return false;

            if (preview == null)
            {
                preview = new PreviewRenderUtility();
                preview.camera.fieldOfView = 30f;
                preview.camera.nearClipPlane = 0.1f;
                preview.camera.farClipPlane = 200f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.17f, 0.18f, 0.2f);
            }

            // Prefabが差し替わったら作り直す
            if (instance == null || instancePrefab != asset.Prefab)
            {
                if (instance != null) DestroyImmediate(instance);
                instance = Instantiate(asset.Prefab);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                preview.AddSingleGO(instance);
                instancePrefab = asset.Prefab;
                systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            }
            return true;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            HandleInput(r);
            if (Event.current.type != EventType.Repaint) return;

            var asset = (VfxAsset)target;
            if (asset.Prefab == null)
            {
                GUI.Label(r, "Prefab が未設定です", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            if (!EnsureInstance()) return;

            float duration = Mathf.Max(0.1f, asset.Lifetime);
            if (playing)
            {
                elapsed = (float)(EditorApplication.timeSinceStartup - startTime);
                if (elapsed > duration)
                {
                    if (loop) { startTime += duration; elapsed -= duration; }
                    else { playing = false; elapsed = duration; }
                }
                foreach (ParticleSystem ps in systems)
                {
                    if (ps != null) ps.Simulate(elapsed, false, true);
                }
            }

            Vector3 focus = Vector3.up;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Transform cam = preview.camera.transform;
            cam.position = focus + rot * (Vector3.back * distance);
            cam.rotation = rot;

            preview.BeginPreview(r, background);
            preview.Render(true);
            Texture tex = preview.EndPreview();
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, false);

            GUI.Label(new Rect(r.x + 6f, r.y + 4f, 160f, 18f), $"{elapsed:F2} / {duration:F2} s", EditorStyles.whiteMiniLabel);
        }

        void HandleInput(Rect r)
        {
            Event e = Event.current;
            if (!r.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                yaw += e.delta.x * 0.5f;
                pitch = Mathf.Clamp(pitch + e.delta.y * 0.5f, -80f, 80f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                distance = Mathf.Clamp(distance + e.delta.y * 0.3f, 1f, 60f);
                e.Use();
                Repaint();
            }
        }
    }
}
