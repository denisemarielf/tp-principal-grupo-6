using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// Auto (o cualquier objeto) del que se extrae nafta. Con E, un bidón vacío del inventario pasa a ser un bidón lleno.
[RequireComponent(typeof(NetworkObject))]
public class FuelSource : NetworkBehaviour
{
    private static readonly List<FuelSource> all = new List<FuelSource>();

    [Header("Ítems")]
    [SerializeField] private ItemDefinition emptyCan;
    [SerializeField] private ItemDefinition fullCan;

    [Header("Extracción")]
    [Tooltip("Cuántos bidones se pueden llenar con este auto.")]
    [SerializeField] private int cansAvailable = 1;
    [SerializeField] private float interactDistance = 3f;

    [Header("Marcador")]
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Distancia a la que se ve el marcador sobre el auto.")]
    [SerializeField] private float markerDistance = 80f;
    [SerializeField] private Color markerColor = new Color(1f, 0.8f, 0.1f);
    [SerializeField] private string markerText = "Nafta";
    [SerializeField] private string promptText = "[E] Extraer nafta";
    [SerializeField] private string needCanText = "Seleccioná el bidón vacío en tu inventario";
    [SerializeField] private ObjectiveHighlight highlight;

    [Header("Feedback")]
    [SerializeField] private AudioClip siphonClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Tooltip("Opcional: se prende mientras quede nafta (por ejemplo un brillo o una gota).")]
    [SerializeField] private GameObject[] showWhileFuel;

    [Header("Eventos (se disparan en cada cliente)")]
    public UnityEvent onSiphoned;

    private readonly NetworkVariable<int> remaining = new NetworkVariable<int>(0);

    public bool HasFuel => remaining.Value > 0;

    public override void OnNetworkSpawn()
    {
        all.Add(this);

        if (IsServer)
            remaining.Value = cansAvailable;

        remaining.OnValueChanged += OnRemainingChanged;
        Refresh();
    }

    public override void OnNetworkDespawn()
    {
        all.Remove(this);
        remaining.OnValueChanged -= OnRemainingChanged;
    }

    public static FuelSource FindNearest(Vector3 from)
    {
        FuelSource best = null;
        float bestDistance = float.MaxValue;

        foreach (FuelSource source in all)
        {
            if (source == null || !source.IsSpawned || !source.HasFuel) continue;

            float distance = Vector3.Distance(from, source.transform.position);
            if (distance <= source.interactDistance && distance < bestDistance)
            {
                best = source;
                bestDistance = distance;
            }
        }

        return best;
    }

    public void Interact()
    {
        if (!IsSpawned || !HasFuel) return;

        int slot = FuelInteraction.HeldSlotOf(emptyCan);
        if (slot < 0)
        {
            FuelInteraction.ShowMessage(needCanText);
            return;
        }

        RequestSiphonServerRpc(slot);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSiphonServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (remaining.Value <= 0) return;

        if (emptyCan == null || fullCan == null)
        {
            Debug.LogError("FuelSource: faltan los ítems del bidón en el Inspector.", this);
            return;
        }

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!FuelInteraction.TryGetServerPlayer(sender, transform.position, interactDistance, out PlayerInventory inventory))
            return;

        if (!inventory.ServerReplaceItemInSlot(slotIndex, emptyCan.id, fullCan.id))
        {
            ShowMessageClientRpc(needCanText, TargetOnly(sender));
            return;
        }

        remaining.Value--;
        SiphonedClientRpc();
    }

    [ClientRpc]
    private void SiphonedClientRpc()
    {
        if (siphonClip != null)
            AudioSource.PlayClipAtPoint(siphonClip, transform.position, volume);

        onSiphoned?.Invoke();
    }

    [ClientRpc]
    private void ShowMessageClientRpc(string text, ClientRpcParams rpcParams = default)
    {
        FuelInteraction.ShowMessage(text);
    }

    private static ClientRpcParams TargetOnly(ulong clientId)
    {
        return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
    }

    private void OnRemainingChanged(int previous, int current) => Refresh();

    private void Refresh()
    {
        if (highlight != null)
            highlight.SetHighlighted(HasFuel);

        foreach (GameObject target in showWhileFuel)
            if (target != null) target.SetActive(HasFuel);
    }

    private void OnGUI()
    {
        if (!IsSpawned) return;

        FuelInteraction.DrawMessage();

        if (!HasFuel || !FuelInteraction.LocalPlayerWithin(transform.position, markerDistance)) return;

        Camera cam = Camera.main;
        float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
        string text = $"◆\n{markerText}\n{distance:0} m";

        if (FuelInteraction.LocalPlayerWithin(transform.position, interactDistance))
        {
            text += "\n" + (FuelInteraction.HeldSlotOf(emptyCan) >= 0 ? promptText : needCanText);
        }

        FuelInteraction.DrawMarker(transform.position + markerOffset, text, markerColor);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.12f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
