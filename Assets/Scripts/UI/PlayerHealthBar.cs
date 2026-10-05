using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Barra de vida (US 3.3). Sirve para dos casos:
// - La barra propia del HUD: con autoBindLocalPlayer activo, busca sola al jugador local.
// - La barra de un compañero: TeamHealthPanel la instancia y le asigna el jugador con Bind().
// La barra cambia al instante y una segunda barra "de retraso" la sigue despacio, para que se note el dano.
public class PlayerHealthBar : MonoBehaviour
{
    [Tooltip("Image con Image Type = Filled. Muestra la vida actual.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Opcional: Image Filled detras de la principal, que baja despacio al recibir dano.")]
    [SerializeField] private Image delayedFillImage;
    [Tooltip("Opcional: texto con la vida en numeros (ej. 75 / 100).")]
    [SerializeField] private TMP_Text healthText;
    [Tooltip("Opcional: nombre del jugador (se usa en las barras de los compañeros).")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Opcional: se activa cuando el jugador muere.")]
    [SerializeField] private GameObject deathPanel;

    [Tooltip("Activo en la barra propia. En la barra de los compañeros se desactiva solo al llamar a Bind().")]
    [SerializeField] private bool autoBindLocalPlayer = true;
    [SerializeField] private float delayedFillSpeed = 0.5f;
    [Tooltip("Si esta activo, el color de la barra sale del gradiente (izquierda = sin vida, derecha = vida llena).")]
    [SerializeField] private bool useColorGradient;
    [SerializeField] private Gradient colorByHealth;

    private PlayerHealth target;
    private float targetFill = 1f;

    private void Awake()
    {
        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        if (target == null && autoBindLocalPlayer)
            TryBindLocalPlayer();

        if (delayedFillImage != null)
        {
            // Al curarse sube junto con la barra principal; al recibir dano baja despacio.
            delayedFillImage.fillAmount = delayedFillImage.fillAmount < targetFill
                ? targetFill
                : Mathf.MoveTowards(delayedFillImage.fillAmount, targetFill, delayedFillSpeed * Time.deltaTime);
        }
    }

    private void TryBindLocalPlayer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        NetworkObject localPlayer = networkManager != null ? networkManager.LocalClient?.PlayerObject : null;
        if (localPlayer == null) return;

        PlayerHealth health = localPlayer.GetComponent<PlayerHealth>();
        if (health != null)
            BindTo(health);
    }

    // Muestra la vida de un jugador puntual (lo usa TeamHealthPanel).
    public void Bind(PlayerHealth health)
    {
        autoBindLocalPlayer = false;
        BindTo(health);
    }

    private void BindTo(PlayerHealth health)
    {
        Unbind();
        if (health == null) return;

        target = health;
        if (deathPanel != null)
            deathPanel.SetActive(false);
        if (nameText != null)
            nameText.text = $"Jugador {health.OwnerClientId + 1}";

        target.HealthChanged += OnHealthChanged;
        target.Died += OnDied;
        OnHealthChanged(target.CurrentHealth, target.MaxHealth);
        if (delayedFillImage != null)
            delayedFillImage.fillAmount = targetFill;
        if (target.IsDead) OnDied();
    }

    private void Unbind()
    {
        if (target == null) return;

        target.HealthChanged -= OnHealthChanged;
        target.Died -= OnDied;
        target = null;
    }

    private void OnHealthChanged(float current, float max)
    {
        targetFill = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (fillImage != null)
        {
            fillImage.fillAmount = targetFill;
            if (useColorGradient && colorByHealth != null)
                fillImage.color = colorByHealth.Evaluate(targetFill);
        }

        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void OnDied()
    {
        if (deathPanel != null)
            deathPanel.SetActive(true);
    }
}
