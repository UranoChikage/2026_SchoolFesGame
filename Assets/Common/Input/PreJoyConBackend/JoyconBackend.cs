using UnityEngine;
using UnityEngine.InputSystem;
using Common;

/// <summary>
/// PC開発用バックエンド。キーボードとマウスを「コントローラー2台」として見せる。
/// 実機用の NnBackend が登録済みならこちらは登録しない（SwitchInput.HasBackend で判定）。
///
/// ■ 0番 = キーボード（Piratesでは舵取り）
///   舵輪 … A/D・←/→ で回す（離すと中央へ戻る）、Space でセンター。
///          加速度として「舵輪の面内で回る重力ベクトル」を返すので、JoyConRudder は実機と同じ計算で動く。
///          ※軸はシーンの JoyConRudder（下=X, 左右=Z, 反転ON）に合わせてある。Inspectorを変えたらここも直すこと。
///   Confirm=Enter / Cancel=Backspace / Option=Tab / Up=W / Down=Space / Left=A / Right=D
///   Stick … Q/E で左右、S/W で上下
///
/// ■ 1番 = マウス（Piratesでは砲手）
///   角速度 … マウスの移動量（Select・砲手のカーソルは GyroPointer でこれを積算して動く）
///   加速度 … 常に真下の重力（0,0,-1）。GyroPointer が横移動の向きを決めるのに使う
///   Cancel（右クリック）でカーソルが画面中央に戻る
///   Confirm=左クリック / Cancel=右クリック / Option=中ボタン
///
/// ■ 2番以降は接続なし扱い
///   Piratesの2・3番砲手は、未接続のときマウス照準＋数字キー切替になる（CannonStation側の既存処理）。
/// </summary>
public class JoyconBackend : ISwitchInputBackend
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (!SwitchInput.HasBackend) SwitchInput.SetBackend(new JoyconBackend());
    }

    public const int KeyboardIndex = 0;
    public const int MouseIndex = 1;

    // 舵輪の調整値（JoyConRudder のキーボード操作の既定値に合わせてある）
    const float WheelSpeed = 180f;       // 度/秒
    const float WheelCenterSpeed = 240f; // 離したときに中央へ戻る速さ（度/秒）
    const float WheelLock = 540f;        // JoyConRudder.wheelLockDeg と同じにする

    // マウス1ピクセル移動 → カーソル1ピクセル移動になる換算（Select の sensitivity 既定値 15 に合わせる）
    const float PixelsPerDegree = 15f;

    float wheelAngle;
    Vector3 mouseGyro;
    int tickFrame = -1;

    // 1フレームに1回だけ状態を進める
    void Tick()
    {
        if (tickFrame == Time.frameCount) return;
        tickFrame = Time.frameCount;

        float dt = Time.deltaTime;
        var kb = Keyboard.current;

        // 舵輪
        float dir = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += 1f;
        }
        if (dir != 0f) wheelAngle += dir * WheelSpeed * dt;
        else wheelAngle = Mathf.MoveTowards(wheelAngle, 0f, WheelCenterSpeed * dt);
        if (kb != null && kb.spaceKey.wasPressedThisFrame) wheelAngle = 0f;
        wheelAngle = Mathf.Clamp(wheelAngle, -WheelLock, WheelLock);

        // マウスの移動量 → 角速度（Selectの軸の対応：x=ピッチ, z=ヨー/ロール）
        mouseGyro = Vector3.zero;
        var mouse = Mouse.current;
        if (mouse != null && dt > 0f)
        {
            Vector2 d = mouse.delta.ReadValue();
            mouseGyro.x = d.y / (dt * PixelsPerDegree);
            mouseGyro.z = -d.x / (dt * PixelsPerDegree);
        }
    }

    public int ConnectedCount => 2;

    public Vector3 Acceleration(int index)
    {
        Tick();

        if (index == KeyboardIndex)
        {
            // 重力ベクトルが舵輪の面内（X-Z）で回った状態。
            // シーンの JoyConRudder は 下=X軸 / 左右=Z軸 / 反転ON なので、それで wheelAngle に戻る形にする
            //   rawTilt = -atan2(az, -ax) = wheelAngle
            float r = wheelAngle * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Cos(r), 0f, -Mathf.Sin(r));
        }

        // マウス：固定の重力。動きは AngularVelocity（移動量）で伝える
        if (index == MouseIndex) return new Vector3(0f, 0f, -1f);

        return Vector3.zero;
    }

    public Vector3 AngularVelocity(int index)
    {
        Tick();
        return index == MouseIndex ? mouseGyro : Vector3.zero;
    }

    public Vector2 Stick(int index)
    {
        var kb = Keyboard.current;
        if (index != KeyboardIndex || kb == null) return Vector2.zero;
        float x = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    public bool TryGetDirection(int index, out Vector3 x, out Vector3 y, out Vector3 z)
    {
        x = y = z = Vector3.zero; // 姿勢は出さない
        return false;
    }

    public bool ReadButton(int index, SwitchButton button)
    {
        if (index == KeyboardIndex)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (button)
            {
                case SwitchButton.Confirm: return kb.enterKey.isPressed;
                case SwitchButton.Cancel: return kb.backspaceKey.isPressed;
                case SwitchButton.Option: return kb.tabKey.isPressed;
                case SwitchButton.Up: return kb.wKey.isPressed;
                case SwitchButton.Down: return kb.spaceKey.isPressed;
                case SwitchButton.Left: return kb.aKey.isPressed;
                default: return kb.dKey.isPressed;
            }
        }

        if (index == MouseIndex)
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            switch (button)
            {
                case SwitchButton.Confirm: return mouse.leftButton.isPressed;
                case SwitchButton.Cancel: return mouse.rightButton.isPressed;
                case SwitchButton.Option: return mouse.middleButton.isPressed;
                default: return false;
            }
        }

        return false;
    }

    public void ShowControllerSupport()
    {
        // PCにコントローラー設定画面は無い
    }
}
