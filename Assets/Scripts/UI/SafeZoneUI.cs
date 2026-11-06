using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD de la zona segura (US Zona Segura). Va dentro del HUD del Player prefab.
// - Muestra cuantos jugadores vivos estan en la zona segura mientras haya alguno adentro.
// - Al completarse el nivel muestra el panel de "Nivel completado", frena al jugador y libera el cursor.
// - En Suburbio ofrece continuar a Oficinas, y en Oficinas a la estacion, conservando el estado de los jugadores.
//
// Estilo: con "Apply Menu Style" activo usa la misma estetica que el menu principal
// (fuentes de GeneradorMenuPrincipal, paleta roja/verde/ambar, botones transparentes con barra de acento).
public class SafeZoneUI : MonoBehaviour
{
    [Tooltip("Texto tipo 'Zona segura: 1/2 jugadores'. Se oculta cuando no hay nadie adentro.")]
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Opcional: texto tipo 'Civiles rescatados: 1/3 - Puntos: 100'. Se muestra mientras haya civiles en el nivel.")]
    [SerializeField] private TMP_Text rescueText;
    [Tooltip("Panel que se muestra al completar el nivel. Puede tener un boton que llame a ReturnToMainMenu().")]

    [SerializeField] private GameObject rescuePanel;
    [Tooltip("Panel que contiene el texto y los iconos de civiles/puntos. Se oculta junto con ellos.")]


    [SerializeField] private GameObject levelCompletePanel;

    [Header("Estilo (mismo look que el menú)")]
    [Tooltip("Aplica tipografía, colores y botones del menú principal a este HUD.")]
    [SerializeField] private bool applyMenuStyle = true;

    private static readonly Color StyleRed = new Color(0.80f, 0.12f, 0.12f, 1f);
    private static readonly Color StyleRedHover = new Color(0.95f, 0.25f, 0.15f, 1f);
    private static readonly Color StyleGreen = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color StyleAmber = new Color(0.95f, 0.78f, 0.28f, 1f);
    private static readonly Color StyleText = new Color(0.93f, 0.94f, 0.92f, 1f);

    private static Sprite fadeSprite;

    private GameObject continueButton;
    private TMP_Text waitingHostText;
    private bool continueUiBuilt;
    private bool panelStyled;
    private MatchBoardWidget boardWidget;

    private void Awake()
    {
        if (applyMenuStyle)
        {
            StyleLabel(statusText, StyleText, 2f);
            StyleLabel(rescueText, StyleText, 2f);
        }

        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SafeZone.LevelCompleted += OnLevelCompleted;
        MatchStatsService.BoardReady += OnBoardReady;
    }

    private void OnDisable()
    {
        SafeZone.LevelCompleted -= OnLevelCompleted;
        MatchStatsService.BoardReady -= OnBoardReady;
    }

    private void OnBoardReady()
    {
        if (boardWidget != null)
            boardWidget.Apply(MatchStatsService.Current);
    }

