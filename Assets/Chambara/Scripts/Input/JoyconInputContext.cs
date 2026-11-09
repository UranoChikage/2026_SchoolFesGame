using Common;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// Joy-Con（SwitchInput）での入力。コントローラー番号 = プレイヤー番号（0:1P 1:2P）
    ///
    /// ■ 操作
    ///   構え（アイドル・移動中） … Joy-Conの向きをそのまま剣に反映する（フルトラッキング）
    ///   攻撃 … 素早く振る。角速度がしきい値を超えた瞬間に、回した向きから8方向を決める
    ///   ガード … ガードボタン(Confirm = ZR/ZL)を押しながら、刃を正面と垂直に構える
    ///            刃が水平 → 上 / 刃先が下向き → 下 / 刃先が上向きで右に傾ける → 右 / 左に傾ける → 左
    ///   前進・後退 … スティック上下（上=前進 下=後退）
    ///   正面の合わせ直し … 剣を calibrationAngle の構えにして合わせ直しボタン(Cancel)。最初に向きが取れたときは自動で合わせる
    ///
    /// Joy-Conの向きが取れないとき（PCのキーボード・マウスなど）は、同じオブジェクトの DebugInputContext に任せる
    /// </summary>
    public class JoyconInputContext : MonoBehaviour, IChambaraInputContext, ISwordTracking
    {
        public enum Axis { Right, Left, Up, Down, Forward, Back }

        [Header("持ち方（Joy-Conのどの軸がどちらを向くか。Joy-Con自身の座標）")]
        [SerializeField, Tooltip("刃が伸びる方向")] Axis bladeAxis = Axis.Forward;
        [SerializeField, Tooltip("剣を真上に立てたとき相手の方を向く面")] Axis frontAxis = Axis.Down;

        [Header("正面の合わせ直し")]
        [SerializeField] SwitchButton recenterButton = SwitchButton.Cancel;
        [SerializeField, Tooltip("合わせ直すときに構える剣の角度（真上=0、正面=90）")] float calibrationAngle = 0f;

        [Header("攻撃")]
        [SerializeField, Tooltip("この角速度（度/秒）を超えたら振ったとみなす")] float swingThreshold = 400f;
        [SerializeField, Range(0.1f, 1f), Tooltip("角速度がしきい値のこの割合まで下がったら次の振りを受け付ける")] float swingRearm = 0.5f;
        [SerializeField, Tooltip("振った入力を覚えておく時間（秒）。硬直中に振っても後から出ないようにする")] float swingBuffer = 0.1f;

        [Header("ガード")]
        [SerializeField] SwitchButton guardButton = SwitchButton.Confirm;   // Joy-Con では ZR/ZL
        [SerializeField, Tooltip("刃と正面のなす角が 90度±この角度 ならガードの構え")] float perpendicularTolerance = 30f;
        [SerializeField, Tooltip("刃先の角度（真上=0）がこれ以内なら上向き、180-これ以上なら下向き。間は水平")] float verticalRange = 45f;
        [SerializeField, Tooltip("上向きのとき、真上からこの角度以上傾けないと左右が決まらない")] float sideDeadZone = 5f;

        [Header("移動")]
        [SerializeField] bool useStick = true;
        [SerializeField, Tooltip("スティックをこれ以上倒したら前進・後退")] float stickThreshold = 0.5f;
        // ボタンでの移動は使わない（スティックのみ）
        // [SerializeField] bool useButtons = true;
        // [SerializeField] SwitchButton frontButton = SwitchButton.Up;
        // [SerializeField] SwitchButton backButton = SwitchButton.Down;

        const int Directions = 8;   // 攻撃方向の数（上=0から時計回り）

        int playerIndex;
        IChambaraInputContext fallback;

        bool calibrated;
        Quaternion calibration;   // Joy-Conの向き → プレイヤー基準の向き（合わせ直しで決まる）
        Quaternion swordRotation; // 剣（pivot）のプレイヤー基準の回転
        bool tracking;            // 今フレーム向きが取れたか

        bool swingArmed = true;
        int pendingSwing = -1;
        float pendingSwingTime;
        int pose = -1;
        bool front, back;

        public void Init(int playerIndex)
        {
            this.playerIndex = playerIndex;
            foreach (var c in GetComponents<IChambaraInputContext>())
            {
                if (!ReferenceEquals(c, this)) { fallback = c; break; }
            }
            fallback?.Init(playerIndex);
        }

        public PlayerInput Read()
        {
            if (!tracking) return fallback != null ? fallback.Read() : PlayerInput.None;

            int swing = pendingSwing >= 0 && Time.time - pendingSwingTime <= swingBuffer ? pendingSwing : -1;
            pendingSwing = -1;
            return new PlayerInput { pose = pose, swing = swing, isFront = front, isBack = back };
        }

        public bool TryGetSwordRotation(out Quaternion rotation)
        {
            rotation = swordRotation;
            return tracking;
        }

        private void Update()
        {
            var input = SwitchInput.I;
            tracking = input.TryGetDirection(playerIndex, out _, out var y, out var z)
                    && Vector3.Cross(z, y).sqrMagnitude > 1e-6f;
            if (!tracking)
            {
                calibrated = false;   // 繋ぎ直したら合わせ直す
                return;
            }

            Quaternion controller = Quaternion.LookRotation(z, y);   // x=右, y=上, z=前（SwitchInput.Angle と同じ）
            Quaternion grip = Quaternion.LookRotation(AxisVector(frontAxis), AxisVector(bladeAxis));   // 剣の座標 → Joy-Conの座標
            if (!calibrated || input.ButtonDown(playerIndex, recenterButton))
            {
                // 今の向きが calibrationAngle の構えになるようにする
                calibration = Quaternion.Euler(calibrationAngle, 0f, 0f) * Quaternion.Inverse(grip) * Quaternion.Inverse(controller);
                calibrated = true;
            }
            Quaternion controllerInPlayer = calibration * controller;   // プレイヤー基準のJoy-Conの向き
            swordRotation = controllerInPlayer * grip;

            UpdateMove(input);
            bool guarding = input.Button(playerIndex, guardButton);
            pose = guarding ? GuardDirection(swordRotation * Vector3.up) : -1;
            UpdateSwing(input, controllerInPlayer, guarding);
        }

        void UpdateMove(SwitchInput input)
        {
            front = back = false;
            if (useStick)
            {
                float s = input.Stick(playerIndex).y;
                front |= s >= stickThreshold;
                back  |= s <= -stickThreshold;
            }
            // ボタンでの移動は使わない（スティックのみ）
            // if (useButtons)
            // {
            //     front |= input.Button(playerIndex, frontButton);
            //     back  |= input.Button(playerIndex, backButton);
            // }
        }

        // 角速度がしきい値を超えた瞬間に1回だけ振る
        void UpdateSwing(SwitchInput input, Quaternion controllerInPlayer, bool guarding)
        {
            Vector3 w = controllerInPlayer * input.AngularVelocity(playerIndex);   // プレイヤー基準の角速度（度/秒）
            float speed = w.magnitude;
            if (speed <= swingThreshold * swingRearm) swingArmed = true;
            if (guarding || !swingArmed || speed < swingThreshold) return;

            // 正面の軸まわり（ひねり）は無視し、回転軸と垂直な向きを振った方向にする
            // 例: 右の軸まわり（振り下ろし）→ 下、上の軸まわり → 右
            Vector3 d = Vector3.Cross(w, Vector3.forward);
            if (new Vector2(d.x, d.y).sqrMagnitude < 1e-6f) return;   // ひねっただけ
            swingArmed = false;
            pendingSwing = AngleToDirection(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, Directions);
            pendingSwingTime = Time.time;
        }

        // 刃の向き（プレイヤー基準）からガード方向（上=0 右=2 下=4 左=6）。構えになっていなければ -1
        int GuardDirection(Vector3 blade)
        {
            float toFront = Vector3.Angle(blade, Vector3.forward);
            if (Mathf.Abs(toFront - 90f) > perpendicularTolerance) return -1;

            float tip = Mathf.Atan2(blade.x, blade.y) * Mathf.Rad2Deg;   // 刃先の角度。真上=0、右回りで+
            float abs = Mathf.Abs(tip);
            if (abs >= 180f - verticalRange) return 4;   // 刃先が下向き
            if (abs > verticalRange) return 0;           // 水平
            if (abs < sideDeadZone) return pose == 2 || pose == 6 ? pose : -1;   // 真上付近は今の左右を保つ
            return tip > 0f ? 2 : 6;
        }

        static int AngleToDirection(float angle, int directions)
        {
            float step = 360f / directions;
            return ((Mathf.RoundToInt(angle / step) % directions) + directions) % directions;
        }

        static Vector3 AxisVector(Axis a) => a switch
        {
            Axis.Right   => Vector3.right,
            Axis.Left    => Vector3.left,
            Axis.Up      => Vector3.up,
            Axis.Down    => Vector3.down,
            Axis.Forward => Vector3.forward,
            _            => Vector3.back,
        };

        private void OnValidate()
        {
            if (Mathf.Abs(Vector3.Dot(AxisVector(bladeAxis), AxisVector(frontAxis))) > 0.5f)
                Debug.LogWarning("JoyconInputContext: bladeAxis と frontAxis は直交する軸にしてください");
        }
    }
}
