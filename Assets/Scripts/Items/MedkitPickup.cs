using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Botiquin (US 3.1). Todavia no hay inventario, asi que se consume en el momento con la tecla E.
// 1. Al apretar E, el jugador local le pide al host usar el botiquin mas cercano dentro del radio.
// 2. El host valida que siga disponible y que el jugador este cerca, vivo y sin la vida llena.
// 3. Si es valido, lo cura y despawnea el botiquin, que desaparece en todas las pantallas.
public class MedkitPickup : NetworkPickup
{
    private static readonly List<MedkitPickup> active = new List<MedkitPickup>();

    [SerializeField] private float healAmount = 25f;
    [SerializeField] private float useRadius = 2f;
    [Tooltip("Margen extra que acepta el host, porque ve la posicion del cliente con un poco de retraso.")]
    [SerializeField] private float serverTolerance = 1f;
    [Tooltip("Opcional: cartel tipo 'E - Usar botiquin'. Solo lo ve el jugador local cuando esta en rango y le falta vida.")]
    [SerializeField] private GameObject usePrompt;
    [Tooltip("Opcional: el modelo hijo que gira. Si esta vacio gira todo el botiquin (cartel incluido).")]
    [SerializeField] private Transform spinTarget;
    [SerializeField] private float spinSpeed = 45f;

    private bool consumed;

    public override void OnNetworkSpawn()
    {
        active.Add(this);
        if (usePrompt != null)
            usePrompt.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        active.Remove(this);
    }

    private void Update()
    {
        // Solo visual: cada equipo lo gira por su cuenta, por eso el prefab no lleva NetworkTransform.
        Transform spinning = spinTarget != null ? spinTarget : transform;
        spinning.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (usePrompt != null && IsSpawned)
        {
            bool showPrompt = LocalPlayerCanUse();
            usePrompt.SetActive(showPrompt);
            if (showPrompt)
                FacePromptToCamera();
        }
    }

    // El cartel siempre mira a la camara del jugador local, para que se lea desde cualquier lado.
    private void FacePromptToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        usePrompt.transform.rotation = Quaternion.LookRotation(usePrompt.transform.position - cam.transform.position);
    }

    // Botiquin mas cercano a 'position' dentro de su radio de uso, o null si no hay ninguno.
    public static MedkitPickup FindNearest(Vector3 position)
    {
        MedkitPickup nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (MedkitPickup medkit in active)
        {
            float distance = Vector3.Distance(position, medkit.transform.position);
            if (distance <= medkit.useRadius && distance < nearestDistance)
            {
                nearest = medkit;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public void RequestUse()
    {
        if (!IsSpawned || consumed) return;
        RequestUseRpc();
    }

    [Rpc(SendTo.Server)]
    private void RequestUseRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (consumed) return;

        if (!NetworkManager.ConnectedClients.TryGetValue(senderId, out NetworkClient client) || client.PlayerObject == null)
        {
            return;
        }

        float distance = Vector3.Distance(client.PlayerObject.transform.position, transform.position);
        if (distance > useRadius + serverTolerance)
        {
            Debug.Log($"MedkitPickup: el host rechazo el pedido del cliente {senderId} (distancia {distance:0.0}).");
            return;
        }

        PlayerHealth health = client.PlayerObject.GetComponent<PlayerHealth>();
        if (health == null || health.IsDead || health.CurrentHealth >= health.MaxHealth)
        {
            Debug.Log($"MedkitPickup: el cliente {senderId} no puede usar el botiquin (muerto o con la vida llena).");
            return;
        }

        consumed = true;
        health.Heal(healAmount);

        Debug.Log($"MedkitPickup: el cliente {senderId} uso un botiquin (+{healAmount}).");
        NotifyCollected();
        NetworkObject.Despawn(true);
    }

    private bool LocalPlayerCanUse()
    {
        NetworkObject localPlayer = NetworkManager.LocalClient?.PlayerObject;
        if (localPlayer == null) return false;

        PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
        if (health == null || health.IsDead || health.CurrentHealth >= health.MaxHealth) return false;

        return Vector3.Distance(localPlayer.transform.position, transform.position) <= useRadius;
    }
}
