using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chambara
{
    /// <summary>
    /// キャラクター選択画面用の背景アニメーション。
    /// 半透明の長方形が上から下へ降り続け、一生をかけて幅・高さが一方向に変化する。
    /// Canvas 内の全画面ストレッチした RectTransform に付けて使う(UI より背面に置くこと)。
    /// 設定は FallingRectBackgroundSettings(ScriptableObject)で行う。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FallingRectBackground : MonoBehaviour
    {
        [SerializeField] FallingRectBackgroundSettings settings;

        class FallingRect
        {
            public RectTransform rt;
            public Image image;
            public int colorIndex;
            public float alpha;
            public float x;        // 中心の横位置(左端=0)
            public float spawnY;   // 生成時の下端位置(上端=0, 下向き正)
            public float y;        // 現在の下端位置
            public float w0, w1, h0, h1;
            public float speed;
        }

        readonly List<FallingRect> rects = new List<FallingRect>();
        RectTransform area;
        Image background;

        float Width => area.rect.width;
        float Height => area.rect.height;

        void Awake()
        {
            area = (RectTransform)transform;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(area, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            background = bgGo.GetComponent<Image>();
            background.raycastTarget = false;
        }

        void Start()
        {
            if (settings == null)
            {
                Debug.LogWarning($"{nameof(FallingRectBackground)}: settings が未設定です", this);
                enabled = false;
                return;
            }
            // 起動直後から画面が埋まって見えるよう、最初だけ縦位置を全体にばらけさせる
            SyncCount(true);
        }

        void Update()
        {
            if (settings == null) return;

            SyncCount(false);
            background.color = settings.backgroundColor;

            float dt = settings.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            dt = Mathf.Min(dt, settings.maxDeltaTime);

            float h = Height;
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                r.y += r.speed * settings.speedMultiplier * dt;

                float p = Progress(r, h);
                if (p >= 1f)
                {
                    // 上端が画面下端を完全に越えた → 上部に再生成
                    Respawn(r, false);
                    p = 0f;
                }
                ApplyShape(r, p);
                ApplyColor(r);
            }
        }

        float Progress(FallingRect r, float screenH)
        {
            float d = (screenH - r.spawnY) + r.h1;
            return Mathf.Clamp01((r.y - r.spawnY) / d);
        }

        void ApplyShape(FallingRect r, float p)
        {
            float e = p * p * (3f - 2f * p);
            float w = Mathf.Lerp(r.w0, r.w1, e);
            float hh = Mathf.Lerp(r.h0, r.h1, e);
            r.rt.sizeDelta = new Vector2(w, hh);
            r.rt.anchoredPosition = new Vector2(r.x, -r.y);
        }

        void ApplyColor(FallingRect r)
        {
            var colors = settings.colors;
            Color c = (colors != null && colors.Length > 0) ? colors[r.colorIndex % colors.Length] : Color.white;
            c.a = r.alpha;
            r.image.color = c;
        }

        void Respawn(FallingRect r, bool initial)
        {
            var s = settings;
            r.x = Random.Range(0f, Width);

            r.w0 = Random.Range(s.widthRange.x, s.widthRange.y);
            r.h0 = Random.Range(s.heightRange.x, s.heightRange.y);
            r.w1 = r.w0 + (Random.Range(s.widthRange.x, s.widthRange.y) - r.w0) * s.deformAmount;
            r.h1 = r.h0 + (Random.Range(s.heightRange.x, s.heightRange.y) - r.h0) * s.deformAmount;
            r.speed = Random.Range(s.speedRange.x, s.speedRange.y);

            // 下端が画面上端より上 → 長方形全体が見えない位置
            r.spawnY = -Random.Range(0f, s.spawnAboveMargin);
            r.y = r.spawnY;
            if (initial)
            {
                float d = (Height - r.spawnY) + r.h1;
                r.y = r.spawnY + Random.Range(0f, d);
            }

            r.colorIndex = Random.Range(0, 1024);
            r.alpha = Random.Range(s.alphaRange.x, s.alphaRange.y);

            ApplyShape(r, Progress(r, Height));
            ApplyColor(r);
        }

        void SyncCount(bool initial)
        {
            while (rects.Count < settings.count)
            {
                var go = new GameObject("Rect", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(area, false);
                // 左上基準、ピボットは下端中央(幅変化で左右対称に伸縮)
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;

                var r = new FallingRect { rt = rt, image = img };
                rects.Add(r);
                // 実行中に増やした分は画面上部から登場させる
                Respawn(r, initial);
            }
            while (rects.Count > settings.count)
            {
                var r = rects[rects.Count - 1];
                rects.RemoveAt(rects.Count - 1);
                Destroy(r.rt.gameObject);
            }
        }
    }
}
