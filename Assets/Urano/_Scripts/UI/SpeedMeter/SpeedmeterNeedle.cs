using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 針で現在の舵角を表示する。真っ直ぐで針は真上、最大舵角で左右に MaxAngle 度振れる。
    /// </summary>
    public class SpeedmeterNeedle : MonoBehaviour
    {
        [Header("最大舵角のときに針が振れる角度（左右それぞれ）")]
        [SerializeField]
        float MaxAngle = 720;

        [SerializeField]
        JoyConRudder rudder;

        void Update()
        {
            if (rudder == null) return;

            float angle = Mathf.Clamp(rudder.RudderNormalized, -1f, 1f) * MaxAngle;

            // 右舵(+)で時計回り
            transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
