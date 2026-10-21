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
        // Las de escena quedan kinematicas; primero las bajamos al piso.
        PickupGroundSnap.SnapToGround(transform);

        if (rb == null)
            return;

        // Las armas de la escena se quedan donde estan. Si caen, atraviesan la calle.
        bool placedInScene = NetworkObject != null && NetworkObject.InScenePlaced;
        rb.useGravity = !placedInScene && IsOwner;
        rb.isKinematic = placedInScene || !IsOwner;
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
