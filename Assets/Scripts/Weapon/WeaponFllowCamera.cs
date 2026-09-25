using UnityEngine;

public class WeaponFollowCamera : MonoBehaviour
{
    public Transform cameraTransform; // Arrastrá acá la Main Camera del mismo jugador

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        transform.rotation = cameraTransform.rotation;
    }
}