using System.Collections.Generic;
using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 宝箱やアイテムを取ったときの獲得通知UI。
    /// ・通知には寿命があり、寿命が来るまで出続ける。新しい通知は一番手前(下)に出て、古い通知は上へずれる
    /// ・同時に出せるのは最大 maxCount 個。超えたら一番古いものから消す
    /// ・消えるときはモデルごとフェードアウト
    /// ・宝箱・アイテムともアイコンを出す。後ろのエフェクトの色は、アイテムなら取った砲手の色、宝箱ならお宝ごとのグラデーション
    /// 通知1件の見た目は PickupNoticeView の Prefab で作って割り当てる。
    /// </summary>
    public class PickupNotifier : MonoBehaviour
    {
        class Entry
        {
            public PickupNoticeView view;
            public float age;
            public bool fading;
            public float fadeAge;
        }

        [Header("UI")]
        [SerializeField]
        [Tooltip("通知1件の見た目のPrefab（PickupNoticeView付き）")]
        PickupNoticeView viewPrefab;
        [SerializeField]
        [Tooltip("通知を並べる親。この中の anchoredPosition で積み上げる（基準は親の中心）")]
        RectTransform container;

        [Header("並び・寿命")]
        [SerializeField]
        [Min(1)]
        [Tooltip("同時に出せる通知の最大数")]
        int maxCount = 3;
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("通知が出ている時間(秒)")]
        float lifetime = 3f;
        [SerializeField]
        [Min(0.01f)]
        float fadeTime = 0.4f;
        [SerializeField]
        [Tooltip("古い通知が1つずれる量（UI座標）。上に積むなら (0, 高さ+隙間)")]
        Vector2 slotOffset = new Vector2(0f, 140f);
        [SerializeField]
        [Tooltip("ずれる速さ。大きいほど素早く動く")]
        float shiftSpeed = 12f;
        [SerializeField]
        float popTime = 0.25f;

        // 先頭が一番新しい
        readonly List<Entry> entries = new List<Entry>();
        readonly Stack<PickupNoticeView> free = new Stack<PickupNoticeView>();
        TreasureWallet wallet;

        void OnEnable() => ItemHolder.Acquired += OnItemAcquired;

        // TreasureWallet.Current は Awake で決まるので、購読は Start で行う
        void Start()
        {
            wallet = TreasureWallet.Current;
            if (wallet != null) wallet.Added += OnTreasureAdded;
        }

        void OnDisable()
        {
            ItemHolder.Acquired -= OnItemAcquired;
            if (wallet != null) wallet.Added -= OnTreasureAdded;
            wallet = null;
            foreach (Entry e in entries) Recycle(e);
            entries.Clear();
        }

        void OnItemAcquired(ItemAsset item, Color playerColor)
        {
            if (item != null) Push(item.Icon, playerColor, null);
        }

        void OnTreasureAdded(TreasureAsset treasure, int total)
        {
            if (treasure != null) Push(treasure.Icon, Color.white, treasure.NoticeColor);
        }

        /// <summary>gradient があれば時間で色が変わる。無ければ color 一色</summary>
        void Push(Sprite icon, Color color, Gradient gradient)
        {
            if (viewPrefab == null || container == null) return;
            if (icon == null) return; // 見せるものが無い

            // 上限を超えないよう、消えていない通知のうち一番古いものからフェードさせる
            int alive = 0;
            foreach (Entry e in entries) if (!e.fading) alive++;
            for (int i = entries.Count - 1; i >= 0 && alive >= maxCount; i--)
            {
                if (entries[i].fading) continue;
                entries[i].fading = true;
                alive--;
            }

            PickupNoticeView view = free.Count > 0 ? free.Pop() : Instantiate(viewPrefab, container);
            view.Rect.anchoredPosition = Vector2.zero; // 一番手前の位置から出す
            view.Show(icon, color, gradient);
            entries.Insert(0, new Entry { view = view });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float follow = 1f - Mathf.Exp(-shiftSpeed * dt);

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry e = entries[i];
                e.age += dt;
                if (!e.fading && e.age >= lifetime) e.fading = true;

                float alpha = 1f;
                if (e.fading)
                {
                    e.fadeAge += dt;
                    alpha = 1f - e.fadeAge / fadeTime;
                    if (alpha <= 0f)
                    {
                        Recycle(e);
                        entries.RemoveAt(i);
                        continue;
                    }
                }

                PickupNoticeView v = e.view;
                v.SetAlpha(alpha);
                v.Tick(e.age);
                v.Rect.localScale = Vector3.one * (e.age < popTime ? EaseOutBack(e.age / popTime) : 1f);
                v.Rect.anchoredPosition = Vector2.Lerp(v.Rect.anchoredPosition, slotOffset * i, follow);
            }
        }

        void Recycle(Entry e)
        {
            if (e.view == null) return;
            e.view.Hide();
            free.Push(e.view);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            x = Mathf.Clamp01(x) - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
