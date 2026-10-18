using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 船大福バリア。一定時間、船が受けるダメージを無効にする。
    /// 使用中に再使用すると残り時間が最初に戻るだけ（重ね掛けで延びない）。
    /// プレイヤー船に自動で付くので、シーンでの設定は不要。
    /// </summary>
    public class ShipBarrier : MonoBehaviour
    {
        ShipHealth health;
        float endTime;
        Transform visual;
        ItemAsset visualFor;

        public bool IsActive => Time.time < endTime;

        /// <summary>プレイヤー船のバリア。なければ自動で付ける。プレイヤー船が無ければnull</summary>
        public static ShipBarrier ForPlayer()
        {
            PlayerTarget player = PlayerTarget.Current;
            if (player == null) return null;
            var b = player.GetComponent<ShipBarrier>();
            return b != null ? b : player.gameObject.AddComponent<ShipBarrier>();
        }

        public void Activate(ItemAsset item)
        {
            if (health == null) health = GetComponent<ShipHealth>();
            if (health == null) return;

            endTime = Time.time + item.Duration; // 重ねても延びず、最初に戻るだけ
            health.Invulnerable = true;
            ShowVisual(item);
        }

        void Update()
        {
            if (health != null && health.Invulnerable && !IsActive) Deactivate();
        }

        void OnDisable() => Deactivate();

        void Deactivate()
        {
            endTime = 0f;
            if (health != null) health.Invulnerable = false;
            if (visual != null) visual.gameObject.SetActive(false);
        }

        // 仮の見た目：半透明の球。専用の見た目ができたら差し替える
        void ShowVisual(ItemAsset item)
        {
            if (visual == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "BarrierVisual";
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"));
                visual = go.transform;
                visual.SetParent(transform, false);
            }
            if (visualFor != item)
            {
                Color c = item.Color;
                c.a = 0.3f;
                visual.GetComponent<Renderer>().material.color = c;
                visual.localScale = Vector3.one * item.BarrierRadius * 2f / Mathf.Max(0.01f, transform.lossyScale.x);
                visualFor = item;
            }
            visual.gameObject.SetActive(true);
        }
    }
}
