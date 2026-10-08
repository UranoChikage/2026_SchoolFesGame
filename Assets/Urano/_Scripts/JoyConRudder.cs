using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates
{
    public class JoyConRudder : MonoBehaviour
    {
        private List<Joycon> joycons;

        [Header("デバッグ表示（読み取り専用）")]
        public float[] stick;
        public Vector3 gyro;
        public Vector3 accel;

        [Header("PCデバッグ")]
        [Tooltip("ONでJoyConがあってもキーボード(A/D・←/→で舵輪を回す、Spaceでセンター)で操作。JoyConが無い場合は自動でキーボードになる")]
        public bool forceKeyboard = false;

        [Header("JoyCon設定")]
        [Tooltip("使用するJoyConのインデックス")]
        public int jc_ind = 0;

        public enum Axis { X, Y, Z }

        [Header("軸の割り当て")]
        [Tooltip("中立姿勢で重力がかかっている軸（|accel| が最大の軸）")]
        public Axis downAxis = Axis.Z;
        [Tooltip("左右に倒したとき値が変化する軸")]
        public Axis steerAxis = Axis.Y;
        [Tooltip("左右が逆ならON")]
        public bool invert = false;

        [Header("舵輪（ハンドル）")]
        [Tooltip("舵輪が左右にどこまで回るか（度）。540なら片側1.5回転で舵が最大")]
        public float wheelLockDeg = 540f;
        [Tooltip("キーボード操作時の舵輪の回転速度（度/秒）。実物同様、離してもその角度のまま")]
        public float keyboardWheelSpeed = 180f;
        [Tooltip("キーボード操作で、キーを離したら舵輪が中央へ戻る。曲がり続けにくく扱いやすい（JoyCon操作には影響しない）")]
        public bool keyboardAutoCenter = true;
        [Tooltip("中央へ戻る速さ（度/秒）")]
        public float keyboardCenterSpeed = 240f;
        [Tooltip("舵輪の見た目（任意）。ローカルZ軸まわりに回す")]
        public Transform wheelVisual;

        [Header("舵の挙動")]
        [Tooltip("舵の最大角度（左右）")]
        public float maxRudderDeg = 30f;
        [Tooltip("追従の速さ。大きいほど機敏、小さいほど重い")]
        public float smoothing = 8f;

        [Header("出力（読み取り専用）")]
        [Tooltip("舵輪の累積回転角（度）。複数回転ぶん保持。+で右回り")]
        public float wheelAngle;
        [Tooltip("現在の舵角（度）")]
        public float rudderAngle;
        [Tooltip("正規化済み舵角（-1〜+1）")]
        public float rudderNormalized;

        // 外部参照用プロパティ
        public float WheelAngle => wheelAngle;
        public float RudderAngle => rudderAngle;
        public float RudderNormalized => rudderNormalized;
        public bool IsConnected => UsingKeyboard || JoyconAvailable;

        bool JoyconAvailable => joycons != null && joycons.Count > jc_ind;
        bool UsingKeyboard => forceKeyboard || !JoyconAvailable;

        private float smoothed;
        private float rawTilt;
        private float prevRawTilt;
        private bool hasPrevTilt;

        void Start()
        {
            gyro = Vector3.zero;
            accel = Vector3.zero;
            joycons = JoyconManager.Instance != null ? JoyconManager.Instance.j : null;
        }

        void Update()
        {
            if (UsingKeyboard)
            {
                hasPrevTilt = false;
                float dir = KeyboardDir();
                if (dir != 0f)
                {
                    wheelAngle += dir * keyboardWheelSpeed * Time.deltaTime;
                }
                else if (keyboardAutoCenter)
                {
                    wheelAngle = Mathf.MoveTowards(wheelAngle, 0f, keyboardCenterSpeed * Time.deltaTime);
                }
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) wheelAngle = 0f; // センター戻し
            }
            else
            {
                Joycon j = joycons[jc_ind];

                gyro = j.GetGyro();
                accel = j.GetAccel();
                stick = j.GetStick();

                float down = Pick(accel, downAxis);
                float steer = Pick(accel, steerAxis);

                // 舵輪にJoyConを固定すると、重力ベクトルが舵輪の面内で回る。その角度が舵輪の回転角。
                rawTilt = Mathf.Atan2(steer, -down) * Mathf.Rad2Deg * (invert ? -1f : 1f);

                // ±180°で折り返すので、フレーム間の差分を足して複数回転ぶんを累積する
                if (hasPrevTilt) wheelAngle += Mathf.DeltaAngle(prevRawTilt, rawTilt);
                prevRawTilt = rawTilt;
                hasPrevTilt = true;

                // 十字キー下で現在の舵輪位置を「真っ直ぐ」に再設定
                if (j.GetButtonDown(Joycon.Button.DPAD_DOWN)) wheelAngle = 0f;
            }

            wheelAngle = Mathf.Clamp(wheelAngle, -wheelLockDeg, wheelLockDeg);

            // 舵輪の回転量 → 舵角（実物の舵は舵輪より小さい角度しか動かない）
            float target = wheelLockDeg > 0 ? wheelAngle / wheelLockDeg * maxRudderDeg : 0f;
            smoothed = Mathf.Lerp(smoothed, target, Time.deltaTime * smoothing);
            rudderAngle = smoothed;
            rudderNormalized = maxRudderDeg > 0 ? Mathf.Clamp(rudderAngle / maxRudderDeg, -1f, 1f) : 0f;

            if (wheelVisual != null) wheelVisual.localRotation = Quaternion.Euler(0f, 0f, -wheelAngle);
        }

        // A/← で左回し、D/→ で右回し
        static float KeyboardDir()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            float dir = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += 1f;
            return dir;
        }

        static float Pick(Vector3 v, Axis a) => a == Axis.X ? v.x : a == Axis.Y ? v.y : v.z;

        void OnDrawGizmos()
        {
            Vector3 origin = transform.position;
            float len = 1.5f;

            // 中立基準線（白）
            Gizmos.color = Color.white;
            Gizmos.DrawLine(origin, origin + Vector3.forward * len);

            // 現在の舵角（緑＝右、赤＝左）
            Quaternion rot = Quaternion.Euler(0, rudderAngle, 0);
            Vector3 dir = rot * Vector3.forward;
            Gizmos.color = rudderAngle >= 0 ? Color.green : Color.red;
            Gizmos.DrawLine(origin, origin + dir * len);
            Gizmos.DrawSphere(origin + dir * len, 0.08f);

            // 最大舵角の扇（黄）
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Vector3 lMax = Quaternion.Euler(0, -maxRudderDeg, 0) * Vector3.forward * len;
            Vector3 rMax = Quaternion.Euler(0, maxRudderDeg, 0) * Vector3.forward * len;
            Gizmos.DrawLine(origin, origin + lMax);
            Gizmos.DrawLine(origin, origin + rMax);

    #if UNITY_EDITOR
            UnityEditor.Handles.Label(origin + Vector3.up * 0.2f,
                $"wheel: {wheelAngle:F0}°  rudder: {rudderAngle:F1}°  raw: {rawTilt:F1}°\n" +
                $"accel: ({accel.x:F2},{accel.y:F2},{accel.z:F2})");
    #endif
        }
    }
}
