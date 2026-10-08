using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Pirates
{
    /// <summary>
    /// 1人の砲手ぶんの照準と発射。砲は見た目だけで、画面上のカーソルが指す位置へ砲弾を飛ばす（Wiiのポインタ風）。
    /// カーソルの動かし方：JoyConの傾き／（JoyCon無し）マウスまたはキーボード。
    /// 1画面を全員で共有するので、砲手ごとに色付きのカーソルを画面に描く。
    /// </summary>
    [RequireComponent(typeof(CannonShooter))]
    [RequireComponent(typeof(AimController))]
    public class CannonStation : MonoBehaviour
    {
        [Header("プレイヤー")]
        [SerializeField]
        [Tooltip("使用するJoyConのインデックス（舵取りの1Pが0なら、砲手は1,2,3）")]
        int joyconIndex = 1;
        [SerializeField]
        [Tooltip("ONでJoyConがあってもマウスで操作（PCデバッグ）。JoyConが無いときは自動でマウスになる")]
        [FormerlySerializedAs("forceKeyboard")]
        bool forceMouse = false;
        [SerializeField]
        [Tooltip("砲手番号(1〜9)。PC代替操作中は、この番号の数字キーを押した砲だけが操作される")]
        int stationNumber = 1;

        [Header("砲身（見た目）")]
        [SerializeField]
        [Tooltip("砲身の見た目（任意）。狙う位置へ向ける")]
        Transform barrel;

        [Header("JoyCon（傾きでカーソルを動かす）")]
        [SerializeField]
        [Tooltip("中立姿勢で重力がかかっている軸")]
        JoyConRudder.Axis downAxis = JoyConRudder.Axis.Z;
        [SerializeField]
        [Tooltip("左右に傾けたとき変化する軸（カーソルのX）")]
        JoyConRudder.Axis xAxis = JoyConRudder.Axis.Y;
        [SerializeField]
        [Tooltip("前後に傾けたとき変化する軸（カーソルのY）")]
        JoyConRudder.Axis yAxis = JoyConRudder.Axis.X;
        [SerializeField]
        bool invertX = false;
        [SerializeField]
        bool invertY = false;
        [SerializeField]
        [Tooltip("この角度(度)傾けると、カーソルが画面端に届く")]
        float tiltRange = 30f;
        [SerializeField]
        Joycon.Button fireButton = Joycon.Button.SHOULDER_2;

        [Header("カーソル表示")]
        [SerializeField]
        Color cursorColor = Color.red;
        [SerializeField]
        [Tooltip("カーソルの画像（任意）。未設定なら十字を描く。VFX/Textures/Crosshair.png など")]
        Texture2D cursorTexture;
        [SerializeField]
        float cursorSize = 40f;
        [SerializeField]
        [Tooltip("JoyConの傾きに対するカーソルの追従の速さ")]
        float smoothing = 12f;

        // PC代替操作で現在操作している砲手番号（全砲で共有）
        static int activeStationNumber = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => activeStationNumber = 1;

        CannonShooter shooter;
        AimController aim;
        TrajectoryLine trajectory;
        Vector2 cursor;
        string status = "";
        Quaternion barrelRotInStation = Quaternion.identity; // 砲身の初期姿勢（この砲を基準）

        void Awake()
        {
            shooter = GetComponent<CannonShooter>();
            aim = GetComponent<AimController>();
            trajectory = GetComponent<TrajectoryLine>(); // 付いていれば予測線を出す（任意）
            if (trajectory != null) trajectory.SetColor(cursorColor);
            if (barrel != null) barrelRotInStation = Quaternion.Inverse(transform.rotation) * barrel.rotation;
        }

        void Start()
        {
            // 砲手ごとに初期位置をずらして重ならないようにする
            float x = Screen.width * (0.25f + 0.25f * ((Mathf.Max(1, stationNumber) - 1) % 3));
            cursor = new Vector2(x, Screen.height * 0.5f);
        }

        void Update()
        {
            SelectByNumberKey();

            Joycon jc = forceMouse ? null : FindJoycon();
            bool fire;

            if (jc != null)
            {
                status = "JOYCON";
                fire = UpdateJoycon(jc);
            }
            else if (activeStationNumber != stationNumber)
            {
                status = "waiting"; // 選ばれていない砲はPC操作を受け付けない
                fire = false;
            }
            else
            {
                status = "MOUSE";
                fire = UpdateMouse();
            }

            cursor.x = Mathf.Clamp(cursor.x, 0f, Screen.width);
            cursor.y = Mathf.Clamp(cursor.y, 0f, Screen.height);

            aim.SetScreenPosition(cursor);
            aim.Refresh();

            if (aim.HasAim)
            {
                UpdateBarrel(aim.AimPoint);
                if (fire) shooter.TryFireAt(aim.AimPoint);
            }

            UpdateTrajectory(status != "waiting");
        }

        // 予測線：選ばれている砲だけ、カーソルが指す位置へ撃った弾道を表示する
        void UpdateTrajectory(bool active)
        {
            if (trajectory == null) return;

            if (active && aim.HasAim && shooter.TryGetLaunchVelocity(aim.AimPoint, out Vector3 velocity))
            {
                trajectory.Show(shooter.MuzzlePosition, velocity, aim.AimPoint.y, shooter.IsReady);
            }
            else
            {
                trajectory.Hide();
            }
        }

        // 数字キー（上段／テンキー）で操作する砲手を切り替える
        void SelectByNumberKey()
        {
            var kb = Keyboard.current;
            if (kb == null || stationNumber < 1 || stationNumber > 9) return;

            Key digit = Key.Digit1 + (stationNumber - 1);
            Key numpad = Key.Numpad1 + (stationNumber - 1);
            if (kb[digit].wasPressedThisFrame || kb[numpad].wasPressedThisFrame)
            {
                activeStationNumber = stationNumber;
            }
        }

        Joycon FindJoycon()
        {
            var manager = JoyconManager.Instance;
            if (manager == null || manager.j == null || manager.j.Count <= joyconIndex) return null;
            return manager.j[joyconIndex];
        }

        bool UpdateJoycon(Joycon jc)
        {
            Vector3 accel = jc.GetAccel();
            float down = Pick(accel, downAxis);

            // 重力ベクトルの傾き角（JoyConRudderと同じ考え方）
            float tiltX = Mathf.Atan2(Pick(accel, xAxis), -down) * Mathf.Rad2Deg * (invertX ? -1f : 1f);
            float tiltY = Mathf.Atan2(Pick(accel, yAxis), -down) * Mathf.Rad2Deg * (invertY ? -1f : 1f);

            Vector2 target = new Vector2(
                (Mathf.Clamp(tiltX / tiltRange, -1f, 1f) * 0.5f + 0.5f) * Screen.width,
                (Mathf.Clamp(tiltY / tiltRange, -1f, 1f) * 0.5f + 0.5f) * Screen.height);

            cursor = Vector2.Lerp(cursor, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            return jc.GetButtonDown(fireButton);
        }

        bool UpdateMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            cursor = mouse.position.ReadValue();
            return mouse.leftButton.wasPressedThisFrame;
        }

        // 砲身を狙う位置へ水平方向（Y軸回転のみ）に向ける（見た目だけ）。
        // 初期の傾き（X/Z）は保持し、Y軸まわりに回した分だけを足す。
        void UpdateBarrel(Vector3 point)
        {
            if (barrel == null) return;

            Vector3 to = point - barrel.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;

            // 初期姿勢を、船の動きに追従させたワールド姿勢
            Quaternion baseRot = transform.rotation * barrelRotInStation;
            Vector3 baseForward = baseRot * Vector3.forward;
            baseForward.y = 0f;
            if (baseForward.sqrMagnitude < 0.0001f) return;

            float yaw = Vector3.SignedAngle(baseForward, to, Vector3.up);
            barrel.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRot;
        }

        static float Pick(Vector3 v, JoyConRudder.Axis a) => a == JoyConRudder.Axis.X ? v.x : a == JoyConRudder.Axis.Y ? v.y : v.z;

        // 砲手ごとの色付きカーソル（クールタイム中は小さく）
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            float size = cursorSize * (shooter.IsReady ? 1f : 0.6f);
            float x = cursor.x;
            float y = Screen.height - cursor.y; // GUIは上原点

            Color old = GUI.color;
            GUI.color = cursorColor;
            if (cursorTexture != null)
            {
                GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), cursorTexture);
            }
            else
            {
                GUI.DrawTexture(new Rect(x - size * 0.5f, y - 2f, size, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - 2f, y - size * 0.5f, 4f, size), Texture2D.whiteTexture);
            }
            // 番号と状態（デバッグ表示）。aim:NG は照準がどこにも当たっていない（Water未設定など）
            GUI.Label(new Rect(x + size * 0.5f + 4f, y - 12f, 200f, 24f),
                $"{stationNumber} {status} aim:{(aim.HasAim ? "OK" : "NG")}");
            GUI.color = old;
        }
    }
}
