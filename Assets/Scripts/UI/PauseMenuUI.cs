using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Overlay de la partida. ESC lo abre y lo cierra. No congela el tiempo:
// enemigos, otros jugadores y el movimiento local siguen.
//
// Misma estética que el menú principal (GeneradorMenuPrincipal).
// Las fuentes son las mismas que asignaste en el Inspector del menú principal
// (se guardan en GeneradorMenuPrincipal.FuenteTituloActual / FuenteTextoActual al
// abrir el menú). Si la partida se inicia directo desde la escena de juego en el
// editor, sin pasar antes por el menú, usa la fuente por defecto de TMP.
public class PauseMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // ---------- diseño (mismos valores que el menú principal) ----------
    private const float LeftMargin = 120f;
    private const float TopMargin = 70f;
    private const float CardWidth = 600f;
    private const float ContentY = TopMargin + 240f;

    private static readonly Color Red = new Color(0.80f, 0.12f, 0.12f, 1f);
    private static readonly Color RedHover = new Color(0.95f, 0.25f, 0.15f, 1f);
    private static readonly Color TextMain = new Color(0.93f, 0.94f, 0.92f, 1f);
    private static readonly Color TextSoft = new Color(0.68f, 0.71f, 0.70f, 1f);
    private static readonly Color TextMuted = new Color(0.52f, 0.55f, 0.54f, 1f);
    private static readonly Color Amber = new Color(0.95f, 0.78f, 0.28f, 1f);
    private static readonly Color Green = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color Glass = new Color(0.03f, 0.035f, 0.04f, 0.58f);
    private static readonly Color Border = new Color(1f, 1f, 1f, 0.10f);

    private enum ButtonStyle { Primary, Normal, Secondary }

    private static Sprite fadeSoft;
    private static Sprite fadeStrong;

    private GameObject panel;
    private GameObject pauseCard;
    private GameObject optionsCard;
    private Slider volumeSlider;
    private TextMeshProUGUI volumeValue;
    private TextMeshProUGUI qualityLabel;
    private int pendingQuality;
    private Slider optionsMasterSlider;
    private Slider optionsMusicSlider;
    private Slider optionsSfxSlider;
    private Slider optionsSensitivitySlider;
    private Button saveButton;
    private TextMeshProUGUI saveStatus;
    private bool saveInProgress;
    private bool optionsOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate(scene);
    }

    private static void TryCreate(Scene scene)
    {
        if (scene.name == GameSessionManager.MainMenuSceneName)
        {
            IsOpen = false;
            return;
        }

        if (FindAnyObjectByType<PauseMenuUI>() != null)
            return;

        new GameObject("PauseMenu").AddComponent<PauseMenuUI>();
    }

    private void Awake()
    {
        EnsureEventSystem();
        BuildUi();
        panel.SetActive(false);
        IsOpen = false;
    }

    private void OnDestroy()
    {
        IsOpen = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Lo llama GameOverUI: con la partida terminada la pausa no tiene que quedar abierta.
    public static void ForceClose()
    {
        PauseMenuUI menu = FindAnyObjectByType<PauseMenuUI>();
        if (menu != null && IsOpen)
            menu.Close();
    }

    private void Update()
    {
        if (GameOverUI.IsShown)
        {
            if (IsOpen)
                Close();
            return;
        }

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (IsOpen)
        {
            if (optionsOpen)
                ShowPauseHome();
            else
                Close();
        }
        else
            Open();
    }

    private void Open()
    {
        IsOpen = true;
        ShowPauseHome();
        panel.SetActive(true);
        RefreshSaveUi();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // US 3.6: el boton de guardar es solo del host y se habilita unicamente en una zona segura.
    private void RefreshSaveUi()
    {
        NetworkManager network = NetworkManager.Singleton;
        bool isHost = network != null && network.IsListening && network.IsServer;
        saveButton.gameObject.SetActive(isHost);
        saveStatus.gameObject.SetActive(isHost);
        if (!isHost) return;

        SaveBlockReason reason = SaveGameService.GetBlockReason();
        saveButton.interactable = reason == SaveBlockReason.None && !saveInProgress;
        saveStatus.text = SaveSystem.Describe(reason);
        saveStatus.color = Amber;
    }

    private void SaveGame()
    {
        if (saveInProgress) return;

        saveInProgress = true;
        saveButton.interactable = false;
        saveStatus.text = "Guardando...";
        saveStatus.color = TextMain;

        SaveGameService.RequestSave((success, message) =>
        {
            saveInProgress = false;
            if (this == null) return; // se salio de la partida mientras se guardaba

            RefreshSaveUi();
            saveStatus.text = message;
            saveStatus.color = success ? Green : Amber;
        });
    }

    private void Close()
    {
        IsOpen = false;
        optionsOpen = false;
        panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ExitMatch()
    {
        IsOpen = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameSessionManager.ReturnToMainMenu();
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    // ============================================================
    // CONSTRUCCIÓN DE LA UI
    // ============================================================

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        // Panel raíz + velo (bloquea los clics hacia el juego)
        panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasObject.transform, false);
        Stretch(panel.GetComponent<RectTransform>());

        Image dim = CreateImage(panel.transform, "Velo", new Color(0f, 0f, 0f, 0.40f));
        Stretch(dim.rectTransform);

        // Degradado oscuro desde la izquierda
        Image gradient = CreateImage(panel.transform, "Degradado_Izquierda", new Color(0.01f, 0.012f, 0.015f, 0.92f));
        gradient.sprite = GetFade(1.6f);
        gradient.raycastTarget = false;
        RectTransform gradientRect = gradient.rectTransform;
        gradientRect.anchorMin = new Vector2(0f, 0f);
        gradientRect.anchorMax = new Vector2(0.7f, 1f);
        gradientRect.offsetMin = Vector2.zero;
        gradientRect.offsetMax = Vector2.zero;

        BuildTitle(panel.transform, "PAUSA", "LA PARTIDA CONTINÚA");

        // Tarjeta de vidrio, alineada a la izquierda
        pauseCard = CreateCard(panel.transform, LeftMargin, ContentY, CardWidth);

        BuildVolumeControl(pauseCard.transform);

        CreateSpacer(pauseCard.transform, 6f);

        CreateButton(pauseCard.transform, "OPCIONES", ButtonStyle.Normal, ShowOptions);
        CreateButton(pauseCard.transform, "CONTINUAR", ButtonStyle.Primary, Close);
        saveButton = CreateButton(pauseCard.transform, "GUARDAR PARTIDA", ButtonStyle.Normal, SaveGame);

        // Estado del guardado: con wrap real y alto según el texto.
        saveStatus = CreateText(pauseCard.transform, "Estado_Guardado", "", 18f, Amber,
            TextAlignmentOptions.TopLeft, false, 1f);
        saveStatus.textWrappingMode = TextWrappingModes.Normal;
        saveStatus.enableAutoSizing = false;
        saveStatus.overflowMode = TextOverflowModes.Overflow;
        LayoutElement statusLayout = saveStatus.gameObject.AddComponent<LayoutElement>();
        statusLayout.minHeight = 28f;

        CreateSpacer(pauseCard.transform, 6f);

        CreateButton(pauseCard.transform, "SALIR DE LA PARTIDA", ButtonStyle.Secondary, ExitMatch);

        optionsCard = CreateCard(panel.transform, LeftMargin, ContentY, CardWidth);
        BuildOptions(optionsCard.transform);
        optionsCard.SetActive(false);
    }

    private void BuildTitle(Transform parent, string title, string subtitle)
    {
        TextMeshProUGUI titleText = CreateText(parent, "Texto_Titulo", title, 68f, TextMain,
            TextAlignmentOptions.BottomLeft, true, 3f);
        AnchorTopLeft(titleText.rectTransform, LeftMargin, TopMargin, 1300f, 120f);

        Image line = CreateImage(parent, "Linea_Titulo", Red);
        line.raycastTarget = false;
        AnchorTopLeft(line.rectTransform, LeftMargin, TopMargin + 130f, 96f, 5f);

        TextMeshProUGUI sub = CreateText(parent, "Texto_Subtitulo", subtitle, 22f, TextSoft,
            TextAlignmentOptions.MidlineLeft, false, 6f);
        AnchorTopLeft(sub.rectTransform, LeftMargin, TopMargin + 148f, 1000f, 34f);
    }

    private void BuildVolumeControl(Transform parent)
    {
        GameObject container = new GameObject("Control_Volumen", typeof(RectTransform), typeof(LayoutElement));
        container.transform.SetParent(parent, false);
        SetHeight(container, 66f);

        TextMeshProUGUI label = CreateText(container.transform, "Etiqueta", "VOLUMEN GENERAL", 24f, TextMain,
            TextAlignmentOptions.MidlineLeft, false, 2f);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(-90f, 30f);

        volumeValue = CreateText(container.transform, "Valor",
            Mathf.RoundToInt(GameOptions.Master * 100f) + "%", 24f, RedHover,
            TextAlignmentOptions.MidlineRight, false, 1f);
        RectTransform valueRect = volumeValue.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 1f);
        valueRect.anchoredPosition = Vector2.zero;
        valueRect.sizeDelta = new Vector2(80f, 30f);

        volumeSlider = CreateVolumeSlider(container.transform);
    }

    // Slider por defecto de Unity, restilizado: línea fina, relleno rojo y selector rectangular.
    private Slider CreateVolumeSlider(Transform parent)
    {
        GameObject root = DefaultControls.CreateSlider(new DefaultControls.Resources());
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 2f);
        rootRect.sizeDelta = new Vector2(0f, 26f);

        Transform background = root.transform.Find("Background");
        Transform fillArea = root.transform.Find("Fill Area");
        Transform handleArea = root.transform.Find("Handle Slide Area");

        if (background != null)
        {
            background.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);
            RectTransform rt = background.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 6f);
        }

        if (fillArea != null)
        {
            RectTransform rt = fillArea.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(-14f, 6f);

            Transform fill = fillArea.Find("Fill");
            if (fill != null)
            {
                fill.GetComponent<Image>().color = Red;
                fill.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            }
        }

        if (handleArea != null)
        {
            RectTransform rt = handleArea.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(7f, 0f);
            rt.offsetMax = new Vector2(-7f, 0f);

            Transform handle = handleArea.Find("Handle");
            if (handle != null)
            {
                handle.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.93f, 1f);
                handle.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 0f);
            }
        }

        Slider slider = root.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = GameOptions.Master;
        slider.onValueChanged.AddListener(value =>
        {
            GameOptions.SetMaster(value);
            volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
        });

        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
        return slider;
    }

    private void ShowPauseHome()
    {
        optionsOpen = false;
        if (optionsCard != null)
            optionsCard.SetActive(false);
        if (pauseCard != null)
            pauseCard.SetActive(true);

        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(GameOptions.Master);
            volumeValue.text = Percent(GameOptions.Master);
        }
    }

    private void ShowOptions()
    {
        optionsOpen = true;
        pauseCard.SetActive(false);
        optionsCard.SetActive(true);
        SetSlider(optionsMasterSlider, GameOptions.Master, Percent);
        SetSlider(optionsMusicSlider, GameOptions.Music, Percent);
        SetSlider(optionsSfxSlider, GameOptions.Sfx, Percent);
        SetSlider(optionsSensitivitySlider, GameOptions.Sensitivity, SensitivityText);
        pendingQuality = GameOptions.Quality;
        if (qualityLabel != null)
            qualityLabel.text = QualityText(pendingQuality);
    }

    private static void SetSlider(Slider slider, float value, System.Func<float, string> format)
    {
        if (slider == null)
            return;

        slider.SetValueWithoutNotify(value);
        Transform valueLabel = slider.transform.parent.Find("Valor");
        if (valueLabel != null)
        {
            TextMeshProUGUI text = valueLabel.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.text = format(value);
        }
    }

    private void BuildOptions(Transform parent)
    {
        TextMeshProUGUI heading = CreateText(parent, "Titulo_Opciones", "OPCIONES", 28f, TextMain,
            TextAlignmentOptions.MidlineLeft, false, 2f);
        LayoutElement headingLayout = heading.gameObject.AddComponent<LayoutElement>();
        headingLayout.minHeight = 36f;

        optionsMasterSlider = BuildBoundSlider(parent, "VOLUMEN GENERAL", 0f, 1f, GameOptions.Master, Percent, GameOptions.SetMaster);
        optionsMusicSlider = BuildBoundSlider(parent, "MUSICA", 0f, 1f, GameOptions.Music, Percent, GameOptions.SetMusic);
        optionsSfxSlider = BuildBoundSlider(parent, "EFECTOS", 0f, 1f, GameOptions.Sfx, Percent, GameOptions.SetSfx);
        optionsSensitivitySlider = BuildBoundSlider(parent, "SENSIBILIDAD", GameOptions.MinSensitivity, GameOptions.MaxSensitivity,
            GameOptions.Sensitivity, SensitivityText, GameOptions.SetSensitivity);

        CreateSpacer(parent, 6f);
        pendingQuality = GameOptions.Quality;
        Button qualityButton = CreateButton(parent, QualityText(pendingQuality), ButtonStyle.Normal, CycleQuality);
        qualityLabel = qualityButton.GetComponentInChildren<TextMeshProUGUI>();
        CreateButton(parent, "APLICAR", ButtonStyle.Primary, ApplyPendingQuality);
        CreateButton(parent, "VOLVER", ButtonStyle.Secondary, ShowPauseHome);
    }

    private Slider BuildBoundSlider(
        Transform parent,
        string caption,
        float min,
        float max,
        float value,
        System.Func<float, string> format,
        System.Action<float> onChanged)
    {
        GameObject container = new GameObject("Control_" + caption, typeof(RectTransform), typeof(LayoutElement));
        container.transform.SetParent(parent, false);
        SetHeight(container, 66f);

        CreateText(container.transform, "Etiqueta", caption, 24f, TextMain,
            TextAlignmentOptions.MidlineLeft, false, 2f).rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0f, 30f);

        TextMeshProUGUI valueLabel = CreateText(container.transform, "Valor", format(value), 24f, RedHover,
            TextAlignmentOptions.MidlineRight, false, 1f);
        RectTransform valueRect = valueLabel.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 1f);
        valueRect.anchoredPosition = Vector2.zero;
        valueRect.sizeDelta = new Vector2(90f, 30f);

        TextMeshProUGUI label = container.transform.Find("Etiqueta").GetComponent<TextMeshProUGUI>();
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(-100f, 30f);

        return CreateStyledSlider(container.transform, min, max, value, next =>
        {
            onChanged(next);
            valueLabel.text = format(next);
        });
    }

    private void CycleQuality()
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
            return;

        pendingQuality = (pendingQuality + 1) % names.Length;
        if (qualityLabel != null)
            qualityLabel.text = QualityText(pendingQuality);
    }

    private void ApplyPendingQuality()
    {
        GameOptions.SetQuality(pendingQuality);
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    private static string SensitivityText(float value)
    {
        return value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string QualityText(int level)
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
            return "GRAFICOS";

        int clamped = Mathf.Clamp(level, 0, names.Length - 1);
        return "GRAFICOS: " + names[clamped].ToUpperInvariant();
    }

    private Slider CreateStyledSlider(Transform parent, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        Slider slider = CreateVolumeSlider(parent);
        slider.minValue = min;
        slider.maxValue = max;
        slider.onValueChanged.RemoveAllListeners();
        slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    // ============================================================
    // TARJETA
    // ============================================================

    private GameObject CreateCard(Transform parent, float x, float y, float width)
    {
        GameObject card = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = Glass;

        AnchorTopLeft(card.GetComponent<RectTransform>(), x, y, width, 0f);

        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 30, 30);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = card.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        const float g = 1.5f;
        CreateBorderLine(card.transform, "Borde_Sup", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, g), Border);
        CreateBorderLine(card.transform, "Borde_Inf", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, g), Border);
        CreateBorderLine(card.transform, "Borde_Izq", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(g, 0f), Border);
        CreateBorderLine(card.transform, "Borde_Der", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(g, 0f), Border);
        CreateBorderLine(card.transform, "Acento_Esquina", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, 3f), Red);

        return card;
    }

    private static void CreateBorderLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        GameObject line = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        line.transform.SetParent(parent, false);
        line.GetComponent<LayoutElement>().ignoreLayout = true;

        Image image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Espaciador", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        SetHeight(spacer, height);
    }

    // ============================================================
    // BOTONES
    // ============================================================

    private static Button CreateButton(Transform parent, string text, ButtonStyle style, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject("Btn_" + text,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        SetHeight(buttonObject, 58f);

        Color bgRest, bgHover, barRest, barHover, textRest;

        switch (style)
        {
            case ButtonStyle.Primary:
                bgRest = new Color(Red.r, Red.g, Red.b, 0.30f);
                bgHover = new Color(RedHover.r, RedHover.g, RedHover.b, 0.62f);
                barRest = Red;
                barHover = RedHover;
                textRest = Color.white;
                break;

            case ButtonStyle.Secondary:
                bgRest = new Color(1f, 1f, 1f, 0.02f);
                bgHover = new Color(Red.r, Red.g, Red.b, 0.30f);
                barRest = new Color(1f, 1f, 1f, 0.10f);
                barHover = RedHover;
                textRest = TextMuted;
                break;

            default:
                bgRest = new Color(1f, 1f, 1f, 0.04f);
                bgHover = new Color(Red.r, Red.g, Red.b, 0.40f);
                barRest = new Color(1f, 1f, 1f, 0.28f);
                barHover = RedHover;
                textRest = TextMain;
                break;
        }

        Image background = buttonObject.GetComponent<Image>();
        background.sprite = GetFade(1f);
        background.color = bgRest;
        background.canvasRenderer.cullTransparentMesh = false;

        // Barra de acento
        Image bar = CreateImage(buttonObject.transform, "Barra_Acento", barRest);
        bar.raycastTarget = false;
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(3f, -18f);

        // Texto
        TextMeshProUGUI label = CreateText(buttonObject.transform, "Texto_TMP", text, 28f, textRest,
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
            bgRest, bgHover, barRest, barHover, textRest, Color.white);

        return button;
    }

    // Hover animado: resalte con degradado, barra que se ensancha y texto desplazado.
    // Atenúa el botón cuando está desactivado.
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
        private bool lastEnabled = true;

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
            Apply(0f, button.interactable);
        }

        private void OnEnable()
        {
            // Al reabrir el menú no debe quedar el botón "pegado" en hover.
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

            // unscaledDeltaTime: funciona igual aunque Time.timeScale sea 0.
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

            Color ct = Color.Lerp(textRest, textHover, k);
            ct.a *= alpha;
            label.color = ct;

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
        // 1) Las del menú principal (Bangers / Anton, o las que hayas asignado)
        TMP_FontAsset font = title
            ? GeneradorMenuPrincipal.FuenteTituloActual
            : GeneradorMenuPrincipal.FuenteTextoActual;

        // 2) Si el título no tiene, usar la fuente de texto del menú
        if (font == null)
            font = GeneradorMenuPrincipal.FuenteTextoActual;

        // 3) Opcional: Resources/Fonts/FuenteTitulo y FuenteTexto (si existieran)
        if (font == null && title)
            font = Resources.Load<TMP_FontAsset>("Fonts/FuenteTitulo");

        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts/FuenteTexto");

        // 4) Fuente predeterminada de TMP
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

        // Se achica solo si no entra (nunca por encima del tamaño pedido).
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
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void SetHeight(GameObject go, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
            element = go.AddComponent<LayoutElement>();

        element.minHeight = height;
        element.preferredHeight = height;
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