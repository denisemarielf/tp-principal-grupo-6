using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fin de partida. En Supervivencia muestra el resultado que replica el server (victoria o derrota).
// En el resto de los niveles, si todos los jugadores mueren y el nivel no se completo, muestra la derrota local.
//
// Estilo: misma estetica que el menu principal (GeneradorMenuPrincipal): titulo arriba a la izquierda,
// boton transparente con barra de acento y tarjeta de vidrio para el HUD de supervivencia.
// Las fuentes son las que se asignaron en el Inspector del menu principal.
public class GameOverUI : MonoBehaviour
{
    private const float CheckInterval = 0.25f;

    // ---------- dise�o (mismos valores que el menu principal) ----------
    private const float LeftMargin = 120f;
    private const float TopMargin = 70f;

    private static readonly Color Red = new Color(0.80f, 0.12f, 0.12f, 1f);
    private static readonly Color RedHover = new Color(0.95f, 0.25f, 0.15f, 1f);
    private static readonly Color TextMain = new Color(0.93f, 0.94f, 0.92f, 1f);
    private static readonly Color TextSoft = new Color(0.68f, 0.71f, 0.70f, 1f);
    private static readonly Color Glass = new Color(0.03f, 0.035f, 0.04f, 0.58f);
    private static readonly Color Border = new Color(1f, 1f, 1f, 0.10f);

    private static Sprite fadeSoft;
    private static Sprite fadeStrong;

    // true mientras se muestra el panel de fin (para que la camara no vuelva a trabar el cursor).
    public static bool IsShown { get; private set; }

    private float checkTimer;
    private bool shown;
    private GameObject panelCanvas;
    private GameObject survivalHud;
    private TextMeshProUGUI hudText;
    private MatchBoardWidget boardWidget;

    private void OnEnable()
    {
        MatchStatsService.BoardReady += OnBoardReady;
    }

    private void OnDisable()
    {
        MatchStatsService.BoardReady -= OnBoardReady;
    }

    private void OnBoardReady()
    {
        if (boardWidget != null)
            boardWidget.Apply(MatchStatsService.Current);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameSessionManager.MainMenuSceneName) return;
        if (FindAnyObjectByType<GameOverUI>() != null) return;

