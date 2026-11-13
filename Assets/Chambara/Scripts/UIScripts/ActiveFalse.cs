using UnityEngine;

public class ActiveFalse : MonoBehaviour
{
    public void OnConnectedToServer()
    {
        gameObject.SetActive(false);
    }
}
