using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// 斬り・突き・ガードのときに、プレイヤーごとの UI を指定範囲内のランダムな位置に出す。
    /// 斬り・突き：消すのは Prefab 側のアニメ（最後に SetActive(false) する）。非アクティブになったものは次に出すときに再利用する
    /// ガード：構えている間だけ表示する（アニメなし。拡大して出てきて、構えている間は少しずつ小さくなる）
    /// </summary>
    public class AttackCallUi : MonoBehaviour
    {
        [System.Serializable]
        class PlayerCallUi
        {
            [Tooltip("斬ったときに出す UI（子の TextMeshPro に「右アタック！」などを入れる）")]
            public GameObject attackPrefab;
            [Tooltip("突いたときに出す UI")]
            public GameObject thrustPrefab;
            [Tooltip("ガードしている間出す UI（子の TextMeshPro に「上ガード」などを入れる）")]
            public GameObject guardPrefab;
            [Tooltip("出現範囲。この RectTransform の矩形内のランダムな位置に、子として生成する")]
            public RectTransform spawnArea;
            [Tooltip("ガード UI の向きごとの出現範囲（上・右・下・左の順）。空の向きは spawnArea のその向き側に出す")]
            public RectTransform[] guardAreas = new RectTransform[4];

            [System.NonSerialized] public GameObject guardUi;      // ガード UI は1人1個を使い回す
            [System.NonSerialized] public TMP_Text guardText;
            [System.NonSerialized] public float guardStart;         // ガード UI を出した時刻
        }

        [SerializeField] PlayerCallUi player1 = new PlayerCallUi();
        [SerializeField] PlayerCallUi player2 = new PlayerCallUi();

        [Header("斬り UI")]
        [SerializeField, Tooltip("振った向きごとの表示（上・右上・右・右下・下・左下・左・左上の順）")]
        string[] attackLabels = { "上アタック！", "右上アタック！", "右アタック！", "右下アタック！", "下アタック！", "左下アタック！", "左アタック！", "左上アタック！" };

        [Header("ガード UI")]
        [SerializeField, Tooltip("ガードの向きごとの表示（上・右・下・左の順）")]
        string[] guardLabels = { "上ガード", "右ガード", "下ガード", "左ガード" };
        [SerializeField, Tooltip("0 から 1 の大きさになるまでの秒数")]
        float guardAppearTime = 0.4f;
        [SerializeField, Tooltip("出きった後、1秒あたりに小さくなる量")]
        float guardShrinkSpeed = 0.03f;
        [SerializeField, Tooltip("これより小さくはならない")]
        float guardMinScale = 0.85f;
        [SerializeField, Range(0.1f, 1f), Tooltip("guardAreas が空のとき、spawnArea の端からどこまでを出現範囲にするか（割合）")]
        float guardSideRatio = 0.35f;

        readonly Dictionary<GameObject, List<GameObject>> pools = new Dictionary<GameObject, List<GameObject>>();   // Prefab ごとの生成済み UI

        ChambaraManager subscribedManager;   // 吹っ飛び演出の開始を受け取る
        PlayerBehaviour[] subscribed;   // 今イベントを登録しているプレイヤー（ラウンドごとに作り直されるので監視する）
        System.Action<PlayerState, int>[] handlers = new System.Action<PlayerState, int>[2];
        PlayerState[] lastStates = new PlayerState[2];   // 同じ状態が毎フレーム通知されることがあるので、変わった瞬間だけ反応する
        int[] lastDirs = new int[2];

        void Update()
        {
            UpdateSubscription();
            UpdateGuardScale(player1);
            UpdateGuardScale(player2);
        }

        void UpdateSubscription()
        {
            var manager = ChambaraManager.Instance;
            if (manager != subscribedManager)
            {
                if (subscribedManager != null) subscribedManager.OnKnockback -= OnKnockback;
                subscribedManager = manager;
                if (manager != null) manager.OnKnockback += OnKnockback;
            }
            PlayerBehaviour[] players = manager != null ? manager.Players : null;
            if (players == subscribed) return;
            Unsubscribe();
            HideGuard(player1);
            HideGuard(player2);
            if (players == null) return;
            subscribed = players;
            for (int i = 0; i < players.Length && i < handlers.Length; i++)
            {
                if (players[i] == null) continue;
                int index = i;
                lastStates[i] = players[i].Data != null ? players[i].Data.state : PlayerState.Idle;
                lastDirs[i] = -1;
                handlers[i] = (state, dir) => OnStateChanged(index, state, dir);
                players[i].OnStateChanged += handlers[i];
            }
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (subscribedManager != null) subscribedManager.OnKnockback -= OnKnockback;
            subscribedManager = null;
        }

        // 吹っ飛び演出に切り替わったら、出ている UI をすべて消す（ガード中にやられたときに残らないように）
        void OnKnockback(PlayerBehaviour loser) => HideAll();

        void HideAll()
        {
            HideGuard(player1);
            HideGuard(player2);
            foreach (List<GameObject> pool in pools.Values)
            {
                foreach (GameObject g in pool)
                {
                    if (g != null) g.SetActive(false);
                }
            }
        }

        void Unsubscribe()
        {
            if (subscribed != null)
            {
                for (int i = 0; i < subscribed.Length && i < handlers.Length; i++)
                {
                    if (subscribed[i] != null && handlers[i] != null) subscribed[i].OnStateChanged -= handlers[i];
                }
            }
            subscribed = null;
            for (int i = 0; i < handlers.Length; i++) handlers[i] = null;
        }

        void OnStateChanged(int playerIndex, PlayerState state, int dir)
        {
            if (lastStates[playerIndex] == state && lastDirs[playerIndex] == dir) return;
            lastStates[playerIndex] = state;
            lastDirs[playerIndex] = dir;

            PlayerCallUi ui = playerIndex == 0 ? player1 : player2;
            if (state == PlayerState.Guard) ShowGuard(ui, dir);
            else HideGuard(ui);

            GameObject prefab = state switch
            {
                PlayerState.Attack => ui.attackPrefab,
                PlayerState.Thrust => ui.thrustPrefab,
                _ => null,
            };
            if (prefab == null || ui.spawnArea == null) return;

            GameObject go = Get(prefab, ui.spawnArea);
            go.transform.localPosition = RandomPosition(ui.spawnArea);
            go.transform.SetAsLastSibling();   // 新しく出したものを手前に
            if (state == PlayerState.Attack && dir >= 0 && dir < attackLabels.Length)
            {
                TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
                if (text != null) text.text = attackLabels[dir];
            }
            go.SetActive(true);                // アクティブにし直すと Animator は最初から再生される
        }

        void ShowGuard(PlayerCallUi ui, int dir)
        {
            if (ui.guardPrefab == null || ui.spawnArea == null) return;
            if (ui.guardUi == null)
            {
                ui.guardUi = Instantiate(ui.guardPrefab, ui.spawnArea, false);
                ui.guardText = ui.guardUi.GetComponentInChildren<TMP_Text>(true);
            }
            int labelIndex = dir / 2;   // 0,2,4,6 → 上右下左
            if (ui.guardText != null && labelIndex >= 0 && labelIndex < guardLabels.Length) ui.guardText.text = guardLabels[labelIndex];

            Transform t = ui.guardUi.transform;
            RectTransform area = labelIndex >= 0 && ui.guardAreas != null && labelIndex < ui.guardAreas.Length ? ui.guardAreas[labelIndex] : null;
            if (area != null)
            {
                if (t.parent != area) t.SetParent(area, false);
                t.localPosition = RandomPosition(area);
            }
            else
            {
                if (t.parent != ui.spawnArea) t.SetParent(ui.spawnArea, false);
                t.localPosition = SidePosition(ui.spawnArea, labelIndex);
            }
            t.localScale = Vector3.zero;
            t.SetAsLastSibling();
            ui.guardStart = Time.time;
            ui.guardUi.SetActive(true);
        }

        void HideGuard(PlayerCallUi ui)
        {
            if (ui.guardUi != null) ui.guardUi.SetActive(false);
        }

        // 0.4秒ほどで 0→1、その後は構えている間ほんの少しずつ小さくなる
        void UpdateGuardScale(PlayerCallUi ui)
        {
            if (ui.guardUi == null || !ui.guardUi.activeSelf) return;
            float elapsed = Time.time - ui.guardStart;
            float scale;
            if (elapsed < guardAppearTime)
            {
                float t = elapsed / guardAppearTime;
                scale = 1f - (1f - t) * (1f - t);   // 最初は速く、最後はゆっくり
            }
            else
            {
                scale = Mathf.Max(guardMinScale, 1f - (elapsed - guardAppearTime) * guardShrinkSpeed);
            }
            ui.guardUi.transform.localScale = Vector3.one * scale;
        }

        // spawnArea のうち、向き（0:上 1:右 2:下 3:左）側の端の帯の中のランダムな位置
        Vector3 SidePosition(RectTransform area, int side)
        {
            Rect r = area.rect;
            float w = r.width * guardSideRatio, h = r.height * guardSideRatio;
            Rect band = side switch
            {
                0 => new Rect(r.xMin, r.yMax - h, r.width, h),
                1 => new Rect(r.xMax - w, r.yMin, w, r.height),
                2 => new Rect(r.xMin, r.yMin, r.width, h),
                3 => new Rect(r.xMin, r.yMin, w, r.height),
                _ => r,
            };
            return new Vector3(Random.Range(band.xMin, band.xMax), Random.Range(band.yMin, band.yMax), 0f);
        }

        static Vector3 RandomPosition(RectTransform area)
        {
            Rect r = area.rect;
            return new Vector3(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax), 0f);
        }

        // 非アクティブ（アニメが終わった）ものがあれば再利用し、なければ新しく作る
        GameObject Get(GameObject prefab, RectTransform parent)
        {
            if (!pools.TryGetValue(prefab, out List<GameObject> pool))
            {
                pool = new List<GameObject>();
                pools.Add(prefab, pool);
            }
            pool.RemoveAll(g => g == null);   // 外から消されたものは除く
            foreach (GameObject g in pool)
            {
                if (!g.activeSelf)
                {
                    if (g.transform.parent != parent) g.transform.SetParent(parent, false);
                    return g;
                }
            }
            GameObject created = Instantiate(prefab, parent, false);
            created.SetActive(false);
            pool.Add(created);
            return created;
        }
    }
}
