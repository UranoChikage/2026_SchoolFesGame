using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 砲手1人ぶんの所持アイテム。CannonStation が自動で付ける（設定不要）。
    /// 1人1つまで。所持中は新しく取得できない。使うと所持が空になり、効果が終わると通常の大砲に戻る。
    /// </summary>
    public class ItemHolder : MonoBehaviour
    {
        const int HitBuffer = 16;
        const float BeamDamage = 99999f; // 一撃で沈める

        static readonly Collider[] hits = new Collider[HitBuffer];

        CannonShooter shooter;
        ItemAsset held;
        ItemAsset beam;
        float beamEnd;
        Transform beamVisual;

        public ItemAsset Held => held;
        /// <summary>ビーム使用中。この間この砲手は通常の大砲を撃てない</summary>
        public bool IsBeamActive => beam != null && Time.time < beamEnd;

        public static ItemHolder For(CannonShooter shooter)
        {
            var h = shooter.GetComponent<ItemHolder>();
            if (h == null) h = shooter.gameObject.AddComponent<ItemHolder>();
            h.shooter = shooter;
            return h;
        }

        public bool TryAcquire(ItemAsset item)
        {
            if (item == null || held != null) return false;
            held = item;
            Debug.Log($"[Item] {name} が {item.DisplayName} を取得", this);
            return true;
        }

        /// <summary>所持アイテムを使う。使えたらtrue（発射ボタンの代わりに呼ぶ）</summary>
        public bool TryUse()
        {
            if (held == null || IsBeamActive) return false;

            ItemAsset item = held;
            held = null;
            Debug.Log($"[Item] {name} が {item.DisplayName} を使用", this);

            if (item.Kind == ItemKind.Barrier)
            {
                ShipBarrier barrier = ShipBarrier.ForPlayer();
                if (barrier != null) barrier.Activate(item);
            }
            else
            {
                beam = item;
                beamEnd = Time.time + item.Duration;
            }
            return true;
        }

        /// <summary>毎フレーム、カーソルが指す位置を渡す。ビーム使用中、その位置の船を沈める</summary>
        public void Tick(Vector3 aimPoint, bool hasAim)
        {
            bool active = IsBeamActive && hasAim;
            if (beamVisual != null) beamVisual.gameObject.SetActive(active);
            if (!active) return;

            ShowBeam(aimPoint);

            Transform owner = shooter.Owner;
            int count = Physics.OverlapSphereNonAlloc(aimPoint, beam.BeamRadius, hits, shooter.HitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (owner != null && hits[i].transform.IsChildOf(owner)) continue;
                var health = hits[i].GetComponentInParent<ShipHealth>();
                if (health != null && !health.IsDead) health.TakeDamage(BeamDamage, aimPoint);
            }
        }

        void OnDisable()
        {
            if (beamVisual != null) beamVisual.gameObject.SetActive(false);
        }

        // 仮の見た目：砲口からカーソル位置へ伸びる細い半透明の円柱。専用のVFXができたら差し替える
        void ShowBeam(Vector3 point)
        {
            if (beamVisual == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "BeamVisual";
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"));
                beamVisual = go.transform;
            }
            Color c = beam.Color;
            c.a = 0.5f;
            beamVisual.GetComponent<Renderer>().material.color = c;

            Vector3 from = shooter.MuzzlePosition;
            Vector3 to = point - from;
            float length = to.magnitude;
            if (length < 0.01f) return;

            float width = Mathf.Min(1f, beam.BeamRadius); // 見た目の太さ（判定の広さとは別）
            beamVisual.position = from + to * 0.5f;
            beamVisual.rotation = Quaternion.FromToRotation(Vector3.up, to / length); // 円柱は上方向に伸びる
            beamVisual.localScale = new Vector3(width, length * 0.5f, width); // 円柱の高さは scale.y の2倍
        }
    }
}
