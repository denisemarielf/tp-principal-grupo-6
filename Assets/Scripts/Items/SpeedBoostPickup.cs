using Unity.Netcode;
using UnityEngine;

// Pickup de velocidad (US 1.2): el host lo instancia y es el unico que decide quien lo agarra.
// 1. El jugador local detecta que esta cerca y le pide el pickup al host.
// 2. El host valida que el pickup siga disponible y que el jugador este realmente cerca.
// 3. Si es valido, aplica el boost y despawnea el pickup, que desaparece en todas las pantallas.
public class SpeedBoostPickup : NetworkPickup
{
    [SerializeField] private float speedMultiplier = 2f;
    [SerializeField] private float boostDuration = 5f;
    [SerializeField] private float pickupRadius = 1.5f;
    [Tooltip("Margen extra que acepta el host, porque ve la posicion del cliente con un poco de retraso.")]
    [SerializeField] private float serverTolerance = 1f;
    [SerializeField] private float spinSpeed = 90f;

    private bool requested;
    private bool collected;

    private void Update()
    {
        // Solo visual: cada equipo lo gira por su cuenta, por eso el prefab no lleva NetworkTransform.
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (!IsSpawned || requested) return;

        NetworkObject localPlayer = NetworkManager.LocalClient?.PlayerObject;
        if (localPlayer == null) return;

        if (Vector3.Distance(localPlayer.transform.position, transform.position) <= pickupRadius)
        {
            requested = true;
            RequestPickupRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestPickupRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (collected) return;

        if (!NetworkManager.ConnectedClients.TryGetValue(senderId, out NetworkClient client) || client.PlayerObject == null)
        {
            return;
        }

        float distance = Vector3.Distance(client.PlayerObject.transform.position, transform.position);
        if (distance > pickupRadius + serverTolerance)
        {
            Debug.Log($"SpeedBoostPickup: el host rechazo el pedido del cliente {senderId} (distancia {distance:0.0}).");
            PickupRejectedRpc(RpcTarget.Single(senderId, RpcTargetUse.Temp));
            return;
        }

        collected = true;

        PMovement movement = client.PlayerObject.GetComponent<PMovement>();
        if (movement != null)
        {
            movement.ApplySpeedBoost(speedMultiplier, boostDuration);
        }

        Debug.Log($"SpeedBoostPickup: el cliente {senderId} agarro el boost.");
        NotifyCollected();
        NetworkObject.Despawn(true);
    }

    // Permite volver a intentarlo si el host rechazo el pedido.
    [Rpc(SendTo.SpecifiedInParams)]
    private void PickupRejectedRpc(RpcParams rpcParams)
    {
        requested = false;
    }
}
