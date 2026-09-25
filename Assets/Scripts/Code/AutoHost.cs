using Unity.Netcode;
using UnityEngine;

public class AutoHost : MonoBehaviour
{
    private void Start()
    {
#if UNITY_EDITOR
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.StartHost();
        }
#endif
    }
}