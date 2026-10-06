using System;
using Unity.Netcode;

// Base de los pickups que maneja PickupSpawner (speed boost, botiquin, ...).
// Collected solo se dispara en el host, cuando el pickup se entrega; el spawner lo usa para programar el respawn.
public abstract class NetworkPickup : NetworkBehaviour
{
    public event Action<NetworkPickup> Collected;

    protected void NotifyCollected()
    {
        Collected?.Invoke(this);
    }
}
