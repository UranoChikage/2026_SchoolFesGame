using System.Collections.Generic;
using UnityEngine;

namespace Common
{
    /// <summary>
    /// Switch(Joy-Con)振動の共通窓口。SwitchInput の振動版。Pirates・Chambaraなど全ゲームはここから振動させる。
    ///
    /// ■ 使い方
    ///   SwitchVibration.High(0, 0.2f) のように呼ぶ。index は SwitchInput と同じ番号。
    ///   登録が無い（PC・JoyCon無し）ときは何も起きない。
    ///
    /// ■ SDKを差し替えるとき
    ///   ISwitchVibrateInput を実装したクラスを別フォルダに作り、起動時に Register で登録する（SwitchInput.SetBackend と同じ形）。
    ///   Update(deltaTime) はここが毎フレーム呼ぶので、実装側で呼ぶ必要はない。
    ///
    ///   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    ///   static void Install()
    ///   {
    ///       for (int i = 0; i &lt; 4; i++)
    ///       {
    ///           var v = new MyVibrate();
    ///           v.GetVibrationDevice(i, padStyle);
    ///           SwitchVibration.Register(i, v);
    ///       }
    ///   }
    ///
    ///   コントローラーが繋がってから取得できる場合は、接続を検知したときに Register する（同じ番号は上書き）。
    /// </summary>
    public static class SwitchVibration
    {
        static readonly Dictionary<int, ISwitchVibrateInput> devices = new Dictionary<int, ISwitchVibrateInput>();

        /// <summary>そのコントローラー番号の振動デバイスを登録する（同じ番号は上書き）。null で登録解除</summary>
        public static void Register(int index, ISwitchVibrateInput device)
        {
            if (device == null) devices.Remove(index);
            else devices[index] = device;
            Diag($"Register index={index} device={(device != null ? device.GetType().Name : "解除")} 登録数={devices.Count}");
        }

        // 【診断】実機でログが見られないので、開発ビルドのときだけ画面の隅にも出す。原因が分かったら消す
        static readonly List<string> diagLines = new List<string>();
        static void Diag(string message)
        {
            Debug.Log("[Vibration診断] " + message);
            diagLines.Add($"{Time.realtimeSinceStartup:F1}s {message}");
            if (diagLines.Count > 10) diagLines.RemoveAt(0);
        }

        // 【診断】デバイス未登録のまま呼ばれたことを、番号ごとに1回だけ知らせる。原因が分かったら消す
        static readonly HashSet<int> warned = new HashSet<int>();
        static bool TryGet(int index, string what, out ISwitchVibrateInput device)
        {
            if (devices.TryGetValue(index, out device)) return true;
            if (warned.Add(index))
                Diag($"index={index} の振動デバイスが未登録です（{what}）。登録済み番号=[{string.Join(",", devices.Keys)}]");
            return false;
        }

        /// <summary>そのコントローラー番号に振動デバイスが登録済みか</summary>
        public static bool Has(int index) => devices.ContainsKey(index);

        /// <summary>弱め（低周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void Low(int index, float duration = -1f)
        {
            if (TryGet(index, "Low", out var d)) d.AmplitudeLowRandomVibrate(duration);
        }

        /// <summary>強め（高周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void High(int index, float duration = -1f)
        {
            if (TryGet(index, "High", out var d)) d.AmplitudeHighRandomVibrate(duration);
        }

        public static void Stop(int index)
        {
            if (devices.TryGetValue(index, out var d)) d.Stop();
        }

        /// <summary>登録済みの全コントローラーを強めに振動</summary>
        public static void HighAll(float duration)
        {
            foreach (var d in devices.Values) d.AmplitudeHighRandomVibrate(duration);
        }

        // 振動の時間管理のため、デバイスの Update を毎フレーム呼ぶ（シーンを跨いで残す）
        sealed class Driver : MonoBehaviour
        {
            void Update()
            {
                float dt = Time.unscaledDeltaTime;
                foreach (var d in devices.Values) d.Update(dt);
            }

            // 【診断】開発ビルドのときだけ、画面左下に振動の状態を出す
            GUIStyle style;
            void OnGUI()
            {
                if (!Debug.isDebugBuild) return;
                if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.yellow } };
                string text = $"Vibration 登録=[{string.Join(",", devices.Keys)}]" + string.Join("", diagLines);
                float h = 34f * (diagLines.Count + 1);
                GUI.Label(new Rect(10f, Screen.height - h - 10f, Screen.width - 20f, h), text, style);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateDriver()
        {
            var go = new GameObject("SwitchVibrationDriver") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }
        /// <summary>bnvib を再生する（他の振動と同時に鳴る）。戻り値は再生ID、失敗時 -1</summary>
        public static int Play(int index, string path)
        {
            if (!TryGet(index, "Play", out var d)) return -1;
            int id = d.PlayResources(path);
            Diag($"Play index={index} path={path} → 再生ID={id}");
            return id;
        }

        /// <summary>bnvib をループ再生する。止めるときは戻り値を StopPlay に渡す</summary>
        public static int Loop(int index, string path)
        {
            if (!TryGet(index, "Loop", out var d)) return -1;
            int id = d.PlayResources(path, true);
            Diag($"Loop index={index} path={path} → 再生ID={id}");
            return id;
        }

        /// <summary>Play / Loop が返したIDの再生だけを止める</summary>
        public static void StopPlay(int index, int playId)
        {
            if (devices.TryGetValue(index, out var d)) d.StopPlay(playId);
        }

        /// <summary>登録済みの全コントローラーの振動を止める</summary>
        public static void StopAll()
        {
            foreach (var d in devices.Values) d.Stop();
        }
        /*
         　 使う側実装方法
            SwitchVibration.High(0, 0.2f);                    // 舵取りを0.2秒強めに振動
            SwitchVibration.Low(1);                           // 砲手1を Stop まで弱めに振動
            SwitchVibration.Stop(1);
            SwitchVibration.Play(2, "Vibration/cannon.bnvib"); // bnvib を再生
            SwitchVibration.HighAll(0.5f);
          */
    }
}
