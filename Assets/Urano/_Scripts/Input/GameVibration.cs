using System.Collections.Generic;
using UnityEngine;
using Common;

namespace Pirates
{
    /// <summary>
    /// Piratesの振動は全てここを通す。実体は Common.ISwitchVibrateInput（SDK側の実装）。
    ///
    /// ■ SDK側（開発機）でやること
    ///   コントローラーごとに ISwitchVibrateInput の実装を用意し、GetVibrationDevice で振動デバイスを取得したあと
    ///   GameVibration.Register(joyconIndex, 実装) を呼ぶ（joyconIndex は GameInput と同じ。0=舵取り、1〜3=砲手）。
    ///   Update(deltaTime) はこのクラスが毎フレーム呼ぶので、SDK側で呼ぶ必要はない。
    ///
    /// ■ 登録が無い（PC・JoyCon無し）ときは、何も起きない。
    /// </summary>
    public static class GameVibration
    {
        // 強さの目安（インターフェースは Low / High の2種類だけ）
        public const float ShortPulse = 0.12f;
        public const float MediumPulse = 0.25f;

        static readonly Dictionary<int, ISwitchVibrateInput> devices = new Dictionary<int, ISwitchVibrateInput>();

        /// <summary>そのコントローラー番号の振動デバイスを登録する（同じ番号は上書き）。null で登録解除</summary>
        public static void Register(int joyconIndex, ISwitchVibrateInput device)
        {
            if (device == null) devices.Remove(joyconIndex);
            else devices[joyconIndex] = device;
        }

        /// <summary>弱め（低周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void Low(int joyconIndex, float duration)
        {
            if (devices.TryGetValue(joyconIndex, out var d)) d.AmplitudeLowRandomVibrate(duration);
        }

        /// <summary>強め（高周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void High(int joyconIndex, float duration)
        {
            if (devices.TryGetValue(joyconIndex, out var d)) d.AmplitudeHighRandomVibrate(duration);
        }

        public static void Stop(int joyconIndex)
        {
            if (devices.TryGetValue(joyconIndex, out var d)) d.Stop();
        }

        /// <summary>登録済みの全コントローラーを強めに振動（被弾など、全員に伝えたいとき）</summary>
        public static void HighAll(float duration)
        {
            foreach (var d in devices.Values) d.AmplitudeHighRandomVibrate(duration);
        }

        static void Tick(float deltaTime)
        {
            foreach (var d in devices.Values) d.Update(deltaTime);
        }

        // 振動の時間管理のため、デバイスの Update を毎フレーム呼ぶ（シーンを跨いで残す）
        sealed class Driver : MonoBehaviour
        {
            void Update() => Tick(Time.unscaledDeltaTime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateDriver()
        {
            var go = new GameObject("GameVibrationDriver") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }
    }
}
