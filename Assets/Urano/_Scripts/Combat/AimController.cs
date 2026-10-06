using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates
{
    /// <summary>
    /// カーソル（のちにSwitchの角度）が指している先を毎フレーム求める。
    /// コライダー（敵船など）と水面（waterYの平面）のうち、手前にあるほうを採用する。
    /// 入力の取り方は TryGetAimRay を差し替えれば変えられる。
    /// </summary>
    public class AimController : MonoBehaviour
    {
        [Header("レイを飛ばすカメラ（未設定ならMainCamera）")]
        [SerializeField]
        Camera targetCamera;

        [Header("判定")]
        [SerializeField]
        [Tooltip("レイが当たる最大距離")]
        float maxDistance = 1000f;
        [SerializeField]
        [Tooltip("当たり判定を取るレイヤー（自分の船のレイヤーは外す）")]
        LayerMask layerMask = ~0;

        [Header("水面")]
        [SerializeField]
        [Tooltip("設定すると、そのwaterYの平面とレイを交差させる（海にコライダー不要）")]
        BoatBuoyancy water;

        /// <summary>照準が何かに当たっているか</summary>
        public bool HasAim { get; private set; }
        /// <summary>照準の指す位置（ワールド座標）</summary>
        public Vector3 AimPoint { get; private set; }
        /// <summary>照準がコライダーに当たっている場合そのコライダー（水面・何もないときはnull）</summary>
        public Collider AimCollider { get; private set; }

        void Update()
        {
            Refresh();
        }

        /// <summary>照準を最新の入力で更新する</summary>
        public void Refresh()
        {
            HasAim = false;
            AimCollider = null;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || !TryGetAimRay(cam, out Ray ray)) return;

            float nearest = float.MaxValue;

            // コライダー（Skyboxには無い）
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                nearest = hit.distance;
                AimPoint = hit.point;
                AimCollider = hit.collider;
                HasAim = true;
            }

            // 水面。コライダーより手前にある場合のみ採用
            if (water != null)
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, water.waterY, 0f));
                if (plane.Raycast(ray, out float enter) && enter <= maxDistance && enter < nearest)
                {
                    AimPoint = ray.GetPoint(enter);
                    AimCollider = null;
                    HasAim = true;
                }
            }
        }

        bool useExternalScreenPosition;
        Vector2 externalScreenPosition;

        /// <summary>画面上の座標（ピクセル）を外部から指定する。以後、マウスではなくこの座標を照準に使う</summary>
        public void SetScreenPosition(Vector2 screenPosition)
        {
            useExternalScreenPosition = true;
            externalScreenPosition = screenPosition;
        }

        /// <summary>照準レイの取得。既定はマウスカーソル。SetScreenPositionで座標が指定されていればそれを使う</summary>
        protected virtual bool TryGetAimRay(Camera cam, out Ray ray)
        {
            if (useExternalScreenPosition)
            {
                ray = cam.ScreenPointToRay(externalScreenPosition);
                return true;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                ray = default;
                return false;
            }
            ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            return true;
        }
    }
}
