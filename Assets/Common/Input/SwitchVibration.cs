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
        }

        /// <summary>そのコントローラー番号に振動デバイスが登録済みか</summary>
        public static bool Has(int index) => devices.ContainsKey(index);

        /// <summary>弱め（低周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void Low(int index, float duration = -1f)
        {
            if (devices.TryGetValue(index, out var d)) d.AmplitudeLowRandomVibrate(duration);
        }

        /// <summary>強め（高周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void High(int index, float duration = -1f)
        {
            if (devices.TryGetValue(index, out var d)) d.AmplitudeHighRandomVibrate(duration);
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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateDriver()
        {
            var go = new GameObject("SwitchVibrationDriver") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }
    }
}
