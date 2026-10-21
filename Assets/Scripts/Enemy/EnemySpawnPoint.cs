using UnityEngine;

// Marca donde puede aparecer un enemigo. Si hay al menos uno en la escena,
// EnemySceneSetup planifica desde estos puntos en vez del plan random de calle.
public class EnemySpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.45f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);
    }
}
