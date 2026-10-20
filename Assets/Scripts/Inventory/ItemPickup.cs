using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Pickup genérico de cualquier ítem del inventario.
// El jugador local pide recogerlo con E; el host valida y lo despawnea.
public class ItemPickup : NetworkPickup
{
    private static readonly List<ItemPickup> active = new List<ItemPickup>();

    [SerializeField] private int itemId;
    [SerializeField] private int initialQuantity = 1;
    [SerializeField] private float useRadius = 2f;
    [Tooltip("Opcional: cartel tipo 'E - Recoger'. Solo lo ve el jugador local cuando está en rango y le cabe en el inventario.")]
    [SerializeField] private GameObject usePrompt;
    [Tooltip("Opcional: el modelo hijo que gira. Si está vacío gira todo el objeto (cartel incluido).")]
    [SerializeField] private Transform spinTarget;
    [SerializeField] private float spinSpeed = 45f;

    private readonly NetworkVariable<int> quantity = new NetworkVariable<int>(1);
    private bool consumed;

    public int ItemId => itemId;
    public float UseRadius => useRadius;

    public override void OnNetworkSpawn()
    {
        active.Add(this);
        if (IsServer) quantity.Value = initialQuantity;
        if (usePrompt != null) usePrompt.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        active.Remove(this);
    }

    private void Update()
    {
        Transform spinning = spinTarget != null ? spinTarget : transform;
        spinning.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (usePrompt != null && IsSpawned)
        {
            bool show = LocalPlayerCanPickup();
            usePrompt.SetActive(show);
            if (show) FacePromptToCamera();
        }
    }

    private void FacePromptToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        usePrompt.transform.rotation = Quaternion.LookRotation(usePrompt.transform.position - cam.transform.position);
    }

    // Pickup más cercano a 'position' dentro de su radio, o null.
    public static ItemPickup FindNearest(Vector3 position)
    {
        ItemPickup nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (ItemPickup pickup in active)
        {
            float distance = Vector3.Distance(position, pickup.transform.position);
            if (distance <= pickup.useRadius && distance < nearestDistance)
            {
                nearest = pickup;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    // Solo servidor.
   

    private bool LocalPlayerCanPickup()
    {
        NetworkObject localPlayer = NetworkManager.LocalClient?.PlayerObject;
        if (localPlayer == null) return false;

        PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return false;

        PlayerInventory inv = localPlayer.GetComponent<PlayerInventory>();
        if (inv == null || !inv.CanAdd(itemId)) return false;

        return Vector3.Distance(localPlayer.transform.position, transform.position) <= useRadius;
    }
    // Solo servidor. Devuelve true si se agregó al menos 1 unidad.
    public bool ServerTryPickup(PlayerInventory inv)
    {
        if (!IsServer || consumed) return false;

        int added = inv.TryAdd(itemId, quantity.Value);
        if (added <= 0) return false;

        if (added >= quantity.Value)
        {
            consumed = true;
            NotifyCollected();
            NetworkObject.Despawn(true);
        }
        else
        {
            quantity.Value -= added;
        }
        return true;
    }

    // Solo servidor. Lo usa el inventario al soltar un ítem.
    public void ServerSetQuantity(int amount)
    {
        if (IsServer) quantity.Value = amount;
    }
}