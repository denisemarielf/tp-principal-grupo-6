using UnityEngine;

[DefaultExecutionOrder(50)]
public class WorldWeaponSway : MonoBehaviour
{
    [Header("Fuente de movimiento (usar UNA)")]
    [SerializeField] private WeaponSway source;                    // armas de fuego
    [Tooltip("Si es un arma melee sin animaciones: asignar acá y se copia la animación procedural.")]
    [SerializeField] private ProceduralMeleeAnimation meleeSource; // armas melee (tiene prioridad)

    [SerializeField] private Transform referenceFrame;   // vacío = raíz del jugador

    [Header("Armas de fuego")]
    [SerializeField] private float positionScale = 1f;
    [SerializeField] private float rotationScale = 0.5f; // en tercera persona conviene menos giro

    [Header("Melee")]
    [SerializeField] private float meleePositionScale = 1f;
    [SerializeField] private float meleeRotationScale = 1f;

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
        if (parent == null) return;

        Vector3 posOffset;
        Quaternion rotOffset;
        float posScale, rotScale;

        if (meleeSource != null)
        {
            posOffset = meleeSource.PositionOffset;
            rotOffset = meleeSource.RotationOffset;
            posScale = meleePositionScale;
            rotScale = meleeRotationScale;
        }
        else if (source != null)
        {
            posOffset = source.PositionOffset;
            rotOffset = source.RotationOffset;
            posScale = positionScale;
            rotScale = rotationScale;
        }
        else return;

        Quaternion frameRot = referenceFrame.rotation;

        // Posición: metros reales en el mundo -> espacio local del holder
        Vector3 worldDelta = (frameRot * posOffset) * posScale;
        transform.localPosition = restPos + parent.InverseTransformVector(worldDelta);

        // Rotación: mismo giro, expresado en los ejes del jugador
        Quaternion q = Quaternion.Slerp(Quaternion.identity, rotOffset, rotScale);
        Quaternion worldDeltaRot = frameRot * q * Quaternion.Inverse(frameRot);
        Quaternion restWorldRot = parent.rotation * restRot;
        transform.localRotation = Quaternion.Inverse(parent.rotation) * (worldDeltaRot * restWorldRot);
    }
}