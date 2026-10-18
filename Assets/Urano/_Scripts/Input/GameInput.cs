using UnityEngine;
using UnityEngine.InputSystem;

namespace Pirates
{
    /// <summary>
    /// ゲームの入力は全てここを通す。Switch実機用に差し替えるときは、このファイルだけ直せばよい。
    ///
    /// ■ 差し替えの手順
    ///   1. 「JoyCon（傾き・ボタン）」の4つのメソッドを、使うSDK／ライブラリの呼び出しに書き換える。
    ///   2. 「PC（キーボード・マウス）」はデバッグ用にそのまま残してよい（JoyConが無いとき自動で使われる）。
    ///   他のスクリプトは UnityEngine.InputSystem や Joycon を直接触らない。
    ///
    /// ■ 番号の決まり
    ///   joyconIndex … 0番=舵取り(1P)、1〜3番=砲手。使うコントローラーの順番。
    /// </summary>
    public static class GameInput
    {
        // ======================================================================
        // JoyCon（傾き・ボタン）  ← Switch実機ではここを書き換える
        // ======================================================================

        /// <summary>そのコントローラーが繋がっているか</summary>
        public static bool IsJoyconConnected(int joyconIndex)
        {
            var manager = JoyconManager.Instance;
            return manager != null && manager.j != null && manager.j.Count > joyconIndex;
        }

        /// <summary>
        /// 加速度（重力ベクトル）。傾きの計算に使う。単位は問わない（角度は比率で出す）。
        /// 繋がっていなければ Vector3.zero。
        /// </summary>
        public static Vector3 GetJoyconAccel(int joyconIndex)
        {
            return IsJoyconConnected(joyconIndex) ? JoyconManager.Instance.j[joyconIndex].GetAccel() : Vector3.zero;
        }

        /// <summary>発射ボタンを押した瞬間（砲手用。JoyConでは R/ZR 側のボタン）</summary>
        public static bool JoyconFirePressed(int joyconIndex)
        {
            return IsJoyconConnected(joyconIndex)
                && JoyconManager.Instance.j[joyconIndex].GetButtonDown(Joycon.Button.SHOULDER_2);
        }

        /// <summary>舵輪を「真っ直ぐ」に戻すボタンを押した瞬間（舵取り用。JoyConでは十字キー下）</summary>
        public static bool JoyconCenterPressed(int joyconIndex)
        {
            return IsJoyconConnected(joyconIndex)
                && JoyconManager.Instance.j[joyconIndex].GetButtonDown(Joycon.Button.DPAD_DOWN);
        }

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
