using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates
{
    /// <summary>
    /// デバッグ用：クリックした照準位置にマーカー（Gameビューでも見える）とGizmoを出す。
    /// 判定そのものは AimController が行う。
    /// </summary>
    public class ClickPointGizmo : MonoBehaviour
    {
        [SerializeField]
        AimController aim;

        [Header("表示")]
        [SerializeField]
        float radius = 0.3f;
        [SerializeField]
        Color color = Color.red;

        bool hasHit;
        Vector3 hitPoint;
        Transform marker; // Gameビューでも見えるマーカー

        void Update()
        {
            var mouse = Mouse.current;
            if (aim == null || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            aim.Refresh();
            if (!aim.HasAim) return;

            hasHit = true;
            hitPoint = aim.AimPoint;
            ShowMarker(hitPoint);
        }

        void ShowMarker(Vector3 position)
        {
            if (marker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "ClickPointMarker";
                Destroy(go.GetComponent<Collider>()); // マーカー自身がレイに当たらないように
                go.transform.localScale = Vector3.one * radius * 2f;
                go.GetComponent<Renderer>().material.color = color;
                marker = go.transform;
            }
            marker.position = position;
        }

        void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }

        void OnDrawGizmos()
        {
            if (!hasHit) return;
            Gizmos.color = color;
            Gizmos.DrawSphere(hitPoint, radius);
        }
    }
}
