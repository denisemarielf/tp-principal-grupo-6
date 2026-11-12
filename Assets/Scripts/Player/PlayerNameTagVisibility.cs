using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hace que el cartel con el nombre del jugador se vea a través de las paredes,
/// solo dentro de una distancia máxima, con fade y tamaño legible a lo lejos.
/// Poner en el objeto PlayerNameText (TextMeshPro 3D).
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class PlayerNameTagVisibility : MonoBehaviour
{
    [Header("Distancia")]
    [Tooltip("Más allá de esta distancia el nombre se oculta.")]
    [SerializeField] private float maxDistance = 40f;
    [Tooltip("Desde esta distancia empieza a desvanecerse.")]
    [SerializeField] private float fadeStartDistance = 30f;

    [Header("Tamaño")]
    [Tooltip("Agranda el cartel con la distancia para que se siga leyendo.")]
    [SerializeField] private bool compensateDistance = true;
    [Tooltip("A esta distancia (o menos) el cartel tiene su escala original.")]
    [SerializeField] private float referenceDistance = 6f;
    [SerializeField] private float maxScaleMultiplier = 4f;

    [Header("Orientación")]
    [Tooltip("Hace que el texto mire siempre a la cámara local.")]
    [SerializeField] private bool billboard = true;

    private TextMeshPro tmp;
    private Camera cam;
    private Vector3 baseScale;
    private float baseAlpha;

    private void Awake()
    {
        tmp = GetComponent<TextMeshPro>();
        baseScale = transform.localScale;
        baseAlpha = tmp.alpha;
        SetupSeeThroughMaterial();
    }

    private void SetupSeeThroughMaterial()
    {
        // fontMaterial crea una instancia propia, así no afecta a otros textos.
        Material mat = tmp.fontMaterial;
        mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always); // ignora la profundidad
        mat.renderQueue = (int)RenderQueue.Overlay;                    // se dibuja al final
    }

    private void OnDestroy()
    {
        // Libera la instancia de material creada en Awake.
        if (tmp != null && tmp.fontMaterial != null)
            Destroy(tmp.fontMaterial);
    }

    private void LateUpdate()
    {
        if (cam == null || !cam.isActiveAndEnabled)
        {
            cam = Camera.main; // la cámara del jugador local (tag MainCamera)
            if (cam == null) return;
        }

        Vector3 camPos = cam.transform.position;
        float dist = Vector3.Distance(camPos, transform.position);

        // Fuera de rango: ocultar
        if (dist > maxDistance)
        {
            if (tmp.enabled) tmp.enabled = false;
            return;
        }
        if (!tmp.enabled) tmp.enabled = true;

        // Fade cerca del límite
        float alpha = 1f;
        if (dist > fadeStartDistance)
            alpha = 1f - Mathf.InverseLerp(fadeStartDistance, maxDistance, dist);
        tmp.alpha = baseAlpha * alpha;

        // Tamaño legible a distancia
        if (compensateDistance)
        {
            float mult = Mathf.Clamp(dist / referenceDistance, 1f, maxScaleMultiplier);
            transform.localScale = baseScale * mult;
        }

        // Mirar a la cámara (misma rotación que la cámara = sin texto espejado)
        if (billboard)
            transform.rotation = cam.transform.rotation;
    }
}