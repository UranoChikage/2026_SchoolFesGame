using Common;

namespace Pirates
{
    /// <summary>
    /// Piratesの振動は全てここを通す。実体は Common.SwitchVibration（SDK側が ISwitchVibrateInput を登録する）。
    /// joyconIndex は GameInput と同じ（0=舵取り、1〜3=砲手）。登録が無い（PC・JoyCon無し）ときは何も起きない。
    /// 強さや長さの目安をここで決める。登録のしかたは SwitchVibration のコメントを参照。
    /// </summary>
    public static class GameVibration
    {
        // 強さの目安（インターフェースは Low / High の2種類だけ）
        public const float ShortPulse = 0.12f;
        public const float MediumPulse = 0.25f;

        /// <summary>弱め（低周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void Low(int joyconIndex, float duration) => SwitchVibration.Low(joyconIndex, duration);

        /// <summary>強め（高周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void High(int joyconIndex, float duration) => SwitchVibration.High(joyconIndex, duration);

        public static void Stop(int joyconIndex) => SwitchVibration.Stop(joyconIndex);

        /// <summary>登録済みの全コントローラーを強めに振動（被弾など、全員に伝えたいとき）</summary>
        public static void HighAll(float duration) => SwitchVibration.HighAll(duration);
    }
}
