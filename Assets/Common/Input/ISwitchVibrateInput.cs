namespace Common
{
    public interface ISwitchVibrateInput
    {
        void GetVibrationDevice(int id, int padStyle);
        void AmplitudeLowRandomVibrate(float duration = -1f);   // -1 = Stopまで継続
        void AmplitudeHighRandomVibrate(float duration = -1f);

        /// <summary>再生して再生IDを返す（失敗時 -1）。他の振動と同時に鳴る。path は StreamingAssets からの相対パス</summary>
        int PlayResources(string path, bool loop = false);

        /// <summary>PlayResources が返したIDの再生だけを止める</summary>
        void StopPlay(int playId);

        void Stop();                                            // 全部止める
        void Update(float deltaTime);                           // 毎フレーム呼ぶ
    }
}