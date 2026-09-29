using UnityEngine;

// Marca donde aparecen los jugadores al cargar la escena de juego (ver GameSessionManager).
// Character separa a cada jugador unos metros de este punto para que no aparezcan uno adentro del otro.
public class PlayerSpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
    }
}
