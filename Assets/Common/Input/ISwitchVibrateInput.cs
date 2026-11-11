using UnityEngine;
namespace Common
{
    public interface ISwitchVibrateInput
    {
        void GetVibrationDevice(int id, int padStyle);
        void AmplitudeLowRandomVibrate(float duration = -1f);   // -1 = Stopまで継続
        void AmplitudeHighRandomVibrate(float duration = -1f);
        void PlayResources(string path);                        // StreamingAssetsからの相対パス
        void Stop();
        void Update(float deltaTime);                           // 毎フレーム呼ぶ
    }
}
