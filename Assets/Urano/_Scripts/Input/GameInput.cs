using UnityEngine;
using UnityEngine.InputSystem;
using Common;

namespace Pirates
{
    /// <summary>
    /// Piratesの入力は全てここを通す。JoyConの生データは共通の Common.SwitchInput から読む。
    ///
    /// ■ SDKを差し替えるとき
    ///   Assets/Common/Input/SwitchInput.cs だけ直せばよい（このファイルは変更不要）。
    ///   ボタン割り当て（発射=ZR、センター=十字下 など）を変えたいときだけ、このファイルを直す。
    ///   「PC（キーボード・マウス）」はデバッグ用にそのまま残す（JoyConが無いとき自動で使われる）。
    ///   他のスクリプトは UnityEngine.InputSystem や Joycon を直接触らない。
    ///
    /// ■ 番号の決まり
    ///   joyconIndex … 0番=舵取り(1P)、1〜3番=砲手。使うコントローラーの順番。
    /// </summary>
    public static class GameInput
    {
        // ======================================================================
        // JoyCon（傾き・ボタン）  ← 共通のSwitchInputから読む。割り当てだけここで決める
        // ======================================================================

        /// <summary>
        /// そのコントローラーが使えるか。繋がっていて、かつ加速度の値が来ている場合だけ true。
        /// 繋がっていても値が全く来ない（反応がない）ときは false になり、呼び出し側はマウス／キーボードに切り替わる。
        /// </summary>
        public static bool IsJoyconConnected(int joyconIndex)
            => SwitchInput.I.IsConnected(joyconIndex)
            && SwitchInput.I.Acceleration(joyconIndex).sqrMagnitude > 1e-6f;

        /// <summary>
        /// 加速度（重力ベクトル）。傾きの計算に使う。単位は問わない（角度は比率で出す）。
        /// 繋がっていなければ Vector3.zero。
        /// </summary>
        public static Vector3 GetJoyconAccel(int joyconIndex) => SwitchInput.I.Acceleration(joyconIndex);

        /// <summary>発射ボタンを押した瞬間（砲手用。JoyConでは SL/SR 側のボタン）</summary>
        public static bool JoyconFirePressed(int joyconIndex) => SwitchInput.I.ButtonDown(joyconIndex, SwitchButton.Confirm);

        /// <summary>
        /// 舵輪を「真っ直ぐ」に戻すボタンを押した瞬間（舵取り用）。
        /// 下（十字キー／スティック下倒し）か Cancel（R）のどちらでも効く。実機でどちらかが割り当てられていなくても戻せるように。
        /// </summary>
        public static bool JoyconCenterPressed(int joyconIndex)
            => SwitchInput.I.ButtonDown(joyconIndex, SwitchButton.Down)
            || SwitchInput.I.ButtonDown(joyconIndex, SwitchButton.Cancel);

        /// <summary>カーソルを画面中央に戻すボタンを押した瞬間（砲手用。Title の Select と同じ Cancel＝R）</summary>
        public static bool JoyconRecenterPressed(int joyconIndex) => SwitchInput.I.ButtonDown(joyconIndex, SwitchButton.Cancel);

        // ======================================================================
        // PC（キーボード・マウス）  ← デバッグ用。JoyConが無いときに使われる
        // ======================================================================

        /// <summary>舵輪の回転方向。A/← で -1、D/→ で +1、なにも押さなければ 0</summary>
        public static float KeyboardSteerDir()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            float dir = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += 1f;
            return dir;
        }

        /// <summary>舵輪をセンターへ戻すキー（Space）を押した瞬間</summary>
        public static bool KeyboardCenterPressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }

        /// <summary>
        /// 砲手の切り替えキーを押した瞬間。stationNumber(1〜9)の数字キー（上段／テンキー）。
        /// PCで1人が複数の砲を切り替えて操作するためのもの。
        /// </summary>
        public static bool StationSelectPressed(int stationNumber)
        {
            var kb = Keyboard.current;
            if (kb == null || stationNumber < 1 || stationNumber > 9) return false;
            return kb[Key.Digit1 + (stationNumber - 1)].wasPressedThisFrame
                || kb[Key.Numpad1 + (stationNumber - 1)].wasPressedThisFrame;
        }

        /// <summary>マウスカーソルの画面座標（ピクセル）。マウスが無ければ false</summary>
        public static bool TryGetMousePosition(out Vector2 position)
        {
            var mouse = Mouse.current;
            position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return mouse != null;
        }

        /// <summary>マウスの発射ボタン（左クリック）を押した瞬間</summary>
        public static bool MouseFirePressed()
        {
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        // ======================================================================
        // ゲーム進行（スタート・スキップ）
        // ======================================================================

        /// <summary>待機中の「スタート」操作（PC: Space）。Switchでは本体のボタンを追加するとよい</summary>
        public static bool StartPressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }

        /// <summary>導入演出の「スキップ」操作（PC: Enter）</summary>
        public static bool SkipPressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.enterKey.wasPressedThisFrame;
        }
    }
}
