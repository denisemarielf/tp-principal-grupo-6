using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Overlay de la partida. ESC lo abre y lo cierra. No congela el tiempo:
// enemigos, otros jugadores y el movimiento local siguen.
public class PauseMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private GameObject panel;
    private Slider volumeSlider;

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

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (IsOpen)
            Close();
        else
            Open();
    }

    private void Open()
    {
        IsOpen = true;
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        panel.SetActive(true);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Close()
    {
        IsOpen = false;
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

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        Image dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(panel.GetComponent<RectTransform>());

        GameObject box = new GameObject("Caja");
        box.transform.SetParent(panel.transform, false);
        Image boxImage = box.AddComponent<Image>();
        boxImage.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(520, 420);

        VerticalLayoutGroup layout = box.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 36, 36);
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(box.transform, "PAUSA", 42, new Color(0.8f, 0.12f, 0.12f), 56);
        CreateLabel(box.transform, "VOLUMEN", 22, new Color(0.9f, 0.9f, 0.85f), 32);
        volumeSlider = CreateVolumeSlider(box.transform);
        CreateButton(box.transform, "CONTINUAR", new Color(0.2f, 0.22f, 0.25f), Close);
        CreateButton(box.transform, "SALIR DE LA PARTIDA", new Color(0.8f, 0.12f, 0.12f), ExitMatch);
    }

    private Slider CreateVolumeSlider(Transform parent)
    {
        GameObject root = new GameObject("Slider", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 28;

        Image background = CreateImage(root.transform, "Fondo", new Color(0.15f, 0.16f, 0.18f));
        Stretch(background.rectTransform);

        RectTransform fillArea = CreateRect(root.transform, "Fill Area");
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(6, 6);
        fillArea.offsetMax = new Vector2(-6, -6);

        Image fill = CreateImage(fillArea, "Fill", new Color(0.8f, 0.12f, 0.12f));
        Stretch(fill.rectTransform);

        RectTransform handleArea = CreateRect(root.transform, "Handle Area");
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(10, 0);
        handleArea.offsetMax = new Vector2(-10, 0);

        Image handle = CreateImage(handleArea, "Handle", new Color(0.9f, 0.9f, 0.85f));
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(16, 0);

        Slider slider = root.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = AudioListener.volume;
        slider.onValueChanged.AddListener(value => AudioListener.volume = value);

        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
        return slider;
    }

    private static void CreateLabel(Transform parent, string text, float fontSize, Color color, float height)
    {
        GameObject labelObject = new GameObject("Texto", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
    }

    private static void CreateButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(text, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 52;

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.15f;
        colors.pressedColor = color * 0.8f;
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        CreateLabel(buttonObject.transform, text, 22, Color.white, 52);
        Stretch(buttonObject.GetComponentInChildren<TextMeshProUGUI>().rectTransform);
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
