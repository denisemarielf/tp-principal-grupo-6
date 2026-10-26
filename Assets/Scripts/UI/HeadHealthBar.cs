using Unity.Netcode;
using UnityEngine;

// Va en el Canvas World Space "headBar", dentro del prefab del jugador.
// Busca solo el PlayerHealth (en el padre) y la PlayerHealthBar (en los hijos).
public class HeadHealthBar : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Debug.Log("HeadBar: soy el dueño, me oculto");
            gameObject.SetActive(false); // el dueño usa su barra del HUD
            return;
        }

        PlayerHealth health = GetComponentInParent<PlayerHealth>();
        PlayerHealthBar bar = GetComponentInChildren<PlayerHealthBar>(true);
        Debug.Log($"HeadBar: health={(health != null)}, bar={(bar != null)}");
        if (health != null && bar != null)
            bar.Bind(health);
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = cam.transform.rotation; // billboard
    }
}