        new GameObject("GameOverUI").AddComponent<GameOverUI>();
    }

    private void OnDestroy()
    {
        IsShown = false;
    }

    private void Update()
    {
        if (shown)
        {
            if (panelCanvas != null && !SessionActive())
                HideForDisconnect();
            return;
        }

        SurvivalModeController mode = SurvivalModeController.Current;
        if (mode != null)
        {
            EnsureSurvivalHud();
            UpdateSurvivalHud(mode);
            if (mode.IsFinished && SessionActive())
                ShowSurvival(mode);
            return;
        }

        if (survivalHud != null)
            survivalHud.SetActive(false);

        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f) return;
        checkTimer = CheckInterval;

        if (!SessionActive()) return;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        bool nivelCompletado = SafeZone.Current != null && SafeZone.Current.IsCompleted;

        if (AllDead(players) && !nivelCompletado)
            Show("PARTIDA TERMINADA", "Todos los jugadores murieron", new Color(0.85f, 0.15f, 0.15f));
    }

    private static bool SessionActive()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening && !networkManager.ShutdownInProgress;
    }

    private static bool AllDead(PlayerHealth[] players)
    {
        if (players == null) return false;

        int spawned = 0;
        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned) continue;

            spawned++;
            if (!player.IsDead)
                return false;
        }
        return spawned > 0;
    }

    private void HideForDisconnect()
    {
        Destroy(panelCanvas);
        panelCanvas = null;
        boardWidget = null;
        if (survivalHud != null)
            survivalHud.SetActive(false);
        IsShown = false;
    }

    private void ShowSurvival(SurvivalModeController mode)
    {
        if (survivalHud != null)
            survivalHud.SetActive(false);

        switch (mode.Outcome)
        {
            case SurvivalMatchOutcome.Victory:
                Show("VICTORIA", $"Superaste las {mode.RoundsTarget} rondas\nPuntos: {mode.EndScore}", new Color(0.25f, 0.85f, 0.35f));
                break;
            case SurvivalMatchOutcome.DefeatCivilians:
                Show("DERROTA", "Todos los civiles murieron", new Color(0.85f, 0.15f, 0.15f));
                break;
            default:
                Show("DERROTA", "Todos los jugadores murieron", new Color(0.85f, 0.15f, 0.15f));
                break;
        }
    }

    // ============================================================
    // HUD DE SUPERVIVENCIA (arriba a la derecha)
    // ============================================================

    private void EnsureSurvivalHud()
    {
        if (survivalHud != null)
        {
            survivalHud.SetActive(true);
            return;
        }

        survivalHud = new GameObject("SurvivalHud");
        survivalHud.transform.SetParent(transform, false);
        Canvas canvas = survivalHud.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvas.pixelPerfect = false;
        CanvasScaler scaler = survivalHud.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Tarjeta de vidrio con borde fino y acento rojo.
        Image card = CreateImage(survivalHud.transform, "Tarjeta", Glass);
        card.raycastTarget = false;
        RectTransform cardRect = card.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(1f, 1f);
        cardRect.pivot = new Vector2(1f, 1f);
        cardRect.anchoredPosition = new Vector2(-36f, -28f);
        cardRect.sizeDelta = new Vector2(400f, 220f);
        AddBorder(cardRect);

        hudText = CreateText(card.transform, "Tiempo", "", 30f, TextMain,
            TextAlignmentOptions.TopLeft, false, 1f);
        hudText.enableAutoSizing = false;
        hudText.fontSize = 30f;
        hudText.overflowMode = TextOverflowModes.Overflow;
        RectTransform textRect = hudText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(26f, 18f);
        textRect.offsetMax = new Vector2(-26f, -18f);
    }

    private void UpdateSurvivalHud(SurvivalModeController mode)
    {
        if (hudText == null || mode.IsFinished)
            return;

        SurvivalWaveDirector waves = mode.GetComponent<SurvivalWaveDirector>();
        int round = waves != null ? waves.WaveNumber : 0;
        int zombies = waves != null ? waves.ZombiesRemaining : 0;
        int totalRounds = mode.RoundsTarget;

        string accent = ColorUtility.ToHtmlStringRGB(RedHover);
        string soft = ColorUtility.ToHtmlStringRGB(TextSoft);
        int breakSecs = waves != null ? waves.BreakSecondsLeft : 0;
        bool onBreak = breakSecs > 0 && zombies <= 0;

        // Durante el receso, la línea de zombies se reemplaza por la cuenta regresiva.
        string lastLine = onBreak
            ? $"<color=#{accent}>PRÓXIMA RONDA EN</color>  {breakSecs}"
            : $"<color=#{soft}>ZOMBIES</color>  {zombies}";

        hudText.text =
            $"<size=68%><color=#{accent}><cspace=0.2em>SUPERVIVENCIA</cspace></color></size>\n" +
            $"<size=135%>RONDA {round}/{totalRounds}</size>\n" +
            $"<color=#{soft}>CIVILES VIVOS</color>  {mode.AliveCivilians}\n" +
            lastLine;
    }

    // ============================================================
    // PANTALLA DE FIN
    // ============================================================

    private void Show(string title, string subtitle, Color titleColor)
    {
        shown = true;
        IsShown = true;
        Debug.Log($"GameOverUI: {title}. {subtitle}");

        EnsureEventSystem();
        BuildPanel(title, subtitle, titleColor);
        PauseMenuUI.ForceClose();
        FreezeLocalPlayer();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void FreezeLocalPlayer()
    {
        NetworkObject localPlayer = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        if (localPlayer == null)
            return;

        PMovement movement = localPlayer.GetComponent<PMovement>();
        if (movement != null)
            movement.ResetMovement();

        PlayerInput playerInput = localPlayer.GetComponent<PlayerInput>();
        if (playerInput != null)
            playerInput.enabled = false;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildPanel(string titleText, string subtitleText, Color titleColor)
    {
        GameObject canvasGo = new GameObject("GameOverCanvas");
        canvasGo.transform.SetParent(transform, false);
        panelCanvas = canvasGo;
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvas.pixelPerfect = false;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Velo oscuro (bloquea los clics hacia el juego)
        Image background = CreateImage(canvasGo.transform, "Fondo", new Color(0f, 0f, 0f, 0.65f));
        Stretch(background.rectTransform);

        // Degradado oscuro desde la izquierda
        Image gradient = CreateImage(background.transform, "Degradado_Izquierda", new Color(0.01f, 0.012f, 0.015f, 0.92f));
        gradient.sprite = GetFade(1.6f);
        gradient.raycastTarget = false;
        RectTransform gradientRect = gradient.rectTransform;
        gradientRect.anchorMin = new Vector2(0f, 0f);
        gradientRect.anchorMax = new Vector2(0.7f, 1f);
        gradientRect.offsetMin = Vector2.zero;
        gradientRect.offsetMax = Vector2.zero;

        // Titulo: arriba a la izquierda, del color del resultado
        TextMeshProUGUI title = CreateText(background.transform, "Titulo", titleText, 96f, titleColor,
            TextAlignmentOptions.BottomLeft, true, 3f);
        AnchorTopLeft(title.rectTransform, LeftMargin, TopMargin, 1500f, 120f);

        // Linea de acento
        Image line = CreateImage(background.transform, "Linea_Titulo", titleColor);
        line.raycastTarget = false;
        AnchorTopLeft(line.rectTransform, LeftMargin, TopMargin + 130f, 96f, 5f);

        // Mensaje (puede tener dos lineas: "Sobreviviste / Puntos: N")
        TextMeshProUGUI subtitle = CreateText(background.transform, "Subtitulo", subtitleText.ToUpperInvariant(), 38f, TextMain,
            TextAlignmentOptions.TopLeft, false, 2f);
        AnchorTopLeft(subtitle.rectTransform, LeftMargin, TopMargin + 152f, 1200f, 90f);

        const float boardTop = 290f;
        const float boardHeight = 560f;
        boardWidget = MatchBoardWidget.Create(
            background.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(LeftMargin, -(TopMargin + boardTop)),
            new Vector2(860f, boardHeight));
        boardWidget.Apply(MatchStatsService.Current);

        // Debajo del tablero real, no del alto reservado: si no, el boton queda fuera de la pantalla.
        float cardHeight = boardWidget.GetComponent<RectTransform>().sizeDelta.y;
        Button button = CreateButton(background.transform, "VOLVER AL MENU", titleColor, ReturnToMainMenu);
        AnchorTopLeft(button.GetComponent<RectTransform>(), LeftMargin, TopMargin + boardTop + cardHeight + 24f, 520f, 72f);
    }

    private void ReturnToMainMenu()
    {
        DisconnectionHandler handler = FindAnyObjectByType<DisconnectionHandler>();
        if (handler != null)
            handler.LeaveSession();
        else
            GameSessionManager.ReturnToMainMenu();
    }

    // ============================================================
    // BOTON
    // ============================================================

    // Boton transparente: degradado, barra de acento y hover animado.
    // 'accent' ti�e el boton (rojo en derrota, verde en victoria).
    private static Button CreateButton(Transform parent, string text, Color accent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject("Btn_" + text,
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Color accentHover = Color.Lerp(accent, Color.white, 0.15f);
        Color bgRest = new Color(accent.r, accent.g, accent.b, 0.30f);
        Color bgHover = new Color(accentHover.r, accentHover.g, accentHover.b, 0.62f);

        Image background = buttonObject.GetComponent<Image>();
        background.sprite = GetFade(1f);
        background.color = bgRest;
        background.canvasRenderer.cullTransparentMesh = false;

        // Barra de acento
        Image bar = CreateImage(buttonObject.transform, "Barra_Acento", accent);
        bar.raycastTarget = false;
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(3f, -18f);

        // Texto
        TextMeshProUGUI label = CreateText(buttonObject.transform, "Texto_TMP", text, 38f, Color.white,
            TextAlignmentOptions.MidlineLeft, false, 2f);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(28f, 0f);
        labelRect.offsetMax = new Vector2(-16f, 0f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        buttonObject.AddComponent<ButtonHoverEffect>().Setup(
            button, background, bar, barRect, label, labelRect,
            bgRest, bgHover, accent, accentHover, Color.white, Color.white);

        return button;
    }

    // Hover animado: resalte con degradado, barra que se ensancha y texto desplazado.
    private sealed class ButtonHoverEffect :
        MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float Shift = 14f;
        private const float Speed = 9f;

        private Button button;
        private Image background;
        private Image bar;
        private RectTransform barRect;
        private TextMeshProUGUI label;
        private RectTransform labelRect;
        private Color bgRest, bgHover, barRest, barHover, textRest, textHover;

        private float baseLeft;
        private float t;
        private bool hovered;
        private float lastK = -1f;

        public void Setup(
            Button button, Image background, Image bar, RectTransform barRect,
            TextMeshProUGUI label, RectTransform labelRect,
            Color bgRest, Color bgHover, Color barRest, Color barHover,
            Color textRest, Color textHover)
        {
            this.button = button;
            this.background = background;
            this.bar = bar;
            this.barRect = barRect;
            this.label = label;
            this.labelRect = labelRect;
            this.bgRest = bgRest;
            this.bgHover = bgHover;
            this.barRest = barRest;
            this.barHover = barHover;
            this.textRest = textRest;
            this.textHover = textHover;

            baseLeft = labelRect.offsetMin.x;
            Apply(0f);
        }

        private void OnEnable()
        {
            hovered = false;
            t = 0f;
            lastK = -1f;

            if (button != null)
                Apply(0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
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

            // unscaledDeltaTime: funciona igual aunque Time.timeScale sea 0.
            t = Mathf.MoveTowards(t, hovered ? 1f : 0f, Time.unscaledDeltaTime * Speed);

            if (Mathf.Approximately(t, lastK))
                return;

            lastK = t;
            Apply(Mathf.SmoothStep(0f, 1f, t));
        }

        private void Apply(float k)
        {
            background.color = Color.Lerp(bgRest, bgHover, k);
            bar.color = Color.Lerp(barRest, barHover, k);
            barRect.sizeDelta = new Vector2(Mathf.Lerp(3f, 6f, k), barRect.sizeDelta.y);
            label.color = Color.Lerp(textRest, textHover, k);

            Vector2 min = labelRect.offsetMin;
            min.x = baseLeft + Shift * k;
            labelRect.offsetMin = min;
        }
    }

    // ============================================================
    // TEXTO / FUENTES
    // ============================================================

    private static TMP_FontAsset GetFont(bool title)
    {
        // 1) Las del menu principal (las que asignaste en su Inspector)
        TMP_FontAsset font = title
            ? GeneradorMenuPrincipal.FuenteTituloActual
            : GeneradorMenuPrincipal.FuenteTextoActual;

        // 2) Si el titulo no tiene, usar la fuente de texto del menu
        if (font == null)
            font = GeneradorMenuPrincipal.FuenteTextoActual;

        // 3) Fuente predeterminada de TMP
        if (font == null)
            font = TMP_Settings.defaultFontAsset;

        return font;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string text,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        bool isTitle,
        float letterSpacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.characterSpacing = letterSpacing;
        label.raycastTarget = false;

        TMP_FontAsset font = GetFont(isTitle);
        if (font != null)
            label.font = font;

        // Se achica solo si no entra (nunca por encima del tama�o pedido).
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(10f, size * 0.5f);
        label.fontSizeMax = size;
        label.overflowMode = TextOverflowModes.Overflow;

        // Sin contorno + sombra suave: se lee sobre cualquier fondo.
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

        return label;
    }

    // Sprite blanco 256x1 cuyo alpha baja de 1 a 0 (izquierda -> derecha).
    private static Sprite GetFade(float power)
    {
        bool strong = power > 1.01f;
        Sprite cached = strong ? fadeStrong : fadeSoft;

        if (cached != null)
            return cached;

        const int width = 256;
        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[width];
        for (int i = 0; i < width; i++)
        {
            float u = i / (float)(width - 1);
            byte a = (byte)Mathf.RoundToInt(Mathf.Pow(1f - u, power) * 255f);
            pixels[i] = new Color32(255, 255, 255, a);
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 100f);

        if (strong) fadeStrong = sprite;
        else fadeSoft = sprite;

        return sprite;
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    // Borde de 1.5 px + acento rojo en la esquina superior izquierda.
    private static void AddBorder(RectTransform frame)
    {
        const float g = 1.5f;

        CreateBorderLine(frame, "Borde_Sup", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, g), Border);
        CreateBorderLine(frame, "Borde_Inf", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, g), Border);
        CreateBorderLine(frame, "Borde_Izq", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(g, 0f), Border);
        CreateBorderLine(frame, "Borde_Der", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(g, 0f), Border);
        CreateBorderLine(frame, "Acento_Esquina", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, 3f), Red);
    }

    private static void CreateBorderLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        Image image = CreateImage(parent, name, color);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    // x e y se miden desde la esquina superior izquierda (y hacia abajo).
    private static void AnchorTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}