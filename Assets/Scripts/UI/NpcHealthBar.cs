using UnityEngine;
using UnityEngine.UI;

// Barra de vida flotante sobre el civil (US 5.10 CA3).
// Lee la vida de RescuableNpc, no modifica nada de el.
// Una segunda barra "de retraso" baja despacio para que se note el dano.
// Los colores se eligen en las Images del prefab, el script no los toca.
public class NpcHealthBar : MonoBehaviour
{
    [SerializeField] private RescuableNpc npc;
    [Tooltip("Objeto que contiene toda la barra (el Canvas). Se oculta cuando el civil muere.")]
    [SerializeField] private GameObject barRoot;
    [Tooltip("Imagen de relleno con Image Type = Filled. Cambia al instante.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Opcional: Image Filled detras de la principal, que baja despacio al recibir dano.")]
    [SerializeField] private Image delayedFillImage;
    [SerializeField] private float delayedFillSpeed = 0.5f;

    private Camera cam;
    private bool wasShown;

    private void Awake()
    {
        if (npc == null)
            npc = GetComponentInParent<RescuableNpc>();
    }

    private void LateUpdate()
    {
        if (npc == null || barRoot == null || fillImage == null) return;

        bool show = npc.IsSpawned && !npc.IsDead && npc.MaxHealth > 0f;
        barRoot.SetActive(show);
        if (!show)
        {
            wasShown = false;
            return;
        }

        float pct = Mathf.Clamp01(npc.CurrentHealth / npc.MaxHealth);
        fillImage.fillAmount = pct;

        if (delayedFillImage != null)
        {
            // La primera vez que se muestra arranca igualada a la barra principal.
            if (!wasShown)
                delayedFillImage.fillAmount = pct;
            // Al curarse sube junto con la principal; al recibir dano baja despacio.
            else if (delayedFillImage.fillAmount < pct)
                delayedFillImage.fillAmount = pct;
            else
                delayedFillImage.fillAmount = Mathf.MoveTowards(
                    delayedFillImage.fillAmount, pct, delayedFillSpeed * Time.deltaTime);
        }

        wasShown = true;

        // Siempre mirando a la camara del jugador local.
        if (cam == null) cam = Camera.main;
        if (cam != null)
            barRoot.transform.rotation = cam.transform.rotation;
    }
}