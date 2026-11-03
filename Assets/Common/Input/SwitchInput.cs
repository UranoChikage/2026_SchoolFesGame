using UnityEngine;

namespace Common
{
    /// <summary>Joy-Conの物理ボタン。SDKが変わってもこの名前で使えるようにするための共通定義</summary>
    public enum SwitchButton
    {
        DpadDown, DpadRight, DpadLeft, DpadUp,
        SL, SR,
        Minus, Home, Plus, Capture,
        Stick,
        Shoulder1, // L / R
        Shoulder2, // ZL / ZR
    }

    /// <summary>
    /// Switch(Joy-Con)入力の共通窓口。Pirates・Chambaraなど全ゲームはここから生データを読む。
    /// ゲーム固有の意味づけ（発射・舵取り・振り判定など）はここに書かず、各ゲーム側で行うこと。
    ///
    /// ■ SDKを差し替えるとき
    ///   「SDK呼び出し」の region 内だけを書き換える。他のファイルは変更不要。
    ///   書き換えるのは 接続判定／加速度／角速度／ボタン の4種類だけ。
    ///
    /// ■ 番号の決まり
    ///   index は「繋がっているコントローラーの順番」(0始まり)。
    ///   どのindexを誰が使うかは各ゲームが決める（Pirates: 0=舵取り, 1〜3=砲手）。
    /// </summary>
    public static class SwitchInput
    {
        #region SDK呼び出し（差し替え対象）

        /// <summary>繋がっているコントローラーの数</summary>
        public static int ConnectedCount
        {
            get
            {
                var m = JoyconManager.Instance;
                return (m != null && m.j != null) ? m.j.Count : 0;
            }
        }

        /// <summary>加速度（重力ベクトル）。繋がっていなければ Vector3.zero</summary>
        public static Vector3 GetAccel(int index)
            => IsConnected(index) ? JoyconManager.Instance.j[index].GetAccel() : Vector3.zero;

        /// <summary>角速度。繋がっていなければ Vector3.zero</summary>
        public static Vector3 GetGyro(int index)
            => IsConnected(index) ? JoyconManager.Instance.j[index].GetGyro() : Vector3.zero;

        /// <summary>ボタンを押した瞬間</summary>
        public static bool ButtonDown(int index, SwitchButton button)
            => IsConnected(index) && JoyconManager.Instance.j[index].GetButtonDown(ToSdk(button));

        /// <summary>ボタンを押している間</summary>
        public static bool Button(int index, SwitchButton button)
            => IsConnected(index) && JoyconManager.Instance.j[index].GetButton(ToSdk(button));

        /// <summary>ボタンを離した瞬間</summary>
        public static bool ButtonUp(int index, SwitchButton button)
            => IsConnected(index) && JoyconManager.Instance.j[index].GetButtonUp(ToSdk(button));

        static Joycon.Button ToSdk(SwitchButton b)
        {
            switch (b)
            {
                case SwitchButton.DpadDown:  return Joycon.Button.DPAD_DOWN;
                case SwitchButton.DpadRight: return Joycon.Button.DPAD_RIGHT;
                case SwitchButton.DpadLeft:  return Joycon.Button.DPAD_LEFT;
                case SwitchButton.DpadUp:    return Joycon.Button.DPAD_UP;
                case SwitchButton.SL:        return Joycon.Button.SL;
                case SwitchButton.SR:        return Joycon.Button.SR;
                case SwitchButton.Minus:     return Joycon.Button.MINUS;
                case SwitchButton.Home:      return Joycon.Button.HOME;
                case SwitchButton.Plus:      return Joycon.Button.PLUS;
                case SwitchButton.Capture:   return Joycon.Button.CAPTURE;
                case SwitchButton.Stick:     return Joycon.Button.STICK;
                case SwitchButton.Shoulder1: return Joycon.Button.SHOULDER_1;
                default:                     return Joycon.Button.SHOULDER_2;
            }
        }

        #endregion

        /// <summary>そのコントローラーが繋がっているか</summary>
        public static bool IsConnected(int index) => index >= 0 && index < ConnectedCount;
    }
}
