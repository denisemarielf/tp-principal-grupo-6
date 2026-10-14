using Unity.Netcode;
using UnityEngine;

public class PlayerCameraSetup : NetworkBehaviour
{
    [SerializeField] private GameObject characterModel;

    public override void OnNetworkSpawn()
    {
        ApplyLayerSetup();
    }

    public void ApplyLayerSetup()
    {
        Camera[] allCameras = GetComponentsInChildren<Camera>(true);
        AudioListener listener = GetComponentInChildren<AudioListener>(true);

        foreach (Camera cam in allCameras)
        {
            cam.enabled = IsOwner;
        }
        if (listener != null)
            listener.enabled = IsOwner;

        if (characterModel != null)
        {
            int layer = IsOwner
                ? LayerMask.NameToLayer("OwnBody")
                : LayerMask.NameToLayer("Default");

            SetLayerRecursively(characterModel, layer);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
