using System.Text;
using Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chambara
{
    /// <summary>
    /// デバッグ用：コントローラーの状況を画面に文字で出す（OnGUI）。シーンのどこかに置くだけで動く
    /// 1P・2P（コントローラー0番・1番）の生データと、入力の結果・プレイヤーの状態を表示する
    /// toggleKey で表示/非表示を切り替える
    /// </summary>
    public class ControllerDebugView : MonoBehaviour
    {
        [SerializeField] bool show = true;               // 最初から表示するか
        [SerializeField] Key toggleKey = Key.F1;
        [SerializeField] int fontSize = 20;
        [SerializeField] Vector2 margin = new Vector2(10f, 10f);

        const int PlayerCount = 2;

        static readonly string[] DirNames = { "上", "右上", "右", "右下", "下", "左下", "左", "左上" };

        readonly string[] texts = new string[PlayerCount];
        readonly StringBuilder sb = new StringBuilder();
        GUIStyle style, shadow;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) show = !show;
            if (!show) return;

            // OnGUI は1フレームに何回も呼ばれるので、文字列はここで1回だけ作る
            for (int i = 0; i < PlayerCount; i++) texts[i] = Build(i);
        }

        string Build(int index)
        {
            var input = SwitchInput.I;
            sb.Clear();
            sb.AppendLine($"■ {index + 1}P（コントローラー{index}番）");

            // 生データ
            bool connected = input.IsConnected(index);
            bool hasDir = input.TryGetDirection(index, out _, out _, out _);
            sb.AppendLine($"接続: {(connected ? "あり" : "なし")}　向き: {(hasDir ? "あり" : "なし")}");
            if (connected)
            {
                Vector3 a = input.Acceleration(index);
                sb.AppendLine($"加速度(G): {Vec(a)}  |{a.magnitude:F2}|");
                Vector3 w = input.AngularVelocity(index);
                sb.AppendLine($"角速度: {Vec(w)}  |{w.magnitude:F0}|");
                Vector2 st = input.Stick(index);
                sb.AppendLine($"スティック: ({st.x:F2}, {st.y:F2})");
                sb.AppendLine($"Confirm(ZR/ZL): {OnOff(input.Button(index, SwitchButton.Confirm))}　Cancel: {OnOff(input.Button(index, SwitchButton.Cancel))}");
            }

            // 結果
            PlayerBehaviour player = FindPlayer(index);
            JoyconInputContext joycon = player != null ? player.GetComponent<JoyconInputContext>() : null;
            if (joycon != null)
            {
                sb.AppendLine($"入力元: {(joycon.IsUsingJoycon ? "Joy-Con" : "キーボード")}");
                PlayerInput last = joycon.LastInput;
                sb.AppendLine($"前進: {OnOff(last.isFront)}　後退: {OnOff(last.isBack)}　構え: {Dir(last.pose)}");
                string swing = joycon.LastSwing >= 0 ? $"{Dir(joycon.LastSwing)}（{Time.time - joycon.LastSwingTime:F1}秒前）" : "-";
                sb.AppendLine($"最後に振った方向: {swing}");
                string thrust = joycon.LastThrustTime >= 0f ? $"{Time.time - joycon.LastThrustTime:F1}秒前" : "-";
                sb.AppendLine($"最後に突いた: {thrust}　加速度(前+): {Vec(joycon.LinearAccel)}");
                // 調整用：JoyconInputConfig のしきい値と見比べる
                sb.AppendLine($"刃先の速さ: {joycon.TipSpeed:F0}度/秒{(joycon.InStroke ? "　振り中" : "")}");
                sb.AppendLine($"振り: 最高 {joycon.StrokePeakSpeed:F0}度/秒（swingMinPeakSpeed）　回った量 {joycon.StrokeTravel:F0}度（swingAngle）");
                sb.AppendLine($"突きの勢い: {joycon.ThrustImpulse:F2}　直近の最大 {joycon.ThrustImpulsePeak:F2}（thrustImpulse）　回った量 {joycon.ThrustTurn:F0}度（thrustMaxTurn）");
            }
            else if (player != null)
            {
                sb.AppendLine("入力元: キーボード（JoyconInputContext なし）");
            }
            if (player != null && player.Data != null)
                sb.AppendLine($"状態: {player.Data.state}　方向: {Dir(player.Data.dir)}");
            else
                sb.AppendLine("状態: -（試合中でない）");

            return sb.ToString();
        }

        static PlayerBehaviour FindPlayer(int index)
        {
            var manager = ChambaraManager.Instance;
            if (manager == null || manager.Players == null || index >= manager.Players.Length) return null;
            return manager.Players[index];   // 破棄済みなら Unity の null になる
        }

        static string Vec(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
        static string OnOff(bool b) => b ? "ON" : "off";
        static string Dir(int dir) => dir >= 0 && dir < DirNames.Length ? DirNames[dir] : "-";

        private void OnGUI()
        {
            if (!show) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = false };
                style.normal.textColor = Color.black;
                shadow = new GUIStyle(style);
                shadow.normal.textColor = Color.white;
            }

            // 1Pは左、2Pは右に並べる
            float width = (Screen.width - margin.x * 3f) / PlayerCount;
            for (int i = 0; i < PlayerCount; i++)
            {
                if (string.IsNullOrEmpty(texts[i])) continue;
                var rect = new Rect(margin.x + (width + margin.x) * i, margin.y, width, Screen.height - margin.y * 2f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), texts[i], shadow);   // 読みやすいよう影をつける
                GUI.Label(rect, texts[i], style);
            }
        }
    }
}
