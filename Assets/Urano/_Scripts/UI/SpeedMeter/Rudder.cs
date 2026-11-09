using UnityEngine;

namespace Pirates
{
    /// <summary>
    /// 3Dモデルの舵輪を現在の舵角に合わせてZ軸回転させる。真っ直ぐで初期角度、最大舵角で左右に MaxAngle 度回る。
    /// </summary>
    public class Rudder : MonoBehaviour
    {
        [Header("最大舵角のときに回る角度（左右それぞれ）")]
        [SerializeField]
        float MaxAngle = 720;

        [SerializeField]
        JoyConRudder rudder;

        Quaternion initialRotation;

        void Awake()
        {
            initialRotation = transform.localRotation;
        }

        void Update()
        {
            if (rudder == null) return;

            float angle = Mathf.Clamp(rudder.RudderNormalized, -1f, 1f) * MaxAngle;

            // 右舵(+)で時計回り。ローカルZ軸まわりに初期姿勢からの回転を加える
            transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
