using UnityEngine;

namespace Pirates
{
    public class SpeedmeterNeedle : MonoBehaviour
    {
        [Header("針が回る角度")]
        [SerializeField, Range(0f, 360f)]
        float MaxAngle = 180f;

        [SerializeField]
        BoatController controller;

        float currentAngle = 0f;
        float speedRatio = 0f;

        void Update()
        {
            speedRatio =
                controller.ForwardSpeed /
                controller.MaxSpeed;

            speedRatio = Mathf.Clamp01(speedRatio);

            currentAngle =
                speedRatio * MaxAngle;

            transform.localRotation =
                Quaternion.Euler(0f, 0f, -currentAngle);
        }
    }
}
