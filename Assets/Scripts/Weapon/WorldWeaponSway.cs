using UnityEngine;

public class WorldWeaponSway : MonoBehaviour
{
    [SerializeField] private WeaponSway source;          // WeaponSway del viewmodel de esta arma
    [SerializeField] private Transform referenceFrame;   // vacío = raíz del jugador
    [SerializeField] private float positionScale = 1f;
    [SerializeField] private float rotationScale = 0.5f; // en tercera persona conviene menos giro

    private Transform parent;
    private Vector3 restPos;
    private Quaternion restRot;

    private void Awake()
    {
        parent = transform.parent;
        restPos = transform.localPosition;
        restRot = transform.localRotation;

        if (referenceFrame == null)
            referenceFrame = transform.root;
    }

    private void Update()
    {
        if (source == null || parent == null) return;

        Quaternion frameRot = referenceFrame.rotation;

        // Posición: metros reales en el mundo -> espacio local del holder (compensa su escala y sus ejes)
        Vector3 worldDelta = (frameRot * source.PositionOffset) * positionScale;
        transform.localPosition = restPos + parent.InverseTransformVector(worldDelta);

        // Rotación: mismo giro, pero expresado en los ejes del jugador
        Quaternion q = Quaternion.Slerp(Quaternion.identity, source.RotationOffset, rotationScale);
        Quaternion worldDeltaRot = frameRot * q * Quaternion.Inverse(frameRot);
        Quaternion restWorldRot = parent.rotation * restRot;
        transform.localRotation = Quaternion.Inverse(parent.rotation) * (worldDeltaRot * restWorldRot);
    }
}