using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Teléfono de extracción (CA1, CA3, CA4, CA7, CA8).
/// - El jugador lo mira de cerca y pulsa la tecla de interacción.
/// - El server acepta UNA sola solicitud (CA8) y guarda el momento en una NetworkVariable.
/// - Pasados 'survivalTime' segundos llega el helicóptero (el server lo hace arrancar antes,
///   descontando lo que dura el vuelo) y al llegar se desbloquea la zona segura.
/// - Sobre el teléfono se dibuja un marcador de objetivo visible a través de paredes (estilo L4D2).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class ExtractionPhone : NetworkBehaviour
{
    public enum ExtractionState { Idle, Requested, Arrived }

    [Header("Referencias")]
    [SerializeField] private HelicopterEscapeNetworked helicopter;
    [SerializeField] private SafeZone safeZone;
    [Tooltip("Panel eléctrico que hay que resolver antes de poder llamar al helicóptero. Vacío = siempre disponible.")]
    [SerializeField] private ElectricPanel requiredPanel;

    [Header("Extracción")]
    [Tooltip("[X] segundos que hay que resistir desde que se solicita hasta que llega el helicóptero")]
    [SerializeField] private float survivalTime = 60f;

    [Header("Interacción")]

    [SerializeField] private float interactDistance = 3f;


    [Header("Marcador de objetivo")]
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private string objectiveText = "OBJETIVO: Llamar al helicóptero";
    [SerializeField] private Color markerColor = new Color(1f, 0.8f, 0.1f);
    [SerializeField] private string lockedText = "Sin energía: repara el panel eléctrico";
    [SerializeField] private Color lockedColor = new Color(1f, 0.3f, 0.2f);

    [Header("Avisos")]
    [SerializeField] private string requestedText = "Helicóptero en camino";
    [SerializeField] private string arrivedText = "¡El helicóptero llegó! Ve a la zona segura";
    [SerializeField] private float arrivedMessageTime = 6f;
    [SerializeField] private ObjectiveHighlight highlight;

    [Header("Eventos (se disparan en cada cliente)")]
    public UnityEvent onRequested;
    public UnityEvent onArrivedState;

    private readonly NetworkVariable<ExtractionState> state =
        new NetworkVariable<ExtractionState>(ExtractionState.Idle);
    private readonly NetworkVariable<double> requestTime = new NetworkVariable<double>(-1.0);

    private bool heliStarted;                 // solo server
    private float arrivedAt = -100f;          // local, para ocultar el aviso
    private GUIStyle labelStyle;

    private static readonly List<ExtractionPhone> all = new List<ExtractionPhone>();

    [SerializeField] private string promptKeyLabel = "E";   // solo para el texto del marcador


    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);
    public static ExtractionPhone FindNearestAvailable(Vector3 from)
    {
        ExtractionPhone best = null;
        float bestDist = float.MaxValue;

        foreach (var p in all)
        {
            if (p == null || !p.IsSpawned || p.state.Value != ExtractionState.Idle || !p.IsUnlocked) continue;

            float d = Vector3.Distance(from, p.transform.position);
            if (d <= p.interactDistance && d < bestDist)
            {
                bestDist = d;
                best = p;
            }
        }
        return best;
    }
    public ExtractionState State => state.Value;
    public bool IsUnlocked => requiredPanel == null || requiredPanel.IsSolved;



    // ---------------------------------------------------------------- Netcode

    public override void OnNetworkSpawn()
    {
        state.OnValueChanged += OnStateChanged;
        if (requiredPanel != null) requiredPanel.onSolved.AddListener(RefreshHighlight);
        RefreshHighlight();
    }

    public override void OnNetworkDespawn()
    {

        state.OnValueChanged -= OnStateChanged;
        if (requiredPanel != null) requiredPanel.onSolved.RemoveListener(RefreshHighlight);
    }

    private void RefreshHighlight()
    {
        if (highlight != null)
            highlight.SetHighlighted(state.Value == ExtractionState.Idle && IsUnlocked);
    }

    private void OnStateChanged(ExtractionState previous, ExtractionState current)
    {
        RefreshHighlight();
        if (current == ExtractionState.Requested) onRequested?.Invoke();
        if (current == ExtractionState.Arrived)
        {
            arrivedAt = Time.time;
            onArrivedState?.Invoke();
        }
    }

    // CA8: el server solo acepta la solicitud si todavía está en Idle. No existe cancelación.
    [ServerRpc(RequireOwnership = false)]
    private void RequestExtractionServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log($"ServerRpc en '{name}' desde cliente {rpcParams.Receive.SenderClientId}", this);
        if (state.Value != ExtractionState.Idle) return;
        if (!IsUnlocked) return;   // el panel eléctrico todavía no se resolvió

        state.Value = ExtractionState.Requested;
        requestTime.Value = NetworkManager.ServerTime.Time;
    }

    /// <summary>
    /// Conectar en el UnityEvent "onArrived" del helicóptero. Solo actúa en el server.
    /// </summary>
    public void OnHelicopterArrived()
    {

        if (!IsServer || state.Value != ExtractionState.Requested) return;

        state.Value = ExtractionState.Arrived;
        if (safeZone != null) safeZone.Unlock();   // CA7
    }
    public void RequestExtraction()
    {

        if (!IsSpawned || state.Value != ExtractionState.Idle || !IsUnlocked) return;
        RequestExtractionServerRpc();   // el server vuelve a validar (CA8)
    }

    // ----------------------------------------------------------------- Update

    private void Update()
    {
        if (!IsSpawned) return;
        if (IsServer) ServerTick();
    }

    private void ServerTick()
    {
        if (state.Value != ExtractionState.Requested || heliStarted) return;

        if (helicopter == null)
        {
            Debug.LogError("ExtractionPhone: 'helicopter' no está asignado en el Inspector");
            return;
        }

        // El helicóptero arranca antes para llegar justo a los 'survivalTime' segundos.
        double startAt = requestTime.Value + Mathf.Max(0f, survivalTime - helicopter.FlightDuration);
        if (NetworkManager.ServerTime.Time >= startAt)
        {
            heliStarted = true;
            helicopter.StartArrival();
            Debug.Log("ExtractionPhone: arrancando helicóptero");
        }
    }

    private bool PlayerCanInteract()
    {
        var localPlayer = NetworkManager.LocalClient?.PlayerObject;
        if (localPlayer == null) return false;

        PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return false;

        return Vector3.Distance(localPlayer.transform.position, transform.position) <= interactDistance;
    }
    // -------------------------------------------------------------------- UI

    private void OnGUI()
    {
        if (!IsSpawned) return;
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }

        DrawNotification();
        if (state.Value == ExtractionState.Idle) DrawObjectiveMarker();
    }

    // Marcador sobre el teléfono: tamaño fijo en pantalla y visible a través de paredes.
    private void DrawObjectiveMarker()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 world = transform.position + markerOffset;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return; // detrás de la cámara

        float dist = Vector3.Distance(cam.transform.position, world);
        bool unlocked = IsUnlocked;
        string text = $"◆\n{(unlocked ? objectiveText : lockedText)}\n{dist:0} m";
        if (unlocked && PlayerCanInteract()) text += $"\n[{promptKeyLabel}] Interactuar";

        Rect r = new Rect(sp.x - 150f, Screen.height - sp.y - 40f, 300f, 100f);
        DrawLabel(r, text, unlocked ? markerColor : lockedColor);
    }

    private void DrawNotification()
    {
        string text = null;

        if (state.Value == ExtractionState.Requested)
        {
            double left = requestTime.Value + survivalTime - NetworkManager.ServerTime.Time;
            int secs = Mathf.Max(0, Mathf.CeilToInt((float)left));
            text = $"{requestedText}\nLlegada en {secs / 60:0}:{secs % 60:00}";
        }
        else if (state.Value == ExtractionState.Arrived && Time.time - arrivedAt < arrivedMessageTime)
        {
            text = arrivedText;
        }

        if (text == null) return;
        DrawLabel(new Rect(Screen.width / 2f - 250f, 40f, 500f, 80f), text, Color.white);
    }

    private void DrawLabel(Rect r, string text, Color color)
    {
        labelStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, labelStyle); // sombra
        labelStyle.normal.textColor = color;
        GUI.Label(r, text, labelStyle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}