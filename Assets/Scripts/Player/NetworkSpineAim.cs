
using UnityEngine;

public class NetworkSpineAim : MonoBehaviour
{
    [System.Serializable]
    public class SpineBone
    {
        public Transform bone;

        [Range(0f, 1f)]
        public float weight = 0.25f;
    }

    [Header("Referencia")]
    [SerializeField] private CameraControllerFPS cameraController;

    [Header("Huesos de la columna")]
    [SerializeField] private SpineBone[] spineBones;

    [Header("Configuracion")]
    [SerializeField] private float maxSpineAngle = 60f;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float pitchMultiplier = 1f;

    private float currentPitch;

    private void LateUpdate()
    {
        if (cameraController == null)
            return;

        // Pitch sincronizado por Netcode.
        float targetPitch = -cameraController.networkPitch.Value;

        // Limitar cuanto puede inclinarse el torso.
        targetPitch = Mathf.Clamp(
            targetPitch,
            -maxSpineAngle,
            maxSpineAngle
        );

        targetPitch *= pitchMultiplier;

        // Suavizar el movimiento de los jugadores remotos.
        currentPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            Time.deltaTime * smoothSpeed
        );

        if (spineBones == null)
            return;

        foreach (SpineBone entry in spineBones)
        {
            if (entry == null || entry.bone == null)
                continue;

            // Sumar la inclinacion a la pose de la animacion.
            float angle = currentPitch * entry.weight;

            entry.bone.localRotation *= Quaternion.AngleAxis(
                angle,
                Vector3.right
            );
        }
    }
}

