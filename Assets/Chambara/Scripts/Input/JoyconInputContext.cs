using Common;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// Joy-Con（SwitchInput）での入力。コントローラー番号 = プレイヤー番号（0:1P 1:2P）
    ///
    /// ■ 操作
    ///   構え（アイドル・移動中） … Joy-Conの向きをそのまま剣に反映する（フルトラッキング）
    ///   攻撃 … 素早く振る。加速度（重力を除いた分）がしきい値を超えた瞬間に振ったとみなし、
    ///          方向はその瞬間の角速度（回した向き）から8方向で決める
    ///   ガード … ガードボタン(Confirm = ZR/ZL)を押しながら、刃を正面と垂直に構える
    ///            刃が水平 → 上 / 刃先が下向き → 下 / 刃先が上向きで右に傾ける → 右 / 左に傾ける → 左
    ///   突き … 回さずに前（相手の方）へ素早く突き出す。重力を除いた加速度がしきい値を超え、
    ///          その向きが前方で、角速度（ひねりを除く）がほぼ0（thrustMaxAngularSpeed 以下）のときに突いたとみなす
    ///   前進・後退 … スティック上下（上=前進 下=後退）
    ///   正面の合わせ直し … ZR/ZL の端を正面（画面）に向けて合わせ直しボタン(Cancel)。押すと剣先が正面を向く。最初に向きが取れたときは自動で合わせる
    ///
    /// 調整値は JoyconInputConfig（ScriptableObject）にまとめてある
    ///
    /// Joy-Conの向きが取れないとき（PCのキーボード・マウスなど）は、同じオブジェクトの DebugInputContext に任せる
    /// </summary>
    public class JoyconInputContext : MonoBehaviour, IChambaraInputContext, ISwordTracking
    {
        [SerializeField, Tooltip("調整値（1P・2P共通）。未設定ならスクリプトの初期値を使う")] JoyconInputConfig config;

        const int Directions = 8;   // 攻撃方向の数（上=0から時計回り）

        int playerIndex;
        IChambaraInputContext fallback;

        bool calibrated;
        Quaternion calibration;   // Joy-Conの向き → プレイヤー基準の向き（合わせ直しで決まる）
        Quaternion swordRotation; // 剣（pivot）のプレイヤー基準の回転
        bool tracking;            // 今フレーム向きが取れたか

        bool swingArmed = true;
        bool hasGravity;
        Vector3 gravity;          // Joy-Conの姿勢の座標（SDKの基準）での重力による加速度
        int pendingSwing = -1;
        float pendingSwingTime;
        bool pendingThrust;
        float pendingThrustTime;
        int pose = -1;
        bool front, back;

        // デバッグ表示用
        public int PlayerIndex => playerIndex;
        public bool IsUsingJoycon => tracking;           // false ならキーボード（DebugInputContext）
        public PlayerInput LastInput { get; private set; } = PlayerInput.None;   // 最後に Read で返した入力
        public int LastSwing { get; private set; } = -1;  // 最後に振った方向（-1:まだ振っていない）
        public float LastSwingTime { get; private set; }
        public float LastThrustTime { get; private set; } = -1f;   // 最後に突いた時刻（-1:まだ突いていない）
        public Vector3 LinearAccel { get; private set; }           // 重力を除いた加速度（プレイヤー基準。z+が前）

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogWarning($"{name}: JoyconInputContext の config が未設定なので初期値を使います");
                config = ScriptableObject.CreateInstance<JoyconInputConfig>();
            }
        }

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
            PlayerInput result;
            if (!tracking) result = fallback != null ? fallback.Read() : PlayerInput.None;
            else
            {
                int swing = pendingSwing >= 0 && Time.time - pendingSwingTime <= config.swingBuffer ? pendingSwing : -1;
                bool thrust = swing < 0 && pendingThrust && Time.time - pendingThrustTime <= config.swingBuffer;
                pendingSwing = -1;
                pendingThrust = false;
                result = new PlayerInput { pose = pose, swing = swing, thrust = thrust, isFront = front, isBack = back };
            }
            LastInput = result;
            if (result.swing >= 0)
            {
                LastSwing = result.swing;
                LastSwingTime = Time.time;
            }
            if (result.thrust) LastThrustTime = Time.time;
            return result;
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
                hasGravity = false;
                return;
            }

            Quaternion controller = Quaternion.LookRotation(z, y);   // x=右, y=上, z=前（SwitchInput.Angle と同じ）
            Quaternion grip = Quaternion.LookRotation(JoyconInputConfig.AxisVector(config.frontAxis), JoyconInputConfig.AxisVector(config.bladeAxis));   // 剣の座標 → Joy-Conの座標
            if (!calibrated || input.ButtonDown(playerIndex, config.recenterButton))
            {
                // 今の向きが calibrationAngle の構えになるようにする
                calibration = Quaternion.Euler(config.calibrationAngle, 0f, 0f) * Quaternion.Inverse(grip) * Quaternion.Inverse(controller);
                calibrated = true;
            }
            Quaternion controllerInPlayer = calibration * controller;   // プレイヤー基準のJoy-Conの向き
            swordRotation = controllerInPlayer * grip;

            UpdateMove(input);
            bool guarding = input.Button(playerIndex, config.guardButton);
            pose = guarding ? GuardDirection(swordRotation * Vector3.up) : -1;
            UpdateSwing(input, controller, controllerInPlayer, guarding);
        }

        void UpdateMove(SwitchInput input)
        {
            front = back = false;
            if (config.useStick)
            {
                float s = input.Stick(playerIndex).y;
                front |= s >= config.stickThreshold;
                back  |= s <= -config.stickThreshold;
            }
            // ボタンでの移動は使わない（スティックのみ）
            // if (useButtons)
            // {
            //     front |= input.Button(playerIndex, frontButton);
            //     back  |= input.Button(playerIndex, backButton);
            // }
        }

        // 重力を除いた加速度がしきい値を超えた瞬間に1回だけ振る（回していなければ、前へ動かしたときだけ突く）
        void UpdateSwing(SwitchInput input, Quaternion controller, Quaternion controllerInPlayer, bool guarding)
        {
            float accel = LinearAcceleration(input, controller, out Vector3 linear);
            LinearAccel = calibration * linear;   // プレイヤー基準
            if (accel <= Mathf.Min(config.swingThreshold, config.thrustThreshold) * config.swingRearm) swingArmed = true;
            if (guarding || !swingArmed) return;

            // 方向は角速度から。正面の軸まわり（ひねり）は無視し、回転軸と垂直な向きを振った方向にする
            // 例: 右の軸まわり（振り下ろし）→ 下、上の軸まわり → 右
            Vector3 w = controllerInPlayer * input.AngularVelocity(playerIndex);   // プレイヤー基準の角速度（度/秒）
            Vector3 d = Vector3.Cross(w, Vector3.forward);
            float angular = new Vector2(d.x, d.y).magnitude;
            if (angular < config.swingMinAngularSpeed)   // 振りになるほど回していない
            {
                if (angular > config.thrustMaxAngularSpeed) return;   // 突きは角速度がほぼ0のときだけ
                if (accel < config.thrustThreshold) return;
                float forward = Vector3.Dot(LinearAccel, Vector3.forward) / accel * (config.invertThrustAccel ? -1f : 1f);
                if (forward < config.thrustForwardDot) return;   // 前へ突いていない（引いた・横に動かした）
                swingArmed = false;
                pendingThrust = true;
                pendingThrustTime = Time.time;
                return;
            }
            if (accel < config.swingThreshold) return;
            swingArmed = false;
            pendingSwing = AngleToDirection(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, Directions);
            pendingSwingTime = Time.time;
        }

        // 重力を除いた加速度の大きさ（G）
        // 加速度はJoy-Conの座標なので姿勢で回して向きをそろえ、ほぼ静止しているときの値を重力として引く
        float LinearAcceleration(SwitchInput input, Quaternion controller, out Vector3 linear)
        {
            linear = Vector3.zero;
            Vector3 a = controller * input.Acceleration(playerIndex);
            if (a.sqrMagnitude < 1e-6f) return 0f;   // 値が来ていない
            if (!hasGravity)
            {
                gravity = a;
                hasGravity = true;
            }
            else if (Mathf.Abs(a.magnitude - 1f) < 0.1f)
            {
                gravity = Vector3.Lerp(gravity, a, 1f - Mathf.Exp(-config.gravityFollow * Time.deltaTime));
            }
            linear = a - gravity;
            return linear.magnitude;
        }

        // 刃の向き（プレイヤー基準）からガード方向（上=0 右=2 下=4 左=6）。構えになっていなければ -1
        int GuardDirection(Vector3 blade)
        {
            float toFront = Vector3.Angle(blade, Vector3.forward);
            if (Mathf.Abs(toFront - 90f) > config.perpendicularTolerance) return -1;

            float tip = Mathf.Atan2(blade.x, blade.y) * Mathf.Rad2Deg;   // 刃先の角度。真上=0、右回りで+
            float abs = Mathf.Abs(tip);
            if (abs >= 180f - config.verticalRange) return 4;   // 刃先が下向き
            if (abs > config.verticalRange) return 0;           // 水平
            if (abs < config.sideDeadZone) return pose == 2 || pose == 6 ? pose : -1;   // 真上付近は今の左右を保つ
            return tip > 0f ? 2 : 6;
        }

        static int AngleToDirection(float angle, int directions)
        {
            float step = 360f / directions;
            return ((Mathf.RoundToInt(angle / step) % directions) + directions) % directions;
        }
    }
}
