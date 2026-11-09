using UnityEngine;

namespace Common
{
    /// <summary>
    /// 抽象的なボタン操作。物理ボタンとの対応は各バックエンドが持ち、ゲーム側は意味だけで読む。
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
    /// ■ 使い方
    ///   SwitchInput.I.ButtonDown(0, SwitchButton.Confirm) のように I 経由で呼ぶ。
    ///   シーンを跨いでも状態は保たれる（MonoBehaviourではないので破棄されない）。
    ///
    /// ■ SDKを差し替えるとき
    ///   ISwitchInputBackend を実装したクラスを別フォルダに作り、SetBackend で登録する。
    ///   このファイルは変更不要。
    ///
    /// ■ 番号の決まり
    ///   index は「繋がっているコントローラーの順番」(0始まり)。
    ///   どのindexを誰が使うかは各ゲームが決める（Pirates: 0=舵取り, 1〜3=砲手）。
    /// </summary>
    public class SwitchInput : MonoBehaviour
    {
        #region シングルトン

        static readonly SwitchInput instance = new SwitchInput();

        /// <summary>唯一の実体。入力は全ゲームで共有する</summary>
        public static SwitchInput I => instance;

        SwitchInput() { } // 外から new させない

        #endregion

        #region バックエンド登録

        static ISwitchInputBackend backend;

        /// <summary>バックエンドが登録済みか。優先度の低い実装は、これが false のときだけ登録する</summary>
        public static bool HasBackend => backend != null;

        /// <summary>使用するバックエンドを登録する。バックエンド側から自分で呼ぶ</summary>
        public static void SetBackend(ISwitchInputBackend b)
        {
            if (b != null) backend = b;
        }

        #endregion

        #region 生データ（バックエンドに委譲）

        /// <summary>繋がっているコントローラーの数</summary>
        public int ConnectedCount => backend?.ConnectedCount ?? 0;

        /// <summary>そのコントローラーが繋がっているか</summary>
        public bool IsConnected(int index)
            => index >= 0 && index < MaxControllers && index < ConnectedCount;

        /// <summary>加速度。単位は G（静止時は重力で大きさ約1）。繋がっていなければ Vector3.zero</summary>
        public Vector3 Acceleration(int index)
            => IsConnected(index) ? backend.Acceleration(index) : Vector3.zero;

        /// <summary>角速度。繋がっていなければ Vector3.zero</summary>
        public Vector3 AngularVelocity(int index)
            => IsConnected(index) ? backend.AngularVelocity(index) : Vector3.zero;

        /// <summary>スティックの傾き。各軸 -1〜1。繋がっていなければ Vector2.zero</summary>
        public Vector2 Stick(int index)
            => IsConnected(index) ? backend.Stick(index) : Vector2.zero;

        /// <summary>
        /// 向き（正規直交系の3ベクトル）。SDKが姿勢推定した基底。
        /// 繋がっていない／まだ姿勢が出ていない場合は false を返し、値は捨てる。
        /// </summary>
        public bool TryGetDirection(int index, out Vector3 x, out Vector3 y, out Vector3 z)
        {
            x = y = z = Vector3.zero;
            return IsConnected(index) && backend.TryGetDirection(index, out x, out y, out z);
        }

        /// <summary>コントローラー設定画面を出す。対応しないバックエンドでは何も起きない</summary>
        public void ShowControllerSupport() => backend?.ShowControllerSupport();

        bool ReadButton(int index, SwitchButton button)
            => IsConnected(index) && backend.ReadButton(index, button);

        #endregion

        const int MaxControllers = 8;

        #region 静止判定

        /// <summary>加速度のノイズ幅(±G)。これ以下の変化は静止中の揺らぎとみなす</summary>
        public static float AccelerationNoise = 0.02f;

        /// <summary>角速度のノイズ幅(±)。これ以下は静止中の揺らぎとみなす</summary>
        public static float AngularVelocityNoise = 0.005f;

