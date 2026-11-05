using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeypadUI : MonoBehaviour
{
    [Header("Estilo")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
    [SerializeField] private Color buttonColor = new Color(0.22f, 0.22f, 0.26f, 1f);
    [SerializeField] private Color textColor = Color.white;

    private GameObject panel;
    private TMP_Text display;

    private CodeKeypad current;
    private string input = "";
    private bool waiting;
    [Header("Sonido")]
[SerializeField] private AudioClip clickClip;   // al tocar un dígito
[SerializeField] private AudioClip errorClip;   // código incorrecto
[SerializeField] private AudioClip openClip;    // opcional: al abrir el teclado
[SerializeField, Range(0f, 1f)] private float volume = 0.8f;

private AudioSource audioSource;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        BuildUI();
    }

    private void Play(AudioClip clip)
{
    if (clip != null) audioSource.PlayOneShot(clip, volume);
}

    // ---------- Construcción procedural ----------

    private void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("KeypadCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel de fondo
        var panelRT = NewRect("Panel", canvasGO.transform);
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(560, 760);
        panelRT.gameObject.AddComponent<Image>().color = panelColor;
        panel = canvasGO;   // se prende y apaga todo el canvas

        // Display
        var displayRT = NewRect("Display", panelRT);
        displayRT.anchorMin = new Vector2(0, 1);
        displayRT.anchorMax = new Vector2(1, 1);
        displayRT.pivot = new Vector2(0.5f, 1);
        displayRT.anchoredPosition = new Vector2(0, -30);
        displayRT.sizeDelta = new Vector2(-60, 120);

        display = displayRT.gameObject.AddComponent<TextMeshProUGUI>();
        display.fontSize = 80;
        display.alignment = TextAlignmentOptions.Center;
        display.color = textColor;
        display.text = "____";

        // Grilla de botones
        var gridRT = NewRect("Grid", panelRT);
        gridRT.anchorMin = Vector2.zero;
        gridRT.anchorMax = Vector2.one;
        gridRT.offsetMin = new Vector2(30, 30);
        gridRT.offsetMax = new Vector2(-30, -170);

        var grid = gridRT.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(155, 125);
        grid.spacing = new Vector2(15, 15);
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 1; i <= 9; i++)
        {
            string d = i.ToString();
            CreateButton(gridRT, d, () => AddDigit(d));
        }
        CreateButton(gridRT, "Borrar", Clear);
        CreateButton(gridRT, "0", () => AddDigit("0"));
        CreateButton(gridRT, "Enter", Submit);

        panel.SetActive(false);
    }

    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var rt = NewRect("Btn_" + label, parent);

        var img = rt.gameObject.AddComponent<Image>();
        img.color = buttonColor;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var textRT = NewRect("Text", rt);
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;

        var tmp = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = label.Length > 1 ? 40 : 60;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;
    }

    // ---------- Lógica ----------

    public bool IsOpenFor(CodeKeypad k) => panel.activeSelf && current == k;

    public void Open(CodeKeypad source)
    {
        current = source;
        input = "";
        waiting = false;
        display.color = textColor;
        UpdateDisplay();
        panel.SetActive(true);
          Play(openClip);
    }

    public void Close()
    {
        if (!panel.activeSelf) return;
        panel.SetActive(false);
        current?.CloseLocal();   // Character reactiva el PlayerInput y bloquea el cursor
        current = null;
    }

    public void AddDigit(string digit)
    {
        if (waiting || input.Length >= 4) return;
        input += digit;
        UpdateDisplay();
         Play(clickClip);
    }

    public void Clear()
    {
        if (waiting) return;
        input = "";
        UpdateDisplay();
         Play(clickClip);
    }

    public void Submit()
    {
        if (waiting || input.Length < 4 || current == null) return;
        waiting = true;
        current.SubmitCode(input);
    }

    public void ShowWrong()
{
    if (!panel.activeSelf) return;
    Play(errorClip);
    StartCoroutine(WrongFeedback());
}

    private IEnumerator WrongFeedback()
    {
        display.color = Color.red;
        yield return new WaitForSeconds(0.6f);
        display.color = textColor;
        waiting = false;
        Clear();
    }

    private void UpdateDisplay() => display.text = input.PadRight(4, '_');

    private void Update()
    {
        if (!panel.activeSelf || Keyboard.current == null) return;

        var kb = Keyboard.current;

        if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }

        // Teclado físico (el PlayerInput está apagado, así que va directo)
        for (int i = 0; i <= 9; i++)
            if (kb[(Key)((int)Key.Digit0 + i)].wasPressedThisFrame)
                AddDigit(i.ToString());

        if (kb.backspaceKey.wasPressedThisFrame) Clear();
        if (kb.enterKey.wasPressedThisFrame) Submit();
    }
}