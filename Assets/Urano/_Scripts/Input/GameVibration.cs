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

        // 振動データ（StreamingAssets からの相対パス）。bnvib は他の振動と同時に鳴る
        public const string FireFile = "Vibration/Exprosion.bnvib";      // 大砲の発射
        public const string ExplosionFile = "Vibration/Exprosion.bnvib"; // 着弾の爆発
        public const string BeamFile = "Vibration/smash.bnvib";          // Beam中（ループ）

        /// <summary>振動データを1回再生する。再生できなければ（デバイス無し・ファイル無し）、代わりに短い振動を出す</summary>
        public static void PlayFile(int joyconIndex, string path, bool fallbackStrong, float fallbackDuration)
        {
            if (SwitchVibration.Play(joyconIndex, path) >= 0) return;
            if (fallbackStrong) SwitchVibration.High(joyconIndex, fallbackDuration);
            else SwitchVibration.Low(joyconIndex, fallbackDuration);
        }

        /// <summary>振動データをループ再生し、再生IDを返す。再生できなければ弱めの継続振動にして -1 を返す</summary>
        public static int LoopFile(int joyconIndex, string path)
        {
            int id = SwitchVibration.Loop(joyconIndex, path);
            if (id < 0) SwitchVibration.Low(joyconIndex, -1f);
            return id;
        }

        /// <summary>LoopFile の振動を止める（IDが -1 なら代わりの継続振動を止める）</summary>
        public static void StopLoop(int joyconIndex, int playId)
        {
            if (playId >= 0) SwitchVibration.StopPlay(joyconIndex, playId);
            else SwitchVibration.Stop(joyconIndex);
        }

        /// <summary>弱め（低周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void Low(int joyconIndex, float duration) => SwitchVibration.Low(joyconIndex, duration);

        /// <summary>強め（高周波）の振動。duration が負なら Stop するまで続く</summary>
        public static void High(int joyconIndex, float duration) => SwitchVibration.High(joyconIndex, duration);

        public static void Stop(int joyconIndex) => SwitchVibration.Stop(joyconIndex);

        /// <summary>登録済みの全コントローラーを強めに振動（被弾など、全員に伝えたいとき）</summary>
        public static void HighAll(float duration) => SwitchVibration.HighAll(duration);
    }
}
