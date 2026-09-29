using UnityEngine;
using Unity.Netcode;

public class WeaponPickup : NetworkBehaviour
{
    public WeaponType weaponType;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Solo el servidor simula la caída real. Los demás clientes reciben
        // la posición ya resuelta vía NetworkTransform, sin física propia
        // (evita que cada uno vea el arma caer/rebotar distinto).
        if (rb != null)
        {
            rb.isKinematic = !IsOwner;
        }
    }

    public void Consume()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();

        if (netObj != null && netObj.IsSpawned)
        {
            if (netObj.InScenePlaced)
            {
                // Objetos colocados en la escena
                netObj.Despawn(false);
                gameObject.SetActive(false);
            }
            else
            {
                // Objetos creados dinámicamente
                netObj.Despawn(true);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}