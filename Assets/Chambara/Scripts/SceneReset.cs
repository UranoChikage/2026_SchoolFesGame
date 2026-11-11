using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneReset : MonoBehaviour
{
    [SerializeField] private KeyCode resetKey = KeyCode.T; // The key to press for resetting the scene
    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(resetKey))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Reload the current scene
        }
    }
}

