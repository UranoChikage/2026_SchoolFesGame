using Common;
using UnityEngine;

namespace Chambara
{
    /// <summary>
    /// Joy-Con（SwitchInput）での入力。コントローラー番号 = プレイヤー番号（0:1P 1:2P）
    ///
    /// ■ 操作
    ///   構え（アイドル・移動中） … Joy-Conの向きをそのまま剣に反映する（フルトラッキング）
    ///   攻撃 … 剣を速く振る。方向は「後ろから見て剣がどちらへ回ったか」（上=0から時計回りの8方向）
    ///   ガード … ガードボタン(Confirm = ZR/ZL)を押しながら、刃を正面と垂直に構える
    ///            刃が水平 → 上 / 刃先が下向き → 下 / 刃先が上向きで右に傾ける → 右 / 左に傾ける → 左
    ///   突き … 剣を回さずに前（相手の方）へ素早く突き出す
    ///   前進・後退 … スティック上下（上=前進 下=後退）
    ///   正面の合わせ直し … ZR/ZL の端を正面（画面）に向けて合わせ直しボタン(Cancel)。押すと剣先が正面を向く。最初に向きが取れたときは自動で合わせる
    ///
    /// ■ 判定のしくみ（動きはすべて姿勢の変化から求める。SDKの角速度の単位には頼らない）
    ///   振り … 刃先が swingStartSpeed（度/秒）以上で動き出したら振り始め。
    ///          振り始めから swingAngle 度以上回り、かつ一番速いときに swingMinPeakSpeed を超えていたら振ったとみなす
    ///          ・ゆっくりした振りかぶりは速さが足りないので振りにならない
    ///          ・途中で逆向きに切り返したら、そこから振り直しとして数える（振りかぶり→振り下ろし）
    ///          ・方向は刃先の位置ではなく回転の向きで決めるので、頭の後ろまで振りかぶってから振り下ろしても「下」になる
    ///          ・振った直後（swingCooldown 秒）に始まった動きは、構えに戻す動きとみなして振りにしない
    ///   突き … 重力を除いた加速度を短い時間（thrustWindow 秒）ためて「前へ出た勢い」を求め、thrustImpulse を超えたら突いたとみなす
    ///          ・瞬間の加速度ではなく勢いで見るので、手を引いて止めた反動や、突き終わりの急停止では出ない
    ///          ・剣を回している間は遠心力が混ざるので数えない
    ///   ガード … 境目で手がぶれても構えが切り替わらないよう、角度のヒステリシスと猶予時間を入れてある
    ///   振り・突きは swingBuffer 秒だけ覚えておき、硬直・移動が明けた直後なら出る（先行入力）
    ///
    /// 調整値は JoyconInputConfig（ScriptableObject）にまとめてある。ControllerDebugView で実際の値を見ながら調整できる
    ///
    /// Joy-Conの向きが取れないとき（PCのキーボード・マウスなど）は、同じオブジェクトの DebugInputContext に任せる
    /// </summary>
    public class JoyconInputContext : MonoBehaviour, IChambaraInputContext, ISwordTracking
    {
        [SerializeField, Tooltip("調整値（1P・2P共通）。未設定ならスクリプトの初期値を使う")] JoyconInputConfig config;

        const int Directions = 8;             // 攻撃方向の数（上=0から時計回り）
        const float MaxFrameTime = 0.1f;      // 1フレームがこれより長いときは動きを測り直す（カクついた分を振りと数えない）
        const float StrokeEndGrace = 0.05f;   // 遅い状態がこの時間（秒）続いたら振り終わり（1フレームだけ値が止まっても切らない）
        const float ReverseDot = -0.3f;       // 今の動きと振り全体の向きの cos がこれ未満なら切り返し
        const float ReverseMinTravel = 5f;    // これ（度）以上振ってからでないと切り返しを見ない（向きが定まらないため）
        const float DepthFloor = 0.5f;        // 剣が真上・真横を向いていても、前後に倒す回転をこの割合で振りの向きに数える
        const float ThrustRearm = 0.3f;       // 勢いが thrustImpulse のこの割合まで下がったら次の突きを受け付ける
        const float StillTipSpeed = 90f;      // 刃先がこれ（度/秒）より遅いときだけ重力を推定し直す

