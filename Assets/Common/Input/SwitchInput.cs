using UnityEngine;

namespace Common
{
    /// <summary>
    /// 抽象的なボタン操作。物理ボタンとの対応はSDK側（ToSdk）が持ち、ゲーム側は意味だけで読む。
    /// </summary>
    public enum SwitchButton
    {
        Confirm, // 決定（発射など）
        Cancel,  // キャンセル
        Option,  // オプション（メニューなど）
        Up, Down, Left, Right,
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

        /// <summary>加速度。単位は G（静止時は重力で大きさ約1）。繋がっていなければ Vector3.zero</summary>
        public static Vector3 Acceleration(int index)
            => IsConnected(index) ? JoyconManager.Instance.j[index].GetAccel() : Vector3.zero;

        /// <summary>角速度。繋がっていなければ Vector3.zero</summary>
        public static Vector3 AngularVelocity(int index)
            => IsConnected(index) ? JoyconManager.Instance.j[index].GetGyro() : Vector3.zero;

        /// <summary>
        /// 向き（正規直交系の3ベクトル）。SDKが姿勢推定した基底。
        /// 繋がっていない／まだ姿勢が出ていない（3つとも0＝零行列）場合は false を返し、値は捨てる。
        /// </summary>
        public static bool TryGetDirection(int index, out Vector3 x, out Vector3 y, out Vector3 z)
        {
            x = y = z = Vector3.zero;
            if (!IsConnected(index)) return false;
            var j = JoyconManager.Instance.j[index];
            if (j.i_b == Vector3.zero && j.j_b == Vector3.zero && j.k_b == Vector3.zero) return false;
            x = j.i_b; y = j.j_b; z = j.k_b;
            return true;
        }

        /// <summary>
        /// 抽象ボタンが「いま押されているか」。SDKから受け取るのはこのboolだけ。
        /// どの物理ボタンが決定/キャンセル/オプションかはSDK側の都合で、ここを差し替える。
        /// </summary>
        static bool ReadButton(int index, SwitchButton button)
            => IsConnected(index) && JoyconManager.Instance.j[index].GetButton(ToSdk(button));

        static Joycon.Button ToSdk(SwitchButton b)
        {
            switch (b)
            {
                case SwitchButton.Confirm: return Joycon.Button.SHOULDER_2; // ZR
                case SwitchButton.Cancel:  return Joycon.Button.SHOULDER_1; // R
                case SwitchButton.Option:  return Joycon.Button.PLUS;
                case SwitchButton.Up:      return Joycon.Button.DPAD_UP;
                case SwitchButton.Down:    return Joycon.Button.DPAD_DOWN;
                case SwitchButton.Left:    return Joycon.Button.DPAD_LEFT;
                default:                   return Joycon.Button.DPAD_RIGHT;
            }
        }

        #endregion

        /// <summary>加速度のノイズ幅(±G)。これ以下の変化は静止中の揺らぎとみなす</summary>
        public static float AccelerationNoise = 0.02f;

        /// <summary>角速度のノイズ幅(±)。これ以下は静止中の揺らぎとみなす</summary>
        public static float AngularVelocityNoise = 0.005f;

        const int MaxControllers = 8;
        static readonly Vector3[] lastAccel = new Vector3[MaxControllers];
        static readonly bool[] stillResult = new bool[MaxControllers];
        static readonly int[] stillFrame = new int[MaxControllers];

        /// <summary>
        /// 静止しているか。角速度が全軸ノイズ幅以内、かつ前フレームからの加速度の変化も全軸ノイズ幅以内なら true。
        /// 1フレームに何回呼んでも同じ結果。繋がっていなければ false。
        /// </summary>
        public static bool IsStill(int index)
        {
            if (!IsConnected(index) || index >= MaxControllers) return false;
            int frame = Time.frameCount + 1; // 0を「未計算」にするため+1
            if (stillFrame[index] == frame) return stillResult[index];

            Vector3 a = Acceleration(index);
            Vector3 g = AngularVelocity(index);
            Vector3 d = a - lastAccel[index];
            bool still = stillFrame[index] != 0
                && Mathf.Abs(g.x) <= AngularVelocityNoise && Mathf.Abs(g.y) <= AngularVelocityNoise && Mathf.Abs(g.z) <= AngularVelocityNoise
                && Mathf.Abs(d.x) <= AccelerationNoise && Mathf.Abs(d.y) <= AccelerationNoise && Mathf.Abs(d.z) <= AccelerationNoise;

            lastAccel[index] = a;
            stillFrame[index] = frame;
            stillResult[index] = still;
            return still;
        }

        // ---- ボタン：SDKのbool(ReadButton)から押下/離上の瞬間をこちらで求める ----
        const int ButtonCount = 7; // SwitchButton の数
        static readonly bool[,] btnCur = new bool[MaxControllers, ButtonCount];
        static readonly bool[,] btnPrev = new bool[MaxControllers, ButtonCount];
        static readonly int[,] btnFrame = new int[MaxControllers, ButtonCount];

        static void UpdateButton(int index, SwitchButton button)
        {
            int b = (int)button;
            int frame = Time.frameCount + 1;
            if (btnFrame[index, b] == frame) return;
            btnPrev[index, b] = btnCur[index, b];
            btnCur[index, b] = ReadButton(index, button);
            btnFrame[index, b] = frame;
        }

        static bool ValidButton(int index) => IsConnected(index) && index < MaxControllers;

        /// <summary>ボタンを押している間</summary>
        public static bool Button(int index, SwitchButton button)
        {
            if (!ValidButton(index)) return false;
            UpdateButton(index, button);
            return btnCur[index, (int)button];
        }

        /// <summary>ボタンを押した瞬間</summary>
        public static bool ButtonDown(int index, SwitchButton button)
        {
            if (!ValidButton(index)) return false;
            UpdateButton(index, button);
            return btnCur[index, (int)button] && !btnPrev[index, (int)button];
        }

        /// <summary>ボタンを離した瞬間</summary>
        public static bool ButtonUp(int index, SwitchButton button)
        {
            if (!ValidButton(index)) return false;
            UpdateButton(index, button);
            return !btnCur[index, (int)button] && btnPrev[index, (int)button];
        }

        /// <summary>そのコントローラーが繋がっているか</summary>
        public static bool IsConnected(int index) => index >= 0 && index < ConnectedCount;

        /// <summary>向き（Direction）から求めたオイラー角（度, Vector3）。向きが無ければ Vector3.zero</summary>
        public static Vector3 Angle(int index)
        {
            if (!TryGetDirection(index, out var x, out var y, out var z)) return Vector3.zero;
            // x=右, y=上, z=前 とみなして回転に変換（y,zが平行などで作れない場合は0）
            if (Vector3.Cross(z, y).sqrMagnitude < 1e-6f) return Vector3.zero;
            return Quaternion.LookRotation(z, y).eulerAngles;
        }
    }
}
