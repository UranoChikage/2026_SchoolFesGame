using Common;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// デバッグ用：今のシーンを読み込み直す
/// キーボードの resetKey、または Joy-Con（1P・2Pどちらでも）で resetButtons を同時に押す
/// </summary>
public class SceneReset : MonoBehaviour
{
    [SerializeField] private KeyCode resetKey = KeyCode.T; // The key to press for resetting the scene
    [SerializeField, Tooltip("Joy-Conでこのボタンを全部同時に押すとリセット")]
    private SwitchButton[] resetButtons = { SwitchButton.Option, SwitchButton.Cancel };
    [SerializeField] private int controllerCount = 2;   // 見るコントローラーの数（0番から）

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(resetKey) || JoyconResetPressed())
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Reload the current scene
        }
    }

    // 全部押されていて、そのうちどれかをこのフレームに押した（押しっぱなしで何度もリセットしない）
    bool JoyconResetPressed()
    {
        if (resetButtons == null || resetButtons.Length == 0) return false;
        for (int i = 0; i < controllerCount; i++)
        {
            bool all = true, down = false;
            foreach (var b in resetButtons)
            {
                all &= SwitchInput.I.Button(i, b);
                down |= SwitchInput.I.ButtonDown(i, b);
            }
            if (all && down) return true;
        }
        return false;
    }
}