        readonly Vector3[] lastAccel = new Vector3[MaxControllers];
        readonly bool[] stillResult = new bool[MaxControllers];
        readonly int[] stillFrame = new int[MaxControllers];

        /// <summary>
        /// 静止しているか。角速度が全軸ノイズ幅以内、かつ前フレームからの加速度の変化も全軸ノイズ幅以内なら true。
        /// 1フレームに何回呼んでも同じ結果。繋がっていなければ false。
        ///
        /// 注意：前フレームとの差分を見るため、呼ばないフレームがあると比較できず false を返す。
        ///       使うなら毎フレーム呼ぶこと。
        /// </summary>
        public bool IsStill(int index)
        {
            if (!IsConnected(index)) return false;

            int frame = Time.frameCount + 1; // 0を「未計算」にするため+1
            if (stillFrame[index] == frame) return stillResult[index];

            Vector3 a = Acceleration(index);
            Vector3 g = AngularVelocity(index);
            Vector3 d = a - lastAccel[index];

            // 直前のフレームで計算していなければ差分が当てにならない
            bool hasPrev = stillFrame[index] == frame - 1;

            bool still = hasPrev
                && Mathf.Abs(g.x) <= AngularVelocityNoise && Mathf.Abs(g.y) <= AngularVelocityNoise && Mathf.Abs(g.z) <= AngularVelocityNoise
                && Mathf.Abs(d.x) <= AccelerationNoise && Mathf.Abs(d.y) <= AccelerationNoise && Mathf.Abs(d.z) <= AccelerationNoise;

            lastAccel[index] = a;
            stillFrame[index] = frame;
            stillResult[index] = still;
            return still;
        }

        #endregion

        #region ボタン（バックエンドのboolから押下/離上の瞬間を求める）

        const int ButtonCount = 7; // SwitchButton の数
        readonly bool[,] btnCur = new bool[MaxControllers, ButtonCount];
        readonly bool[,] btnPrev = new bool[MaxControllers, ButtonCount];
        readonly int[,] btnFrame = new int[MaxControllers, ButtonCount];

        void UpdateButton(int index, SwitchButton button)
        {
            int b = (int)button;
            int frame = Time.frameCount + 1;
            if (btnFrame[index, b] == frame) return;

            // 呼ばないフレームがあっても、離した事実は拾えるので前回値をそのまま前フレーム扱いにする
            btnPrev[index, b] = btnCur[index, b];
            btnCur[index, b] = ReadButton(index, button);
            btnFrame[index, b] = frame;
        }

        /// <summary>ボタンを押している間</summary>
        public bool Button(int index, SwitchButton button)
        {
            if (!IsConnected(index)) return false;
            UpdateButton(index, button);
            return btnCur[index, (int)button];
        }

        /// <summary>ボタンを押した瞬間</summary>
        public bool ButtonDown(int index, SwitchButton button)
        {
            if (!IsConnected(index)) return false;
            UpdateButton(index, button);
            return btnCur[index, (int)button] && !btnPrev[index, (int)button];
        }

        /// <summary>ボタンを離した瞬間</summary>
        public bool ButtonUp(int index, SwitchButton button)
        {
            if (!IsConnected(index)) return false;
            UpdateButton(index, button);
            return !btnCur[index, (int)button] && btnPrev[index, (int)button];
        }

        #endregion

        /// <summary>向き（Direction）から求めたオイラー角（度, Vector3）。向きが無ければ Vector3.zero</summary>
        public Vector3 Angle(int index)
        {
            if (!TryGetDirection(index, out var x, out var y, out var z)) return Vector3.zero;
            // x=右, y=上, z=前 とみなして回転に変換（y,zが平行などで作れない場合は0）
            if (Vector3.Cross(z, y).sqrMagnitude < 1e-6f) return Vector3.zero;
            return Quaternion.LookRotation(z, y).eulerAngles;
        }
    }
}