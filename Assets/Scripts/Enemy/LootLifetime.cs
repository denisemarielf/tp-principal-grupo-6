using Unity.Netcode;
using UnityEngine;

// Se agrega en runtime solo a los drops de enemigos (lo hace EnemyHealth). Solo actua en el servidor.
public class LootLifetime : MonoBehaviour
{
    private NetworkObject netObj;
    private float timeLeft;

    public void Begin(NetworkObject target, float seconds)
    {
        netObj = target;
        timeLeft = seconds;
    }

    private void Update()
    {
        // Si ya lo recogieron (o se despawneo por otro motivo), no hay nada que hacer.
        if (netObj == null || !netObj.IsSpawned)
        {
            Destroy(this);
            return;
        }

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
            netObj.Despawn(true);
    }
}