        int playerIndex;
        IChambaraInputContext fallback;

        // 姿勢
        bool tracking;            // 今フレーム向きが取れたか
        bool calibrated;
        Quaternion calibration;   // Joy-Conの向き → プレイヤー基準の向き（合わせ直しで決まる）
        Quaternion swordRotation; // 剣（pivot）のプレイヤー基準の回転

        // 動き（プレイヤー基準。x+右 y+上 z+前）
        bool hasPrev;
        Quaternion prevRotation;
        Vector3 prevTip;          // 前のフレームの刃先の向き

        // 振り
        bool inStroke;            // 速く動かしている最中か
        bool strokeCanFire;       // この振りで攻撃を出してよいか（振った直後に始まった動きは出さない）
        bool strokeFired;         // この振りでもう攻撃を出したか
        float strokeStartTime;
        float strokeFastTime;     // 最後に swingEndSpeed 以上だった時刻
        Vector2 strokeMove;       // 振り始めから、後ろから見て回った量（度）
        float strokePeak;         // この振りで一番速かったときの刃先の速さ（度/秒）
        float swingBlockedUntil;  // この時刻までに始まった動きは振りにしない

        // 突き
        bool hasGravity;
        Vector3 gravity;          // Joy-Conの姿勢の座標（SDKの基準）での重力による加速度
        Vector3 thrustVelocity;   // 重力を除いた加速度をためた勢い（G・秒。プレイヤー基準）
        bool thrustArmed = true;
        float thrustBlockedUntil; // この時刻までは突きを受け付けない（振った後）
        float thrustPeakTime;

        // ガード
        int pose = -1;
        float poseSeen;           // 今の構えを最後に確認した時刻

        // Read で渡す入力
        int pendingSwing = -1;
        float pendingSwingTime;
        bool pendingThrust;
        float pendingThrustTime;
        bool front, back;

        // デバッグ表示用
        public int PlayerIndex => playerIndex;
        public bool IsUsingJoycon => tracking;           // false ならキーボード（DebugInputContext）
        public PlayerInput LastInput { get; private set; } = PlayerInput.None;   // 最後に Read で返した入力
        public int LastSwing { get; private set; } = -1;  // 最後に振った方向（-1:まだ振っていない）
        public float LastSwingTime { get; private set; }
        public float LastThrustTime { get; private set; } = -1f;   // 最後に突いた時刻（-1:まだ突いていない）
        public Vector3 LinearAccel { get; private set; }           // 重力を除いた加速度（プレイヤー基準。z+が前）
        public float TipSpeed { get; private set; }                // 刃先の速さ（度/秒）
        public bool InStroke => inStroke;
        public float StrokePeakSpeed => strokePeak;                // 今の（最後の）振りで一番速かったときの速さ。swingMinPeakSpeed と比べる
        public float StrokeTravel => strokeMove.magnitude;         // 今の（最後の）振りで回った量。swingAngle と比べる
        public float ThrustImpulse { get; private set; }           // 前へ出た勢い。thrustImpulse と比べる
        public float ThrustImpulsePeak { get; private set; }       // 直近1秒で一番大きかった勢い

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
                float now = Time.unscaledTime;
                int swing = pendingSwing >= 0 && now - pendingSwingTime <= config.swingBuffer ? pendingSwing : -1;
                bool thrust = swing < 0 && pendingThrust && now - pendingThrustTime <= config.swingBuffer;
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
                pose = -1;
                ResetMotion();
                return;
            }

