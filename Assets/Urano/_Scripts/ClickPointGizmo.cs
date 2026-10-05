using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates
{
    public class ClickPointGizmo : MonoBehaviour
    {
        [Header("レイを飛ばすカメラ（未設定ならMainCamera）")]
        [SerializeField]
        Camera targetCamera;

        [Header("判定")]
        [SerializeField]
        [Tooltip("レイが当たる最大距離")]
        float maxDistance = 1000f;
        [SerializeField]
        [Tooltip("当たり判定を取るレイヤー")]
        LayerMask layerMask = ~0;

        [Header("Gizmo")]
        [SerializeField]
        float radius = 0.3f;
        [SerializeField]
        Color color = Color.red;

        bool hasHit;
        Vector3 hitPoint;

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            // Skyboxにはコライダーが無いので、メッシュ（コライダー）に当たったときだけ更新される
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                hasHit = true;
                hitPoint = hit.point;
            }
        }

        void OnDrawGizmos()
        {
            if (!hasHit) return;
            Gizmos.color = color;
            Gizmos.DrawSphere(hitPoint, radius);
        }
    }
}
