using System;
using Unity.Netcode;
using UnityEngine;

public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
{
    public int itemId;    // -1 = vacío
    public int quantity;

    public bool IsEmpty => itemId < 0 || quantity <= 0;
    public static InventorySlot Empty => new InventorySlot { itemId = -1, quantity = 0 };


    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref itemId);
        s.SerializeValue(ref quantity);
    }

    public bool Equals(InventorySlot o) => itemId == o.itemId && quantity == o.quantity;
}

public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] private ItemCatalog catalog;
    [SerializeField] private int slotCount = 4;
    [Tooltip("Margen extra que acepta el host, porque ve la posición del cliente con un poco de retraso.")]
    [SerializeField] private float pickupTolerance = 1f;

    [Header("Feedback")]
    [SerializeField] private InventoryAudio inventoryAudio;

    [Header("Soltar")]
    [SerializeField] private float dropDistance = 1.5f;
    [Tooltip("Altura sobre el suelo a la que queda el pickup (tu botiquín en la escena está a ~0.4).")]
    [SerializeField] private float dropGroundOffset = 0.4f;

    private NetworkList<InventorySlot> slots;

    public event Action InventoryChanged;

    public int LoadedSlotCount => slots.Count;
    public int SlotCount => slotCount;
    public InventorySlot GetSlot(int i) => slots[i];
    public ItemDefinition GetDefinition(int itemId) => catalog.Get(itemId);

    private void Awake()
    {
        slots = new NetworkList<InventorySlot>(
            null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            for (int i = 0; i < slotCount; i++) slots.Add(InventorySlot.Empty);

        slots.OnListChanged += OnListChanged;
        InventoryChanged?.Invoke();
    }

    public override void OnNetworkDespawn() => slots.OnListChanged -= OnListChanged;

    private void OnListChanged(NetworkListEvent<InventorySlot> _) => InventoryChanged?.Invoke();
    /// <summary>True si se puede agregar al menos 1 unidad.</summary>
    public bool CanAdd(int itemId)
    {
        var def = catalog.Get(itemId);
        if (def == null) return false;
        if (def.maxCarried > 0 && CountOf(itemId) >= def.maxCarried) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.IsEmpty) return true;
            if (s.itemId == itemId && s.quantity < def.maxStack) return true;
        }
        return false;
    }
    public int CountOf(int itemId)
    {
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].itemId == itemId) total += slots[i].quantity;
        return total;
    }

    public bool Has(int itemId) => CountOf(itemId) > 0;

    /// <summary>Solo servidor. Devuelve cuántas unidades se agregaron realmente.</summary>
    public int TryAdd(int itemId, int quantity)
    {
        if (!IsServer) return 0;
        var def = catalog.Get(itemId);
        if (def == null) Debug.LogWarning($"[Inv] El catálogo no tiene el ítem con id {itemId}");
        if (def == null || quantity <= 0) return 0;

        if (def.maxCarried > 0)
            quantity = Mathf.Min(quantity, def.maxCarried - CountOf(itemId));
        if (quantity <= 0) return 0;

        int remaining = quantity;

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.itemId != itemId || s.quantity >= def.maxStack) continue;
            int add = Mathf.Min(remaining, def.maxStack - s.quantity);
            s.quantity += add;
            slots[i] = s;
            remaining -= add;
        }

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int add = Mathf.Min(remaining, def.maxStack);
            slots[i] = new InventorySlot { itemId = itemId, quantity = add };
            remaining -= add;
        }

        return quantity - remaining;
    }


    public bool TryRemove(int itemId, int quantity)
    {
        if (!IsServer || CountOf(itemId) < quantity) return false;

        for (int i = slots.Count - 1; i >= 0 && quantity > 0; i--)
        {
            var s = slots[i];
            if (s.itemId != itemId) continue;
            int take = Mathf.Min(quantity, s.quantity);
            s.quantity -= take;
            slots[i] = s.quantity <= 0 ? InventorySlot.Empty : s;
            quantity -= take;
        }
        return true;
    }

    [Rpc(SendTo.Owner)]
    private void PlayFeedbackRpc(InventorySound kind, int itemId)
    {
        if (inventoryAudio == null) return;
        inventoryAudio.Play(kind, catalog.Get(itemId));
    }

    public int FirstOccupiedSlot()
    {
        for (int i = 0; i < slots.Count; i++)
            if (!slots[i].IsEmpty) return i;
        return -1;
    }
    [ServerRpc]
    public void RequestUseItemServerRpc(int itemId)
    {
        var def = catalog.Get(itemId);
        if (def == null || def.useEffect == null || !Has(itemId))
        {
            PlayFeedbackRpc(InventorySound.Fail, itemId);
            return;
        }

        if (!def.useEffect.Apply(gameObject))
        {
            PlayFeedbackRpc(InventorySound.Fail, itemId); // ej: vida llena
            return;
        }

        if (def.consumeOnUse) TryRemove(itemId, 1);
        PlayFeedbackRpc(InventorySound.Use, itemId);
    }
    [ServerRpc]
    public void RequestPickupItemServerRpc(ulong pickupNetworkObjectId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects
                .TryGetValue(pickupNetworkObjectId, out NetworkObject obj)) return;
        if (!obj.TryGetComponent(out ItemPickup pickup)) return;

        float distance = Vector3.Distance(transform.position, obj.transform.position);
        if (distance > pickup.UseRadius + pickupTolerance) return;

        int itemId = pickup.ItemId;
        bool ok = pickup.ServerTryPickup(this);
        PlayFeedbackRpc(ok ? InventorySound.Pickup : InventorySound.Fail, itemId); // Fail = inventario lleno
    }
    [ServerRpc]
    public void RequestDropSlotServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex].IsEmpty)
        {
            PlayFeedbackRpc(InventorySound.Fail, -1);
            return;
        }

        InventorySlot slot = slots[slotIndex];
        var def = catalog.Get(slot.itemId);

        // Validar ANTES de quitar nada, para no perder el ítem si el prefab está mal configurado.
        if (def == null || def.worldPrefab == null || def.worldPrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning($"[Inventario] El ítem {slot.itemId} no tiene un World Prefab válido (con NetworkObject).");
            PlayFeedbackRpc(InventorySound.Fail, slot.itemId);
            return;
        }

        slot.quantity -= 1;
        slots[slotIndex] = slot.quantity <= 0 ? InventorySlot.Empty : slot;

        GameObject go = Instantiate(def.worldPrefab, GetDropPosition(), Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn(true);
        if (go.TryGetComponent(out ItemPickup dropped)) dropped.ServerSetQuantity(1);

        PlayFeedbackRpc(InventorySound.Drop, slot.itemId);
    }

    private Vector3 GetDropPosition()
    {
        Vector3 origin = transform.position + transform.forward * dropDistance + Vector3.up;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * dropGroundOffset;

        return transform.position + transform.forward * dropDistance;
    }
}