    private void Start()
    {
        // Por si el nivel ya se habia completado antes de que se creara este HUD.
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted)
            OnLevelCompleted();
    }

    private void Update()
    {
        UpdateRescueText();

        if (statusText == null) return;

        SafeZone zone = SafeZone.Current;
        bool show = zone != null && !zone.IsCompleted && zone.PlayersInside > 0;
        statusText.gameObject.SetActive(show);
        if (!show) return;

        if (applyMenuStyle)
        {
            statusText.text = zone.AllAlivePlayersInside
                ? "¡TODOS A SALVO!"
                : $"ZONA SEGURA: {zone.PlayersInside}/{zone.AlivePlayers} JUGADORES";
            statusText.color = zone.AllAlivePlayersInside ? StyleGreen : StyleText;
            return;
        }

        statusText.text = zone.AllAlivePlayersInside
            ? "¡Todos a salvo!"
            : $"Zona segura: {zone.PlayersInside}/{zone.AlivePlayers} jugadores";
    }

    /*
    private void UpdateRescueText()
    {
        if (rescueText == null) return;

        SafeZone zone = SafeZone.Current;
        bool show = zone != null && zone.TotalNpcs > 0;
        rescueText.gameObject.SetActive(show);
        if (!show) return;

        rescueText.text = $"CIVILES  {zone.RescuedNpcs}/{zone.TotalNpcs}\nPUNTOS  {zone.TeamScore}";
    }
    */

    private void UpdateRescueText()
    {
        SafeZone zone = SafeZone.Current;
        bool show = zone != null && zone.TotalNpcs > 0;

        // Si hay panel, se prende/apaga todo junto (texto + iconos).
        if (rescuePanel != null)
            rescuePanel.SetActive(show);
        else if (rescueText != null)
            rescueText.gameObject.SetActive(show);

        if (!show || rescueText == null) return;

        rescueText.text = $"CIVILES  {zone.RescuedNpcs}/{zone.TotalNpcs}\nPUNTOS  {zone.TeamScore}";
    }

    private void OnLevelCompleted()
    {
        if (statusText != null)
            statusText.gameObject.SetActive(false);

        StyleLevelCompletePanel();

        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(true);

        // El jugador deja de moverse y puede usar el mouse para los botones del panel.
        NetworkObject localPlayer = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        if (localPlayer != null)
        {
            PMovement movement = localPlayer.GetComponent<PMovement>();
            if (movement != null)
                movement.ResetMovement();

            PlayerInput playerInput = localPlayer.GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = false;

            if (HasNextScene())
                PlayerCarryOver.CaptureLocalOwner(localPlayer.gameObject);
        }

        SetupContinueUi();
        EnsureMatchBoard();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetupContinueUi()
    {
        if (levelCompletePanel == null || !HasNextScene()) return;

        EnsureContinueUi();

        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        if (continueButton != null)
            continueButton.SetActive(isServer);
        if (waitingHostText != null)
            waitingHostText.gameObject.SetActive(!isServer);
    }

    private void EnsureContinueUi()
    {
        if (continueUiBuilt || levelCompletePanel == null) return;
        continueUiBuilt = true;

        Button menuButton = levelCompletePanel.GetComponentInChildren<Button>(true);
        RectTransform menuRect = menuButton != null ? menuButton.GetComponent<RectTransform>() : null;

        if (menuRect != null)
            menuRect.anchoredPosition = new Vector2(menuRect.anchoredPosition.x, -110f);

        TMP_FontAsset panelFont = null;
        if (menuButton != null)
        {
            TMP_Text menuLabel = menuButton.GetComponentInChildren<TMP_Text>(true);
            if (menuLabel != null)
                panelFont = menuLabel.font;
        }

        bool toStation = IsOfficesScene();
        continueButton = CreatePanelButton(
            levelCompletePanel.transform,
            toStation ? "ContinueToStationButton" : "ContinueToOfficesButton",
            applyMenuStyle
                ? (toStation ? "CONTINUAR A LA ESTACION" : "CONTINUAR A OFICINAS")
                : (toStation ? "Continuar a la estacion" : "Continuar a Oficinas"),
            new Vector2(0f, -30f),
            toStation ? ContinueToStation : ContinueToOffices,
            panelFont,
            applyMenuStyle);

        GameObject waitingGo = new GameObject("WaitingHostText", typeof(RectTransform));
        waitingGo.transform.SetParent(levelCompletePanel.transform, false);
        RectTransform waitingRect = waitingGo.GetComponent<RectTransform>();
        waitingRect.anchorMin = new Vector2(0.5f, 0.5f);
        waitingRect.anchorMax = new Vector2(0.5f, 0.5f);
        waitingRect.pivot = new Vector2(0.5f, 0.5f);
        waitingRect.anchoredPosition = new Vector2(0f, -30f);
        waitingRect.sizeDelta = new Vector2(420f, 40f);

        waitingHostText = waitingGo.AddComponent<TextMeshProUGUI>();
        waitingHostText.text = applyMenuStyle ? "ESPERANDO AL HOST..." : "Esperando al host...";
        waitingHostText.alignment = TextAlignmentOptions.Center;
        waitingHostText.fontSize = 22f;
        waitingHostText.color = Color.white;
        if (panelFont != null)
            waitingHostText.font = panelFont;
        if (applyMenuStyle)
            StyleLabel(waitingHostText, StyleAmber, 3f);

        waitingGo.SetActive(false);
        if (continueButton != null)
            continueButton.SetActive(false);
    }

    private void EnsureMatchBoard()
    {
        if (levelCompletePanel == null)
            return;

        if (boardWidget == null)
        {
            boardWidget = MatchBoardWidget.Create(
                levelCompletePanel.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -350f),
                new Vector2(860f, 340f));
        }

        boardWidget.Apply(MatchStatsService.Current);
    }

    private static GameObject CreatePanelButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction onClick,
        TMP_FontAsset font,
        bool menuStyle)
    {
        GameObject buttonGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(360f, 70f);

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        Button button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text (TMP)", typeof(RectTransform));
        textGo.transform.SetParent(buttonGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (font != null)
            tmp.font = font;

        // El botón de "continuar" es la acción principal del panel.
        if (menuStyle)
            StyleButton(button, tmp, true);

        return buttonGo;
    }

    private static bool IsSuburbScene()
    {
        return SceneManager.GetActiveScene().name == PlayerCarryOver.SuburbSceneName;
    }

    private static bool IsOfficesScene()
    {
        return SceneManager.GetActiveScene().name == PlayerCarryOver.OfficesSceneName;
    }

    private static bool HasNextScene()
    {
        return IsSuburbScene() || IsOfficesScene();
    }

    // Solo el host. Captura estado de red y carga Oficinas para todos (CA1 + CA4).
    public void ContinueToOffices()
    {
        ContinueTo(PlayerCarryOver.OfficesSceneName);
    }

    // Solo el host. Al terminar Oficinas sigue la cadena hacia la estacion.
    public void ContinueToStation()
    {
        ContinueTo(PlayerCarryOver.StationSceneName);
    }

    private static void ContinueTo(string sceneName)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer) return;

        NetworkObject localPlayer = networkManager.LocalClient?.PlayerObject;
        if (localPlayer != null)
            PlayerCarryOver.CaptureLocalOwner(localPlayer.gameObject);

        PlayerCarryOver.CaptureServerState();
        GameSessionManager.StartGame(sceneName);
    }

    // Para el boton "Volver al menu" del panel.
    public void ReturnToMainMenu()
    {
        DisconnectionHandler handler = FindFirstObjectByType<DisconnectionHandler>();
        if (handler != null)
            handler.LeaveSession();
        else
            GameSessionManager.ReturnToMainMenu();
    }

    // ============================================================
    // ESTILO
    // ============================================================

    // Estiliza los textos y botones que ya trae el panel de "Nivel completado".
    // No cambia posiciones ni tamaños: eso sigue definido en el prefab.
    private void StyleLevelCompletePanel()
    {
        if (!applyMenuStyle || panelStyled || levelCompletePanel == null)
            return;

        panelStyled = true;

        Image panelImage = levelCompletePanel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = new Color(0.03f, 0.035f, 0.04f, 0.82f);

        // Si hay un botón de "continuar", el de volver al menú pasa a ser secundario.
        bool menuIsPrimary = !HasNextScene();

        foreach (Button button in levelCompletePanel.GetComponentsInChildren<Button>(true))
            StyleButton(button, button.GetComponentInChildren<TMP_Text>(true), menuIsPrimary);

        foreach (TMP_Text text in levelCompletePanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Button>() != null)
                continue; // las etiquetas de los botones las maneja StyleButton

            StyleLabel(text, StyleText, 2f);
        }
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

    // Botón transparente con degradado, barra de acento a la izquierda y hover animado.
    private static void StyleButton(Button button, TMP_Text label, bool primary)
    {
        if (button == null)
            return;

        Image background = button.targetGraphic as Image;
        if (background == null)
            background = button.GetComponent<Image>();
        if (background == null)
            return;

        if (button.transform.Find("Barra_Acento") != null)
            return; // ya estilizado

        Color bgRest = primary
            ? new Color(StyleRed.r, StyleRed.g, StyleRed.b, 0.30f)
            : new Color(1f, 1f, 1f, 0.06f);
        Color bgHover = primary
            ? new Color(StyleRedHover.r, StyleRedHover.g, StyleRedHover.b, 0.62f)
            : new Color(StyleRed.r, StyleRed.g, StyleRed.b, 0.40f);
        Color barRest = primary ? StyleRed : new Color(1f, 1f, 1f, 0.28f);
        Color textRest = primary ? Color.white : StyleText;

        background.sprite = GetFadeSprite();
        background.type = Image.Type.Simple;
        background.color = bgRest;
        background.canvasRenderer.cullTransparentMesh = false;

        button.transition = Selectable.Transition.None;

        GameObject barGo = new GameObject("Barra_Acento", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(button.transform, false);
        barGo.transform.SetAsFirstSibling(); // detrás del texto

        Image bar = barGo.GetComponent<Image>();
        bar.color = barRest;
        bar.raycastTarget = false;

        RectTransform barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(3f, -18f);

        StyleLabel(label, textRest, 2f);

        button.gameObject.AddComponent<PanelButtonHover>().Setup(
            button, background, bar, barRect, label,
            bgRest, bgHover, barRest, StyleRedHover, textRest, Color.white);
    }

    // Sprite blanco 256x1 cuyo alpha baja de 1 a 0 (izquierda -> derecha).
    private static Sprite GetFadeSprite()
    {
        if (fadeSprite != null)
            return fadeSprite;

        const int width = 256;
        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[width];
        for (int i = 0; i < width; i++)
        {
            float u = i / (float)(width - 1);
            pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt((1f - u) * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        fadeSprite = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 100f);
        return fadeSprite;
    }

    // Hover animado: resalte con degradado y barra de acento que se ensancha.
    private sealed class PanelButtonHover :
        MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float Speed = 9f;

        private Button button;
        private Image background;
        private Image bar;
        private RectTransform barRect;
        private TMP_Text label;
        private Color bgRest, bgHover, barRest, barHover, textRest, textHover;

        private float t;
        private bool hovered;
        private float lastK = -1f;
        private bool lastEnabled = true;

        public void Setup(
            Button button, Image background, Image bar, RectTransform barRect, TMP_Text label,
            Color bgRest, Color bgHover, Color barRest, Color barHover,
            Color textRest, Color textHover)
        {
            this.button = button;
            this.background = background;
            this.bar = bar;
            this.barRect = barRect;
            this.label = label;
            this.bgRest = bgRest;
            this.bgHover = bgHover;
            this.barRest = barRest;
            this.barHover = barHover;
            this.textRest = textRest;
            this.textHover = textHover;

            Apply(0f, button.interactable);
        }

        private void OnEnable()
        {
            // Al reabrir el panel no debe quedar el botón "pegado" en hover.
            hovered = false;
            t = 0f;
            lastK = -1f;

            if (button != null)
                Apply(0f, button.interactable);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.interactable)
                hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
        }

        private void Update()
        {
            if (button == null)
                return;

            bool enabledNow = button.interactable;
            float target = (hovered && enabledNow) ? 1f : 0f;

            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * Speed);

            if (Mathf.Approximately(t, lastK) && enabledNow == lastEnabled)
                return;

            lastK = t;
            lastEnabled = enabledNow;
            Apply(Mathf.SmoothStep(0f, 1f, t), enabledNow);
        }

        private void Apply(float k, bool enabledNow)
        {
            float alpha = enabledNow ? 1f : 0.35f;

            Color cb = Color.Lerp(bgRest, bgHover, k);
            cb.a *= alpha;
            background.color = cb;

            Color cbar = Color.Lerp(barRest, barHover, k);
            cbar.a *= alpha;
            bar.color = cbar;

            barRect.sizeDelta = new Vector2(Mathf.Lerp(3f, 6f, k), barRect.sizeDelta.y);

            if (label != null)
            {
                Color ct = Color.Lerp(textRest, textHover, k);
                ct.a *= alpha;
                label.color = ct;
            }
        }
    }
}
