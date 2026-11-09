using UnityEngine;
using Common;

/// <summary>
/// 非公式Joy-Conライブラリ版。SDKが無いPCでの開発用。
/// NnBackend が登録済みならこちらは登録しない（手順6の LoadType 順で保証）。
/// </summary>
public class JoyconBackend : ISwitchInputBackend
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (!SwitchInput.HasBackend) SwitchInput.SetBackend(new JoyconBackend());
    }

    public int ConnectedCount
    {
        get
        {
            var m = JoyconManager.Instance;
            return (m != null && m.j != null) ? m.j.Count : 0;
        }
    }
    public Vector2 Stick(int index)
    {
        return new Vector2();
    }
    public Vector3 Acceleration(int index) => JoyconManager.Instance.j[index].GetAccel();

    public Vector3 AngularVelocity(int index) => JoyconManager.Instance.j[index].GetGyro();

    public bool TryGetDirection(int index, out Vector3 x, out Vector3 y, out Vector3 z)
    {
        x = y = z = Vector3.zero;
        var j = JoyconManager.Instance.j[index];
        if (j.i_b == Vector3.zero && j.j_b == Vector3.zero && j.k_b == Vector3.zero) return false;
        x = j.i_b; y = j.j_b; z = j.k_b;
        return true;
    }

    public bool ReadButton(int index, SwitchButton button)
        => JoyconManager.Instance.j[index].GetButton(ToSdk(button));

    static Joycon.Button ToSdk(SwitchButton b)
    {
        switch (b)
        {
            case SwitchButton.Confirm: return Joycon.Button.SHOULDER_2; // ZR
            case SwitchButton.Cancel: return Joycon.Button.SHOULDER_1; // R
            case SwitchButton.Option: return Joycon.Button.PLUS;
            case SwitchButton.Up: return Joycon.Button.DPAD_UP;
            case SwitchButton.Down: return Joycon.Button.DPAD_DOWN;
            case SwitchButton.Left: return Joycon.Button.DPAD_LEFT;
            default: return Joycon.Button.DPAD_RIGHT;
        }
    }
    public void ShowControllerSupport()
    {
        //非公式版にそんな機能はないのでそのまま返す
        return;
    }
}