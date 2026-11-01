using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Barra de vida (US 3.3). Sirve para dos casos:
// - La barra propia del HUD: con autoBindLocalPlayer activo, busca sola al jugador local.
// - La barra de un compañero: TeamHealthPanel la instancia y le asigna el jugador con Bind().
// La barra cambia al instante y una segunda barra "de retraso" la sigue despacio, para que se note el dano.
//
// Estilo: con "Apply Menu Style" activo, la barra usa la misma estética que el menú principal
// (fuentes de GeneradorMenuPrincipal, paleta verde -> ámbar -> rojo, fondo de vidrio con borde fino).
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

    [Header("Estilo (mismo look que el menú)")]
    [Tooltip("Aplica la tipografía, la paleta y la sombra del menú principal. Si no usás gradiente propio, usa verde -> ámbar -> rojo.")]
    [SerializeField] private bool applyMenuStyle = true;
    [Tooltip("Opcional: el Image de fondo de la barra. Se pinta como 'vidrio' con un borde fino.")]
    [SerializeField] private Image backgroundImage;

    private static readonly Color StyleGreen = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color StyleAmber = new Color(0.95f, 0.78f, 0.28f, 1f);
    private static readonly Color StyleRed = new Color(0.80f, 0.12f, 0.12f, 1f);
    private static readonly Color StyleText = new Color(0.93f, 0.94f, 0.92f, 1f);
    private static readonly Color StyleTextSoft = new Color(0.68f, 0.71f, 0.70f, 1f);
    private static readonly Color StyleGlass = new Color(0.03f, 0.035f, 0.04f, 0.58f);
    private static readonly Color StyleBorder = new Color(1f, 1f, 1f, 0.10f);

    private PlayerHealth target;
    private float targetFill = 1f;

    private void Awake()
    {
        ApplyStyle();

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


    private float nextBindLog;

    private float nextBindTry;

    private void TryBindLocalPlayer()
    {
        if (Time.time < nextBindTry) return;
        nextBindTry = Time.time + 0.5f;

        foreach (PlayerHealth health in FindObjectsByType<PlayerHealth>())
        {
            if (health.IsSpawned && health.IsOwner)
            {
                BindTo(health);
                return;
            }
        }
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
            nameText.text = applyMenuStyle
                ? $"JUGADOR {health.OwnerClientId + 1}"
                : $"Jugador {health.OwnerClientId + 1}";

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

    // ============================================================
    // ESTILO
    // ============================================================

    private void ApplyStyle()
    {
        if (!applyMenuStyle)
            return;

        // Color de la vida: si no hay gradiente propio, usa el del menú
        // (izquierda = sin vida -> rojo, derecha = vida llena -> verde).
        if (!useColorGradient)
        {
            colorByHealth = CreateMenuGradient();
            useColorGradient = true;

            if (fillImage != null)
                fillImage.color = StyleGreen;
        }

        // Barra de retraso: estela clara y translúcida.
        if (delayedFillImage != null)
            delayedFillImage.color = new Color(1f, 1f, 1f, 0.45f);

        // Fondo de vidrio con borde fino.
        if (backgroundImage != null)
        {
            backgroundImage.color = StyleGlass;
            AddBorder(backgroundImage.rectTransform);
        }

        StyleLabel(nameText, StyleTextSoft, 2f);
        StyleLabel(healthText, StyleText, 1f);
    }

    private static Gradient CreateMenuGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(StyleRed, 0f),
                new GradientColorKey(StyleAmber, 0.5f),
                new GradientColorKey(StyleGreen, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }

    // Solo cambia fuente, color, espaciado y sombra. El tamaño lo define el prefab.
    private static void StyleLabel(TMP_Text label, Color color, float letterSpacing)
    {
        if (label == null)
            return;

        // Misma fuente que el menú principal (si todavía no se abrió el menú, deja la del prefab).
        TMP_FontAsset font = GeneradorMenuPrincipal.FuenteTextoActual;
        if (font != null)
            label.font = font;

        label.color = color;
        label.characterSpacing = letterSpacing;
        label.outlineWidth = 0f;

        Material material = label.fontMaterial;
        if (material != null)
        {
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color32(0, 0, 0, 150));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.05f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
        }
    }

    // Borde de 1.5 px alrededor del fondo (no participa de ningún layout).
    private static void AddBorder(RectTransform frame)
    {
        const float g = 1.5f;

        CreateBorderLine(frame, "Borde_Sup", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, g));
        CreateBorderLine(frame, "Borde_Inf", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, g));
        CreateBorderLine(frame, "Borde_Izq", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(g, 0f));
        CreateBorderLine(frame, "Borde_Der", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(g, 0f));
    }

    private static void CreateBorderLine(RectTransform frame, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        GameObject line = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        line.transform.SetParent(frame, false);
        line.GetComponent<LayoutElement>().ignoreLayout = true;

        Image image = line.GetComponent<Image>();
        image.color = StyleBorder;
        image.raycastTarget = false;

        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }
}