            Quaternion controller = Quaternion.LookRotation(z, y);   // x=右, y=上, z=前（SwitchInput.Angle と同じ）
            Quaternion grip = Quaternion.LookRotation(JoyconInputConfig.AxisVector(config.frontAxis), JoyconInputConfig.AxisVector(config.bladeAxis));   // 剣の座標 → Joy-Conの座標
            if (!calibrated || input.ButtonDown(playerIndex, config.recenterButton))
            {
                // 今の向きが calibrationAngle の構えになるようにする
                calibration = Quaternion.Euler(config.calibrationAngle, 0f, 0f) * Quaternion.Inverse(grip) * Quaternion.Inverse(controller);
                calibrated = true;
                ResetMotion();   // 向きが飛ぶので、振りとして数えない
            }
            swordRotation = calibration * controller * grip;

            // スロー・ヒットストップ中も同じように判定できるよう、動きは実時間で測る
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            bool guarding = input.Button(playerIndex, config.guardButton);
            UpdateMove(input);
            UpdateGuard(guarding, now);

            Vector3 tip = swordRotation * Vector3.up;   // 刃先の向き
            if (dt <= 1e-5f) return;
            if (!hasPrev || dt > MaxFrameTime)
            {
                ResetMotion();
                prevRotation = swordRotation;
                prevTip = tip;
                hasPrev = true;
                return;
            }

            // 前のフレームからの回転 → 角速度（度/秒）
            Quaternion delta = swordRotation * Quaternion.Inverse(prevRotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 omega = float.IsNaN(axis.x) || float.IsInfinity(axis.x) ? Vector3.zero : axis * (angle / dt);
            TipSpeed = Vector3.Angle(prevTip, tip) / dt;   // 刃の軸まわりのひねりは含まない

            UpdateSwing(tip, omega, dt, now, guarding);
            UpdateThrust(input, controller, dt, now, guarding);

            prevRotation = swordRotation;
            prevTip = tip;
        }

        // 動きの測定をやり直す（向きが飛んだ・取れなくなった・カクついたとき）
        void ResetMotion()
        {
            hasPrev = false;
            inStroke = false;
            TipSpeed = 0f;
            thrustVelocity = Vector3.zero;
            ThrustImpulse = 0f;
        }

        void UpdateMove(SwitchInput input)
        {
            front = back = false;
            if (!config.useStick) return;
            float s = input.Stick(playerIndex).y;
            front = s >= config.stickThreshold;
            back  = s <= -config.stickThreshold;
        }

        #region 振り

        void UpdateSwing(Vector3 tip, Vector3 omega, float dt, float now, bool guarding)
        {
            if (guarding)   // 構えている間は振らない
            {
                inStroke = false;
                return;
            }

            // 後ろから見た刃先の動き（度）。ω × 刃先 の x,y 成分
            // 剣が体より後ろ（z<0）にあると見かけの動きが逆になるので、前を向いているものとして数える（|z|）。
            // 真上・真横を向いているときも前後に倒す回転を拾えるよう、下限（DepthFloor）を入れる
            Vector3 mid = (prevTip + tip).normalized;
            float depth = Mathf.Max(Mathf.Abs(mid.z), DepthFloor);
            Vector2 velocity = new Vector2(omega.y * depth - omega.z * mid.y, omega.z * mid.x - omega.x * depth);

            if (inStroke)
            {
                if (TipSpeed >= config.swingEndSpeed) strokeFastTime = now;
                if (now - strokeFastTime > StrokeEndGrace || now - strokeStartTime > config.swingTime)
                    inStroke = false;   // 止まった・長すぎる
                else if (strokeMove.magnitude >= ReverseMinTravel && velocity.magnitude >= config.swingEndSpeed
                         && Vector2.Dot(velocity.normalized, strokeMove.normalized) < ReverseDot)
                    StartStroke(now);   // 逆向きに切り返した（振りかぶり→振り下ろし）
            }
            if (!inStroke && TipSpeed >= config.swingStartSpeed) StartStroke(now);
            if (!inStroke) return;

            strokeMove += velocity * dt;
            strokePeak = Mathf.Max(strokePeak, TipSpeed);

            if (strokeFired || !strokeCanFire) return;
            if (strokeMove.magnitude < config.swingAngle || strokePeak < config.swingMinPeakSpeed) return;

            pendingSwing = AngleToDirection(Mathf.Atan2(strokeMove.x, strokeMove.y) * Mathf.Rad2Deg, Directions);
            pendingSwingTime = now;
            pendingThrust = false;
            strokeFired = true;    // 同じ振りでは1回だけ
            swingBlockedUntil = now + config.swingCooldown;
        }

