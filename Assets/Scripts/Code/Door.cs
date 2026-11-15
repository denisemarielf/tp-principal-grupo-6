using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Door : MonoBehaviour
{
    [SerializeField] private float openAngle = 90f;      // usá negativo para abrir hacia el otro lado
    [SerializeField] private float openDuration = 1.2f;
    [SerializeField] private Vector3 axis = Vector3.up;  // eje de giro en coordenadas del mundo

    [Tooltip("Opcional: objeto vacío ubicado en la bisagra. Si está vacío, gira sobre el pivote de la puerta.")]
    [SerializeField] private Transform hinge;

    [Header("NavMesh")]
    [Tooltip("Obstáculo (con Carve) que bloquea el NavMesh mientras la puerta está cerrada. Si está vacío, se busca en este objeto o en sus hijos.")]
    [SerializeField] private NavMeshObstacle navObstacle;

    [Header("Sonido")]
    [SerializeField] private AudioClip openClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private Vector3 startPos;
    private Quaternion startRot;
    private bool isOpen;

    private void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;

        if (navObstacle == null)
            navObstacle = GetComponentInChildren<NavMeshObstacle>();
    }

    public void Open(bool instant)
    {
        if (isOpen) return;
        isOpen = true;

        // Al desactivar el obstáculo, el carve desaparece y el cuarto
        // se reconecta al NavMesh sin necesidad de re-bakear.
        if (navObstacle != null)
            navObstacle.enabled = false;

        if (instant)
        {
            Apply(openAngle);
        }
        else
        {
            if (openClip != null)
                AudioSource.PlayClipAtPoint(openClip, transform.position, volume);
            StartCoroutine(OpenRoutine());
        }
    }

    private IEnumerator OpenRoutine()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openDuration;
            Apply(Mathf.Lerp(0f, openAngle, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        Apply(openAngle);
    }

    private void Apply(float angle)
    {
        Quaternion rot = Quaternion.AngleAxis(angle, axis);
        Vector3 center = hinge != null ? hinge.position : startPos;

        transform.position = center + rot * (startPos - center);
        transform.rotation = rot * startRot;
    }
}
