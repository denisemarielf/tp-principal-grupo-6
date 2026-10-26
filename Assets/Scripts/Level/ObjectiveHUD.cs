using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// HUD del objetivo del modo activo (US 5.10 CA2).
// Usa tres textos independientes: titulo, descripcion y distancia.
public class ObjectiveHUD : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TMP_Text objectiveTitle;
    [SerializeField] private TMP_Text objectiveDescription;
    [SerializeField] private TMP_Text distanceText;

    [Header("Escenas")]
    [SerializeField] private string extractionSceneName = "ExtraccionScene";  

    private void Awake()
    {
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
            Show("¡Nivel completado!", null);
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
    private void Show(string description, string distance)
    {
        SetActive(objectiveTitle, true);

        if (objectiveDescription != null)
        {
            objectiveDescription.gameObject.SetActive(true);
            objectiveDescription.text = description;
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
}