        void StartStroke(float now)
        {
            inStroke = true;
            strokeFired = false;
            strokeCanFire = now >= swingBlockedUntil;   // 振った・突いた直後の動きは構えに戻しているだけ
            strokeStartTime = strokeFastTime = now;
            strokeMove = Vector2.zero;
            strokePeak = 0f;
        }

        #endregion

        #region 突き

        void UpdateThrust(SwitchInput input, Quaternion controller, float dt, float now, bool guarding)
        {
            LinearAccel = calibration * LinearAcceleration(input, controller, dt);   // プレイヤー基準

            // 振っている間とその直後は受け付けない（遠心力・振り終わりの急停止が前向きの加速度に見えるため）
            if (inStroke) thrustBlockedUntil = now + config.thrustLockout;
            if (guarding || now < thrustBlockedUntil || TipSpeed > config.thrustMaxTipSpeed)
            {
                thrustVelocity = Vector3.zero;
                ThrustImpulse = 0f;
                return;
            }

            // 加速度をためて勢い（速度の変化）にする。古い分は thrustWindow 秒で消えていく
            thrustVelocity = thrustVelocity * Mathf.Exp(-dt / Mathf.Max(config.thrustWindow, 0.01f)) + LinearAccel * dt;
            float forward = thrustVelocity.z * (config.invertThrustAccel ? -1f : 1f);
            ThrustImpulse = forward;
            if (forward > ThrustImpulsePeak || now - thrustPeakTime > 1f)
            {
                ThrustImpulsePeak = forward;
                thrustPeakTime = now;
            }

            if (!thrustArmed)
            {
                if (forward < config.thrustImpulse * ThrustRearm) thrustArmed = true;
                return;
            }
            if (forward < config.thrustImpulse) return;
            if (forward < config.thrustForwardDot * thrustVelocity.magnitude) return;   // 横・上下に動かしただけ

            thrustArmed = false;
            pendingThrust = true;
            pendingThrustTime = now;
            swingBlockedUntil = now + config.swingCooldown;   // 突いた後に引き戻す動きで振りが出ないようにする
        }

        // 重力を除いた加速度（G。Joy-Conの姿勢の座標）
        // 加速度はJoy-Conの座標なので姿勢で回して向きをそろえ、ほぼ静止しているときの値を重力として引く
        Vector3 LinearAcceleration(SwitchInput input, Quaternion controller, float dt)
        {
            Vector3 a = controller * input.Acceleration(playerIndex);
            if (a.sqrMagnitude < 1e-6f) return Vector3.zero;   // 値が来ていない
            if (!hasGravity)
            {
                gravity = a;
                hasGravity = true;
            }
            else if (Mathf.Abs(a.magnitude - 1f) < 0.1f && TipSpeed < StillTipSpeed)
            {
                gravity = Vector3.Lerp(gravity, a, 1f - Mathf.Exp(-config.gravityFollow * dt));
            }
            return a - gravity;
        }

        #endregion

        #region ガード

        // ガードの構え。境目で手がぶれても、guardGraceTime 秒続くまでは今の構えを保つ（ボタンを離したらすぐ解く）
        void UpdateGuard(bool guarding, float now)
        {
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

        #endregion

        static int AngleToDirection(float angle, int directions)
        {
            float step = 360f / directions;
            return ((Mathf.RoundToInt(angle / step) % directions) + directions) % directions;
        }
    }
}
