
using UnityEditor;
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
    private float[] appliedAngles;
    private bool pitchApplied;
    private bool undone;

    private float currentPitch;
    private void Awake() // NUEVO
    {
        appliedAngles = new float[spineBones != null ? spineBones.Length : 0];
    }

    private void LateUpdate()
    {
        if (cameraController == null)
            return;

        float targetPitch = -cameraController.networkPitch.Value;
        targetPitch = Mathf.Clamp(targetPitch, -maxSpineAngle, maxSpineAngle);
        targetPitch *= pitchMultiplier;

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);

        if (spineBones == null)
            return;

        for (int i = 0; i < spineBones.Length; i++)
        {
            SpineBone entry = spineBones[i];
            if (entry == null || entry.bone == null)
                continue;

            float angle = currentPitch * entry.weight;
            appliedAngles[i] = angle; // NUEVO

            entry.bone.localRotation *= Quaternion.AngleAxis(angle, Vector3.right);
        }

        pitchApplied = true; // NUEVO
    }

    // NUEVO: quita temporalmente la inclinación aplicada en el LateUpdate anterior
    public void UndoPitch()
    {
        if (!pitchApplied || undone) return;
        ApplyAngles(-1f);
        undone = true;
    }

    // NUEVO: la devuelve
    public void RedoPitch()
    {
        if (!undone) return;
        ApplyAngles(1f);
        undone = false;
    }

    private void ApplyAngles(float sign)
    {
        if (spineBones == null || appliedAngles == null) return;

        for (int i = 0; i < spineBones.Length && i < appliedAngles.Length; i++)
        {
            if (spineBones[i] == null || spineBones[i].bone == null) continue;

            spineBones[i].bone.localRotation *=
                Quaternion.AngleAxis(sign * appliedAngles[i], Vector3.right);
        }
    }
}

