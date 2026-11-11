using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// キャラ選択画面で決定したときに、プレビューのキャラの位置へエフェクトを出す
    /// プレビュー（SelectModelSwitcher と同じオブジェクト）に付ける
    /// 出したエフェクトのレイヤーはこのオブジェクトと同じにする（プレビュー用カメラに映るように）
    /// </summary>
    public class SelectPickEffect : MonoBehaviour
    {
        [SerializeField] CharacterSelectManager manager;
        [Tooltip("同じオブジェクトに SelectModelSwitcher があればそちらの番号を使う")]
        [SerializeField] int player;

        [Header("Effect")]
        [Tooltip("キャラ・剣を決定するたびに出す")]
        [SerializeField] GameObject pickEffect;
        [Tooltip("キャラと剣の両方が決まったときに追加で出す（任意）")]
        [SerializeField] GameObject readyEffect;
        [Tooltip("出してから消すまで（実時間）")]
        [SerializeField] float lifetime = 2f;

        [Header("Position")]
        [Tooltip("エフェクトを出す位置。未設定なら自分（ジャンプでは動かない足元）")]
        [SerializeField] Transform anchor;
        [Tooltip("anchor からのずれ（anchor の座標系）")]
        [SerializeField] Vector3 offset = Vector3.zero;
        [Tooltip("エフェクトを anchor の子にして一緒に動かす")]
        [SerializeField] bool followAnchor = false;

        Transform Anchor => anchor != null ? anchor : transform;

        void Start()
        {
            if (manager == null) manager = FindAnyObjectByType<CharacterSelectManager>();
            if (TryGetComponent(out SelectModelSwitcher switcher)) player = switcher.Player;
            if (manager == null) return;
            manager.OnPicked += OnPicked;
            manager.OnReadyChanged += OnReadyChanged;
        }

        void OnDestroy()
        {
            if (manager == null) return;
            manager.OnPicked -= OnPicked;
            manager.OnReadyChanged -= OnReadyChanged;
        }

        void OnPicked(int p, SelectSlot slot)
        {
            if (p == player) Spawn(pickEffect);
        }

        void OnReadyChanged(int p, bool ready)
        {
            if (p == player && ready) Spawn(readyEffect);
        }

        /// <summary>prefab を anchor の位置に出す</summary>
        public void Spawn(GameObject prefab)
        {
            if (prefab == null) return;
            Transform a = Anchor;
            Vector3 pos = a.TransformPoint(offset);
            var effect = Instantiate(prefab, pos, a.rotation * prefab.transform.rotation);
            if (followAnchor) effect.transform.SetParent(a, true);

            int layer = gameObject.layer;
            foreach (var t in effect.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            // 選択画面は実時間で進めているので、パーティクルも実時間で再生する
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }

            StartCoroutine(DestroyLater(effect));
        }

        System.Collections.IEnumerator DestroyLater(GameObject effect)
        {
            yield return new WaitForSecondsRealtime(lifetime);
            if (effect != null) Destroy(effect);
        }
    }
}
