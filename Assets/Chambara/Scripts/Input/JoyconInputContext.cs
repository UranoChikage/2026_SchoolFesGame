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
    ///   攻撃 … 刃先が swingStartSpeed（度/秒）以上の速さで動き出したら振り始め。そこから swingAngle 度以上動いたら振ったとみなす
    ///          ゆっくりした振りかぶりは振りにならない。途中で逆向きに切り返したら、そこから振り直しとして数える
    ///          方向は、振り始めの刃先から今の刃先へ動いた向き（後ろから見て。上=0から時計回りの8方向）
    ///          移動中・硬直中など行動を受け付けないときは出さない（受け付けるようになった時点でまだ振っていれば出る）
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
        // 振りの検出（刃先の向きはプレイヤー基準）
        bool hasPrevTip;
        Vector3 prevTip;          // 前のフレームの刃先の向き
        bool inStroke;            // 速く動かしている最中か
        bool strokeFired;         // この振りでもう攻撃を出したか
        float strokeStartTime;
        Vector3 strokeFrom;       // 振り始めの刃先の向き
        Vector2 strokeMove;       // 振り始めから後ろから見て動いた量
        int lastReadFrame = -10;  // 最後に Read されたフレーム（PlayerBehaviour が行動を受け付けているか）

        float poseSeen;           // 今の構えを最後に確認した時刻
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
            lastReadFrame = Time.frameCount;
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
                hasPrevTip = false;
                inStroke = false;
                return;
            }

            Quaternion controller = Quaternion.LookRotation(z, y);   // x=右, y=上, z=前（SwitchInput.Angle と同じ）
            Quaternion grip = Quaternion.LookRotation(JoyconInputConfig.AxisVector(config.frontAxis), JoyconInputConfig.AxisVector(config.bladeAxis));   // 剣の座標 → Joy-Conの座標
            if (!calibrated || input.ButtonDown(playerIndex, config.recenterButton))
            {
                // 今の向きが calibrationAngle の構えになるようにする
                calibration = Quaternion.Euler(config.calibrationAngle, 0f, 0f) * Quaternion.Inverse(grip) * Quaternion.Inverse(controller);
                calibrated = true;
                hasPrevTip = false;   // 向きが飛ぶので、振りとして数えない
                inStroke = false;
            }
            Quaternion controllerInPlayer = calibration * controller;   // プレイヤー基準のJoy-Conの向き
            swordRotation = controllerInPlayer * grip;

            UpdateMove(input);
            bool guarding = input.Button(playerIndex, config.guardButton);
            UpdateGuard(guarding);
            // 前のフレームか今のフレームに Read されていれば、行動を受け付けている（Update の順番はどちらでもよい）
            bool accepting = Time.frameCount - lastReadFrame <= 1;
            UpdateSwing(guarding, accepting);
            UpdateThrust(input, controller, controllerInPlayer, guarding);
        }

        // ガードの構え。境目で手がぶれても、guardGraceTime 秒続くまでは今の構えを保つ（ボタンを離したらすぐ解く）
        void UpdateGuard(bool guarding)
        {
            float now = Time.time;
            if (!guarding)
            {
                pose = -1;
                return;
            }
            int raw = GuardDirection(swordRotation * Vector3.up);
            if (raw == pose) poseSeen = now;
            else if (pose < 0 || now - poseSeen > config.guardGraceTime)
            {
                pose = raw;
                poseSeen = now;
            }
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

        // 刃先が速く動き出してから swingAngle 度以上動いたら、動いた向きに1回振る
        void UpdateSwing(bool guarding, bool accepting)
        {
            float now = Time.time;
            Vector3 tip = swordRotation * Vector3.up;   // 刃先の向き（プレイヤー基準。x+右 y+上 z+前）
            if (!hasPrevTip)
            {
                prevTip = tip;
                hasPrevTip = true;
                return;
            }

            // 刃先の速さ（度/秒）。スロー演出中も同じ速さで判定できるよう実時間で測る
            float dt = Time.unscaledDeltaTime;
            float speed = dt > 0f ? Vector3.Angle(prevTip, tip) / dt : 0f;
            Vector2 step = new Vector2(tip.x - prevTip.x, tip.y - prevTip.y);   // 後ろから見た今フレームの動き

            if (inStroke)
            {
                if (speed < config.swingEndSpeed || now - strokeStartTime > config.swingTime) inStroke = false;   // 止まった・長すぎる
                else if (strokeMove.sqrMagnitude > 1e-6f && step.sqrMagnitude > 1e-6f
                         && Vector2.Dot(step.normalized, strokeMove.normalized) < -0.3f)
                    StartStroke(prevTip, now);   // 逆向きに切り返した（振りかぶり→振り下ろし）
            }
            if (!inStroke && speed >= config.swingStartSpeed) StartStroke(prevTip, now);
            if (inStroke) strokeMove += step;
            prevTip = tip;

            if (!inStroke || strokeFired || guarding || !accepting || now < swingBlockedUntil) return;
            if (Vector3.Angle(strokeFrom, tip) < config.swingAngle) return;
            if (strokeMove.magnitude < config.swingMinScreenMove) return;   // ほぼ前後にだけ動いた

            pendingSwing = AngleToDirection(Mathf.Atan2(strokeMove.x, strokeMove.y) * Mathf.Rad2Deg, Directions);
            pendingSwingTime = now;
            swingBlockedUntil = now + config.swingCooldown;
            thrustArmed = false;   // 振りの終わりの急停止で突きが出ないようにする
            strokeFired = true;    // 同じ振りでは1回だけ
        }

        void StartStroke(Vector3 from, float now)
        {
            inStroke = true;
            strokeFired = false;
            strokeStartTime = now;
            strokeFrom = from;
            strokeMove = Vector2.zero;
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
            // 今の構えから抜けにくくなるよう、境目を guardHysteresis 度ずらす
            float h = config.guardHysteresis;
            float toFront = Vector3.Angle(blade, Vector3.forward);
            if (Mathf.Abs(toFront - 90f) > config.perpendicularTolerance + (pose >= 0 ? h : 0f)) return -1;

            float tip = Mathf.Atan2(blade.x, blade.y) * Mathf.Rad2Deg;   // 刃先の角度。真上=0、右回りで+
            float abs = Mathf.Abs(tip);
            float downEdge = 180f - config.verticalRange + (pose == 4 ? -h : pose == 0 ? h : 0f);   // これ以上なら下向き
            float upEdge = config.verticalRange + (pose == 0 ? -h : pose == 2 || pose == 6 ? h : 0f);  // これ以下なら上向き
            if (abs >= downEdge) return 4;   // 刃先が下向き
            if (abs > upEdge) return 0;      // 水平
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
