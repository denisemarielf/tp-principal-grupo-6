using System.Collections.Generic;
using UnityEngine;

// Barras de vida de los compañeros (US 3.3). Va dentro del HUD del jugador local:
// crea una barra por cada otro jugador conectado, la saca cuando se desconecta
// y las ordena por id de cliente. La disposicion en pantalla la resuelve el
// Horizontal Layout Group del contenedor.
public class TeamHealthPanel : MonoBehaviour
{
    [Tooltip("Contenedor con Horizontal Layout Group, anclado abajo de la pantalla.")]
    [SerializeField] private RectTransform container;
    [Tooltip("Prefab de la barra de un compañero (PlayerHealthBar).")]
    [SerializeField] private PlayerHealthBar entryPrefab;
    [Tooltip("Cada cuantos segundos se revisa si entro o salio algun jugador.")]
    [SerializeField] private float refreshInterval = 0.5f;

    private readonly Dictionary<PlayerHealth, PlayerHealthBar> entries = new Dictionary<PlayerHealth, PlayerHealthBar>();
    private readonly List<PlayerHealth> toRemove = new List<PlayerHealth>();
    private readonly List<PlayerHealth> teammates = new List<PlayerHealth>();
    private float refreshTimer;

    private void OnDisable()
    {
        foreach (PlayerHealthBar bar in entries.Values)
        {
            if (bar != null) Destroy(bar.gameObject);
        }
        entries.Clear();
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        if (container == null || entryPrefab == null) return;

        teammates.Clear();
        foreach (PlayerHealth health in FindObjectsByType<PlayerHealth>())
        {
            if (health.IsSpawned && !health.IsOwner)
                teammates.Add(health);
        }

        // Jugadores que se desconectaron.
        toRemove.Clear();
        foreach (KeyValuePair<PlayerHealth, PlayerHealthBar> entry in entries)
        {
            if (entry.Key == null || !teammates.Contains(entry.Key))
                toRemove.Add(entry.Key);
        }
        foreach (PlayerHealth health in toRemove)
        {
            if (entries[health] != null) Destroy(entries[health].gameObject);
            entries.Remove(health);
        }

        // Jugadores nuevos.
        foreach (PlayerHealth health in teammates)
        {
            if (entries.ContainsKey(health)) continue;

            PlayerHealthBar bar = Instantiate(entryPrefab, container);
            bar.Bind(health);
            entries.Add(health, bar);
        }

        teammates.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        for (int i = 0; i < teammates.Count; i++)
        {
            entries[teammates[i]].transform.SetSiblingIndex(i);
        }
    }
}
