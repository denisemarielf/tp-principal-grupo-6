using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// HUD del objetivo del modo activo (US 5.10 CA2).
// Usa tres textos independientes: titulo, descripcion y distancia.
//
// Estilo: con "Apply Menu Style" activo usa la misma estetica que el menu principal
// (fuente de GeneradorMenuPrincipal, titulo en rojo, descripcion clara, distancia en ambar).
public class ObjectiveHUD : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TMP_Text objectiveTitle;
    [SerializeField] private TMP_Text objectiveDescription;
    [SerializeField] private TMP_Text distanceText;

    [Header("Escenas")]
    [SerializeField] private string extractionSceneName = "ExtraccionScene";

    [Header("Estilo (mismo look que el menú)")]
    [Tooltip("Aplica la tipografía y los colores del menú principal a este HUD.")]
    [SerializeField] private bool applyMenuStyle = true;

    private static readonly Color StyleRed = new Color(0.80f, 0.12f, 0.12f, 1f);
    private static readonly Color StyleRedHover = new Color(0.95f, 0.25f, 0.15f, 1f);
    private static readonly Color StyleGreen = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color StyleAmber = new Color(0.95f, 0.78f, 0.28f, 1f);
    private static readonly Color StyleText = new Color(0.93f, 0.94f, 0.92f, 1f);

    private void Awake()
    {
        if (applyMenuStyle)
        {
            StyleLabel(objectiveTitle, StyleRedHover, 4f);
            StyleLabel(objectiveDescription, StyleText, 1f);
            StyleLabel(distanceText, StyleAmber, 2f);
        }

        if (objectiveTitle != null)
            objectiveTitle.text = "OBJETIVO";
    }

    private void Update()
    {
        string scene = SceneManager.GetActiveScene().name;

        if (scene == PlayerCarryOver.SuburbSceneName)
            UpdateSuburb();
        else if (scene == PlayerCarryOver.OfficesSceneName)
            Show("Atravesá la horda de enemigos y llega al punto seguro", null);
        else if (scene == extractionSceneName)
            Show("Llegá al tren y haz lo necesario para huir", null);
        else
            Hide();
    }

    private void UpdateSuburb()
    {
        SafeZone zone = SafeZone.Current;
        if (zone == null)
        {
            Hide();
            return;
        }

        if (zone.IsCompleted)
        {
            Show("¡Nivel completado!", null, true);
            return;
        }

        NetworkObject player = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClient?.PlayerObject
            : null;

        if (player == null)
        {
            Show("Llegá a la zona segura", null);
            return;
        }

        Collider col = zone.GetComponent<Collider>();
        Vector3 pos = player.transform.position;
        Vector3 closest = col.ClosestPoint(pos);

        // Distancia horizontal (ignoramos la altura)
        float dx = pos.x - closest.x;
        float dz = pos.z - closest.z;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);

        if (distance < 0.1f)
            Show("Esperá a tu equipo en la zona segura", null);
        else
            Show("Llegá a la zona segura", $"DISTANCIA: {distance:0} m");
    }

    // Muestra titulo + descripcion. La distancia es opcional (null = oculta).
    // 'success' pinta la descripcion en verde (ej. nivel completado).
    private void Show(string description, string distance, bool success = false)
    {
        SetActive(objectiveTitle, true);

        if (objectiveDescription != null)
        {
            objectiveDescription.gameObject.SetActive(true);
            objectiveDescription.text = description;

            if (applyMenuStyle)
                objectiveDescription.color = success ? StyleGreen : StyleText;
        }

        if (distanceText != null)
        {
            bool hasDistance = !string.IsNullOrEmpty(distance);
            distanceText.gameObject.SetActive(hasDistance);
            if (hasDistance)
                distanceText.text = distance;
        }
    }

    private void Hide()
    {
        SetActive(objectiveTitle, false);
        SetActive(objectiveDescription, false);
        SetActive(distanceText, false);
    }

    private static void SetActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }

    // Solo cambia fuente, color, espaciado y sombra. El tamaño lo define el prefab.
    private static void StyleLabel(TMP_Text label, Color color, float letterSpacing)
    {
        if (label == null)
            return;

        // Misma fuente que el menú principal (si todavía no se abrió el menú, deja la del prefab).
        TMP_FontAsset font = GeneradorMenuPrincipal.FuenteTextoActual;
        if (font != null)
            label.font = font;

        label.color = color;
        label.characterSpacing = letterSpacing;
        label.outlineWidth = 0f;

        Material material = label.fontMaterial;
        if (material != null)
        {
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color32(0, 0, 0, 150));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.05f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
        }
    }
}