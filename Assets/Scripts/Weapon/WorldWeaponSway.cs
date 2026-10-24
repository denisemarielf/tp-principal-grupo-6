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
    [SerializeField] private float rotationScale = 0.5f;

    [Header("Melee")]
    [SerializeField] private float meleePositionScale = 1f;
    [SerializeField] private float meleeRotationScale = 1f;

    [Header("Mano izquierda")]
    [Tooltip("El LeftHandRestPoint (hijo directo de este objeto).")]
    [SerializeField] private Transform leftHandPoint;
    [Tooltip("Activar en armas melee de UNA mano: la mano izquierda no acompaña el movimiento del arma.")]
    [SerializeField] private bool keepLeftHandStill = false;

    private Transform parent;
    private Vector3 restPos;
    private Quaternion restRot;
    private Vector3 leftRestPos;
    private Quaternion leftRestRot;

    private void Awake()
    {
        parent = transform.parent;
        restPos = transform.localPosition;
        restRot = transform.localRotation;

        if (referenceFrame == null)
            referenceFrame = transform.root;

        if (leftHandPoint != null)
        {
            if (leftHandPoint.parent != transform)
            {
                Debug.LogWarning($"{name}: leftHandPoint debe ser hijo directo de este objeto. Se ignora keepLeftHandStill.", this);
                keepLeftHandStill = false;
            }
            else
            {
                leftRestPos = leftHandPoint.localPosition;
                leftRestRot = leftHandPoint.localRotation;
            }
        }
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

        Vector3 worldDelta = (frameRot * posOffset) * posScale;
        transform.localPosition = restPos + parent.InverseTransformVector(worldDelta);

        Quaternion q = Quaternion.Slerp(Quaternion.identity, rotOffset, rotScale);
        Quaternion worldDeltaRot = frameRot * q * Quaternion.Inverse(frameRot);
        Quaternion restWorldRot = parent.rotation * restRot;
        transform.localRotation = Quaternion.Inverse(parent.rotation) * (worldDeltaRot * restWorldRot);

        if (keepLeftHandStill && leftHandPoint != null)
            CancelWeaponMotionOnLeftHand();
    }

    /// <summary>
    /// Deja el LeftHandRestPoint en la misma pose (respecto al holder) que tendría
    /// con el arma en reposo, aunque el arma se esté moviendo.
    /// </summary>
    private void CancelWeaponMotionOnLeftHand()
    {
        Vector3 scale = transform.localScale;

        Matrix4x4 weaponNow = Matrix4x4.TRS(transform.localPosition, transform.localRotation, scale);
        Matrix4x4 weaponRest = Matrix4x4.TRS(restPos, restRot, scale);
        Matrix4x4 leftRest = Matrix4x4.TRS(leftRestPos, leftRestRot, Vector3.one);

        // pose local del punto = inversa(arma ahora) * arma en reposo * pose de reposo del punto
        Matrix4x4 result = weaponNow.inverse * weaponRest * leftRest;

        leftHandPoint.localPosition = result.MultiplyPoint3x4(Vector3.zero);
        leftHandPoint.localRotation = result.rotation;
    }
}