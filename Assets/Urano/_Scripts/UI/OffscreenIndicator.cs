using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pirates
{
    /// <summary>
    /// 画面外にいる敵とアイテムの方向を、画面の端にアイコンで示す。
    /// 近いものから最大 maxIcons 個まで。シーンに置かなくても起動時に自動で作られる。
    /// アイコンは仮の三角形（Icon Sprite を設定すれば差し替え可能、上向きの絵にする）。
    /// </summary>
    public class OffscreenIndicator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("同時に出すアイコンの最大数")]
        int maxIcons = 10;
        [SerializeField]
        [Tooltip("この距離(m)より遠いものは出さない（「ある程度近いもの」の範囲）")]
        float maxDistance = 80f;
        [SerializeField]
        [Tooltip("画面の端からアイコンまでの余白(ピクセル)")]
        float edgeMargin = 40f;
        [SerializeField]
        float iconSize = 36f;
        [SerializeField]
        Color enemyColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        [SerializeField]
        [Tooltip("アイテムはアイテムごとの色を使う。OFFなら下の色で統一")]
        bool useItemColor = true;
        [SerializeField]
        Color itemColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        [SerializeField]
        [Tooltip("アイコンの絵（任意・上向き）。空なら仮の三角形")]
        Sprite iconSprite;

        struct Entry
        {
            public Vector3 position;
            public float distance;
            public Color color;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly List<RectTransform> icons = new List<RectTransform>();
        readonly List<Image> images = new List<Image>();
        RectTransform root;
        Camera cam;

        // シーンに無ければ起動時に1つ作る
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<OffscreenIndicator>() != null) return;
            new GameObject("OffscreenIndicator").AddComponent<OffscreenIndicator>();
        }

        void Awake()
        {
            var canvasGo = new GameObject("IndicatorCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            root = canvasGo.GetComponent<RectTransform>();

            if (iconSprite == null) iconSprite = CreateTriangleSprite();
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || PlayerTarget.Current == null)
            {
                Show(0);
                return;
            }

            Collect();
            entries.Sort((a, b) => a.distance.CompareTo(b.distance));

            int count = 0;
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 half = center - Vector2.one * edgeMargin;

            for (int i = 0; i < entries.Count && count < maxIcons; i++)
            {
                if (!TryEdgePosition(entries[i].position, center, half, out Vector2 pos, out Vector2 dir)) continue;

                RectTransform icon = GetIcon(count);
                icon.anchoredPosition = pos;
                icon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                images[count].color = entries[i].color;
                count++;
            }
            Show(count);
        }

        void Collect()
        {
            entries.Clear();
            Vector3 origin = PlayerTarget.Current.transform.position;

            foreach (EnemyShip e in EnemyShip.Active)
            {
                if (e.IsSunk) continue;
                Add(e.transform.position, origin, enemyColor);
            }
            foreach (ItemPickup p in ItemPickup.Active)
            {
                Add(p.transform.position, origin, useItemColor ? p.Color : itemColor);
            }
        }

        void Add(Vector3 pos, Vector3 origin, Color color)
        {
            float d = Vector3.Distance(pos, origin);
            if (d > maxDistance) return;
            entries.Add(new Entry { position = pos, distance = d, color = color });
        }

        // 画面内ならfalse。画面外なら、中心から対象へ向かう線が画面の端と交わる位置を返す
        bool TryEdgePosition(Vector3 world, Vector2 center, Vector2 half, out Vector2 pos, out Vector2 dir)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            pos = default;
            dir = default;

            bool behind = sp.z < 0f;
            if (!behind && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height) return false;

            Vector2 d = new Vector2(sp.x, sp.y) - center;
            if (behind) d = -d; // 背後は投影が反転するので逆向きにする
            if (d.sqrMagnitude < 0.0001f) d = Vector2.down; // ほぼ真後ろ
            dir = d.normalized;

            float tx = Mathf.Abs(dir.x) > 0.0001f ? half.x / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.0001f ? half.y / Mathf.Abs(dir.y) : float.MaxValue;
            pos = center + dir * Mathf.Min(tx, ty);
            return true;
        }

        RectTransform GetIcon(int index)
        {
            while (icons.Count <= index)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero; // 左下基準でピクセル指定
                rt.sizeDelta = Vector2.one * iconSize;
                var img = go.GetComponent<Image>();
                img.sprite = iconSprite;
                img.raycastTarget = false;
                icons.Add(rt);
                images.Add(img);
            }
            icons[index].gameObject.SetActive(true);
            return icons[index];
        }

        void Show(int count)
        {
            for (int i = count; i < icons.Count; i++) icons[i].gameObject.SetActive(false);
        }

        // 上向きの三角形
        static Sprite CreateTriangleSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float halfWidth = (1f - y / (float)(size - 1)) * 0.5f * size; // 上ほど細い
                for (int x = 0; x < size; x++)
                {
                    px[y * size + x] = Mathf.Abs(x - (size - 1) * 0.5f) <= halfWidth ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
