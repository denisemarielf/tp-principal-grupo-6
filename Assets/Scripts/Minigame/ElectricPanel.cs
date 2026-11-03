using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// Panel eléctrico interactuable. Solo un jugador puede usarlo a la vez.
/// Al resolverlo, queda desbloqueado para TODOS (NetworkVariable).
public class ElectricPanel : NetworkBehaviour
{
    [Header("Minijuego")]
    public WireMinigame minigame;
    public GameObject minigameUI;

    [Header("Interacción")]
    public float interactRange = 2.5f;   // distancia máxima del jugador al panel

    [Header("Eventos")]
    public UnityEvent onOpened;    // (opcional) corre solo en el jugador que abre el minijuego
    public UnityEvent onClosed;    // (opcional) corre solo en el jugador que lo cierra
    public UnityEvent onSolved;    // habilitar el botón del helicóptero (corre en todos los clientes)
    public UnityEvent onBusy;      // feedback local: "Panel en uso" (sonido/texto)

    /// true = el jugador local abrió el minijuego, false = lo cerró. Lo escucha Character.
    public static event System.Action<bool> LocalMinigameChanged;

    static readonly List<ElectricPanel> all = new List<ElectricPanel>();
    static ElectricPanel localActive;   // panel abierto en ESTE cliente

    readonly NetworkVariable<bool> inUse = new(false);
    readonly NetworkVariable<bool> solved = new(false);
    ulong occupant;                // solo lo usa el servidor

    public bool IsSolved => solved.Value;

    /// Panel sin resolver más cercano que esté dentro de su rango de interacción.
    public static ElectricPanel FindNearestUnsolved(Vector3 from)
    {
        ElectricPanel best = null;
        float bestDist = float.MaxValue;
        foreach (var p in all)
        {
            if (p == null || p.solved.Value) continue;
            float d = Vector3.Distance(from, p.transform.position);
            if (d <= p.interactRange && d < bestDist) { best = p; bestDist = d; }
        }
        return best;
    }

    public override void OnNetworkSpawn()
    {
        all.Add(this);
        minigameUI.SetActive(false);
        solved.OnValueChanged += OnSolvedChanged;
        if (solved.Value) onSolved.Invoke();   // jugadores que entran tarde

        minigame.onCompleted.AddListener(OnMinigameDone);

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        all.Remove(this);
        solved.OnValueChanged -= OnSolvedChanged;
        if (localActive == this) localActive = null;
        minigame.onCompleted.RemoveListener(OnMinigameDone);
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    void OnSolvedChanged(bool _, bool now) { if (now) onSolved.Invoke(); }

    // ---- Lo llama Character.OnInteraction cuando el panel es lo más cercano ----
    public void Interact()
    {
        if (solved.Value) return;
        if (inUse.Value) { onBusy.Invoke(); return; }
        RequestUseServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestUseServerRpc(ServerRpcParams p = default)
    {
        if (solved.Value || inUse.Value) return;

        inUse.Value = true;
        occupant = p.Receive.SenderClientId;

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { occupant } }
        };
        OpenClientRpc(target);
    }

    [ClientRpc]
    void OpenClientRpc(ClientRpcParams p = default)
    {
        minigame.Setup();
        minigameUI.SetActive(true);
        localActive = this;
        LocalMinigameChanged?.Invoke(true);
        onOpened.Invoke();
    }

    // Botón "Cancelar" o tecla Esc del minijuego
    public void Close()
    {
        if (!minigameUI.activeSelf) return;
        minigameUI.SetActive(false);
        if (localActive == this) localActive = null;
        LocalMinigameChanged?.Invoke(false);
        onClosed.Invoke();
        ReleaseServerRpc();
    }

    /// Llamar desde el código de daño del jugador LOCAL (solo en el Owner).
    public static void CancelIfPlaying()
    {
        if (localActive != null) localActive.Close();
    }

    void OnMinigameDone()
    {
        if (!minigameUI.activeSelf) return;
        minigameUI.SetActive(false);
        if (localActive == this) localActive = null;
        LocalMinigameChanged?.Invoke(false);
        onClosed.Invoke();
        CompleteServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void ReleaseServerRpc(ServerRpcParams p = default)
    {
        if (p.Receive.SenderClientId == occupant) inUse.Value = false;
    }

    [ServerRpc(RequireOwnership = false)]
    void CompleteServerRpc(ServerRpcParams p = default)
    {
        if (!inUse.Value || p.Receive.SenderClientId != occupant) return;
        solved.Value = true;
        inUse.Value = false;
    }

    void OnClientDisconnected(ulong id)
    {
        if (inUse.Value && id == occupant) inUse.Value = false;
    }
}