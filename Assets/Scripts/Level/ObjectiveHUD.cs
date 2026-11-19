using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

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
    //n
    [Header("Filas (para ocultar texto + icono juntos)")]
    [SerializeField] private GameObject titleRow;
    [SerializeField] private GameObject distanceRow;
    [Tooltip("Opcional: CanvasGroup del panel para ocultar también el fondo.")]
    [SerializeField] private CanvasGroup panelGroup;
    //n
    [Header("Escenas")]
    [SerializeField] private string extractionSceneName = "ExtraccionScene";
    [SerializeField] private string collectionSceneName = "EstacionRecoleccion";

    [SerializeField] private string survivalSceneName = "SupervivenciaScene";

    [Header("Estilo (mismo look que el menú)")]
    [Tooltip("Aplica la tipografía y los colores del menú principal a este HUD.")]
    [SerializeField] private bool applyMenuStyle = true;

    private static readonly Color StyleRed = new Color(0.80f, 0.12f, 0.12f, 1f);
    private static readonly Color StyleRedHover = new Color(0.95f, 0.25f, 0.15f, 1f);
    private static readonly Color StyleGreen = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color StyleAmber = new Color(0.95f, 0.78f, 0.28f, 1f);
    private static readonly Color StyleText = new Color(0.93f, 0.94f, 0.92f, 1f);

    private static readonly Color StyleMuted = new Color(0.55f, 0.57f, 0.56f, 1f);

   // private float descriptionHeight;
    private bool cansFound;
    private FuelGeneratorGroup generatorGroup;
    private float nextGroupSearch;

    private SafeZone cachedZone;
    private Collider cachedZoneCol;
    private ExtractionPhone phone;
    private float nextPhoneSearch;

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
            UpdateZoneObjective("Atravesá la horda de enemigos y llegá al punto seguro");
        else if (scene == extractionSceneName)
            UpdateZoneObjective("Llegá al tren y hacé lo necesario para huir");
        else if (scene == survivalSceneName)
            UpdateZoneObjective("Sobreviví las rondas, salva a los civiles y llegá a la zona segura");
        else if (scene == collectionSceneName)
            UpdateStation();
        else
            Hide();
    }

    // Estación: objetivo general + los pasos de la nafta, en orden. Lo hecho se pinta en verde.
    private void UpdateStation()
    {
        const string goal = "Llegá al tren y haz lo necesario para huir";

        if (generatorGroup == null && Time.unscaledTime >= nextGroupSearch)
        {
            generatorGroup = FindFirstObjectByType<FuelGeneratorGroup>();
            nextGroupSearch = Time.unscaledTime + 1f;
        }

        if (generatorGroup == null || generatorGroup.Total == 0)
        {
            Show(goal, null);
            return;
        }

        int powered = generatorGroup.PoweredCount;
        bool allPowered = powered >= generatorGroup.Total;

        // Una vez que se encontró un bidón (o ya hay generadores cargándose), el paso queda hecho.
        if (!cansFound && (generatorGroup.LocalPlayerHasCan() || powered > 0))
            cansFound = true;

        bool step2Done = allPowered;

        string step1 = Step("1. Encontrá los bidones vacíos que están en las carpas", cansFound, true);
        string step2 = Step($"2. Cargá los generadores con la nafta de los autos ({powered}/{generatorGroup.Total})", step2Done, cansFound);

        FindPhone();
        string escape = EscapeSteps(3, step2Done, out string distance);

        Show($"{goal}\n{step1}\n{step2}\n{escape}", distance);
    }

    // Hecho = verde, el que toca ahora = blanco, el que todavía no = gris.
    private static string Step(string text, bool done, bool current)
    {
        Color color = done ? StyleGreen : (current ? StyleText : StyleMuted);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }

    private void UpdateSuburb()
    {
        SafeZone zone = SafeZone.Current;
        if (zone == null || zone.IsCompleted)
        {
            Hide(); // el panel de SafeZoneUI ya muestra el resumen
            return;
        }

        if (!TryGetZoneDistance(out float distance, out bool inside))
        {
            Show("Llegá a la zona segura", null);
            return;
        }

        if (inside)
            Show("Esperá a tu equipo en la zona segura", null);
        else
            Show("Llegá a la zona segura", $"DISTANCIA: {distance:0} m");
    }

    // Distancia horizontal del jugador LOCAL a la zona segura actual.
    // Devuelve false si no hay zona, si sigue bloqueada o si todavía no existe el jugador.
    private bool TryGetZoneDistance(out float distance, out bool inside)
    {
        distance = 0f;
        inside = false;

        SafeZone zone = SafeZone.Current;
        if (zone == null ) return false;

        if (zone != cachedZone)
        {
            cachedZone = zone;
            cachedZoneCol = zone.GetComponent<Collider>();
        }
        if (cachedZoneCol == null) return false;

        NetworkObject player = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClient?.PlayerObject
            : null;
        if (player == null) return false;

        Vector3 pos = player.transform.position;
        Vector3 closest = cachedZoneCol.ClosestPoint(pos);
        float dx = pos.x - closest.x;
        float dz = pos.z - closest.z;
        distance = Mathf.Sqrt(dx * dx + dz * dz);
        inside = distance < 0.1f;
        return true;
    }

    private float DistanceToLocalPlayer(Vector3 target)
    {
        NetworkObject player = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClient?.PlayerObject
            : null;
        if (player == null) return -1f;

        Vector3 p = player.transform.position;
        float dx = p.x - target.x;
        float dz = p.z - target.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // Objetivo genérico: si el mapa tiene teléfono de extracción, guía por fases
    // (panel -> llamar -> resistir -> zona segura). Si no, va directo a la zona segura.
    private void FindPhone()
    {
        if (phone == null && Time.unscaledTime >= nextPhoneSearch)
        {
            phone = FindFirstObjectByType<ExtractionPhone>();
            nextPhoneSearch = Time.unscaledTime + 1f;
        }
    }

    private void UpdateZoneObjective(string goal)
    {
        SafeZone zone = SafeZone.Current;
        if (zone != null && zone.IsCompleted)
        {
            Hide();
            return;
        }

        FindPhone();
        string steps = EscapeSteps(1, true, out string distance);
        Show($"{goal}\n{steps}", distance);
    }

    // Pasos de escape: [panel] -> [llamar helicóptero] -> zona segura.
    // 'firstNumber' es el número del primer paso; 'active' = false los deja todos en gris.
    // La distancia apunta al paso actual: teléfono (panel / llamar) o zona segura.
    private string EscapeSteps(int firstNumber, bool active, out string distance)
    {
        distance = null;
        int n = firstNumber;

        bool hasPhone = phone != null && phone.IsSpawned;
        bool hasPanel = hasPhone && phone.HasPanel;
        bool panelDone = !hasPanel || (hasPhone && phone.IsUnlocked);
        bool called = hasPhone && phone.State != ExtractionPhone.ExtractionState.Idle;
        bool hasZone = TryGetZoneDistance(out float zoneDist, out bool inside);

        bool panelCurrent = active && hasPanel && !panelDone;
        bool callCurrent = active && hasPhone && panelDone && !called;
        bool zoneCurrent = active && (!hasPhone || called);

        var lines = new List<string>();

        if (hasPanel)
            lines.Add(Step($"{n++}. Arreglá el panel eléctrico", panelDone, panelCurrent));

        if (hasPhone)
            lines.Add(Step($"{n++}. Llamá al helicóptero para huir", called, callCurrent));

        string zoneText;
        if (inside)
            zoneText = $"{n}. Esperá a tu equipo en la zona segura";
        else if (hasPhone && phone.State == ExtractionPhone.ExtractionState.Requested)
        {
            int secs = Mathf.CeilToInt(phone.SecondsLeft);
            zoneText = $"{n}. Andá a la zona segura y escapá (helicóptero en {secs / 60}:{secs % 60:00})";
        }
        else
            zoneText = $"{n}. Andá a la zona segura y escapá";

        lines.Add(Step(zoneText, false, zoneCurrent));

        if (panelCurrent || callCurrent)
        {
            float d = DistanceToLocalPlayer(phone.transform.position);
            if (d >= 0f) distance = $"DISTANCIA: {d:0} m";
        }
        else if (zoneCurrent && hasZone && !inside)
        {
            distance = $"DISTANCIA: {zoneDist:0} m";
        }

        return string.Join("\n", lines);
    }

    // Muestra titulo + descripcion. La distancia es opcional (null = oculta).
    // 'success' pinta la descripcion en verde (ej. nivel completado).
    private void Show(string description, string distance, bool success = false)
    {
        SetPanelVisible(true);
        SetActive(titleRow != null ? titleRow : (objectiveTitle != null ? objectiveTitle.gameObject : null), true);

        if (objectiveDescription != null)
        {
            objectiveDescription.gameObject.SetActive(true);
            objectiveDescription.text = description;

            if (applyMenuStyle)
                objectiveDescription.color = success ? StyleGreen : StyleText;
        }

        bool hasDistance = !string.IsNullOrEmpty(distance);
        GameObject distanceObj = distanceRow != null ? distanceRow : (distanceText != null ? distanceText.gameObject : null);
        SetActive(distanceObj, hasDistance);
        if (hasDistance && distanceText != null)
            distanceText.text = distance;
    }


    private void Hide()
    {
        SetPanelVisible(false);
        SetActive(titleRow != null ? titleRow : (objectiveTitle != null ? objectiveTitle.gameObject : null), false);
        SetActive(objectiveDescription != null ? objectiveDescription.gameObject : null, false);
        SetActive(distanceRow != null ? distanceRow : (distanceText != null ? distanceText.gameObject : null), false);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelGroup != null)
            panelGroup.alpha = visible ? 1f : 0f;
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
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
        label.characterSpacing = 0f;
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