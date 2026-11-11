using System.Collections.Generic;
using Common;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// Joy-Con（SwitchInput）での入力。コントローラー番号 = プレイヤー番号（0:1P 1:2P）
    ///
    /// ■ 操作
    ///   構え（アイドル・移動中） … Joy-Conの向きをそのまま剣に反映する（フルトラッキング）
    ///   攻撃 … 刃先の向きが swingTime 秒以内に swingAngle 度以上動いたら振ったとみなす
    ///          方向は、動き始めの刃先から今の刃先へ動いた向き（後ろから見て。上=0から時計回りの8方向）
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

        bool thrustArmed = true;
        float thrustBlockedUntil;  // この時刻までは突きを受け付けない（前以外に動かした後）
        float swingBlockedUntil;   // この時刻までは次の振りを受け付けない
        readonly Queue<(float time, Vector3 tip)> tipHistory = new();   // 刃先の向き（プレイヤー基準）の履歴。swingTime 秒分
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
                tipHistory.Clear();
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
            UpdateSwing(guarding);
            UpdateThrust(input, controller, controllerInPlayer, guarding);
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

        // 刃先の向きが swingTime 秒以内に swingAngle 度以上動いたら、動いた向きに1回振る
        void UpdateSwing(bool guarding)
        {
            float now = Time.time;
            Vector3 tip = swordRotation * Vector3.up;   // 刃先の向き（プレイヤー基準。x+右 y+上 z+前）
            while (tipHistory.Count > 0 && now - tipHistory.Peek().time > config.swingTime) tipHistory.Dequeue();
            tipHistory.Enqueue((now, tip));

            if (guarding || now < swingBlockedUntil) return;

            // 一番古い（大きく動いた）ものから順に、今の向きと swingAngle 度以上離れているものを探す
            foreach (var (_, from) in tipHistory)
            {
                if (Vector3.Angle(from, tip) < config.swingAngle) continue;

                // 動いた向き = 後ろから見た刃先の位置（x, y）の差
                Vector2 d = new Vector2(tip.x - from.x, tip.y - from.y);
                if (d.sqrMagnitude < 1e-4f) return;   // 前後にだけ動いた（後ろから見て動いていない）
                pendingSwing = AngleToDirection(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, Directions);
                pendingSwingTime = now;
                swingBlockedUntil = now + config.swingCooldown;
                thrustArmed = false;   // 振りの終わりの急停止で突きが出ないようにする
                tipHistory.Clear();
                return;
            }
        }

        // 回さずに前へ素早く動かしたら1回突く
        void UpdateThrust(SwitchInput input, Quaternion controller, Quaternion controllerInPlayer, bool guarding)
        {
            float accel = LinearAcceleration(input, controller, out Vector3 linear);
            LinearAccel = calibration * linear;   // プレイヤー基準
            if (accel <= config.thrustThreshold * config.thrustRearm && Time.time >= swingBlockedUntil && Time.time >= thrustBlockedUntil) thrustArmed = true;
            if (guarding || !thrustArmed || accel < config.thrustThreshold) return;

            // 正面の軸まわり（ひねり）を除いた角速度がほぼ0のときだけ
            Vector3 w = controllerInPlayer * input.AngularVelocity(playerIndex);   // プレイヤー基準の角速度
            if (new Vector2(w.x, w.y).magnitude > config.thrustMaxAngularSpeed) return;

            // しきい値を超えた最初の瞬間の向きで決める。前以外なら、その後の止める動き（逆向きの加速度）で出ないよう止める
            float forward = Vector3.Dot(LinearAccel, Vector3.forward) / accel * (config.invertThrustAccel ? -1f : 1f);
            if (forward < config.thrustForwardDot)   // 前へ突いていない（引いた・横に動かした）
            {
                thrustArmed = false;
                thrustBlockedUntil = Time.time + config.thrustLockout;
                return;
            }
            thrustArmed = false;
            pendingThrust = true;
            pendingThrustTime = Time.time;
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
