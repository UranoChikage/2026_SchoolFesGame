using UnityEngine;

/// <summary>
/// フレームを開始時に固定する
/// </summary>
public class FrameChanger : MonoBehaviour
{
    [SerializeField] int frame = 60;

    private void Start()
    {
        QualitySettings.vSyncCount = 0;          // VSync をオフ
        Application.targetFrameRate = frame; 
    }
}
