using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD de la zona segura (US Zona Segura). Va dentro del HUD del Player prefab.
// - Muestra cuantos jugadores vivos estan en la zona segura mientras haya alguno adentro.
// - Al completarse el nivel muestra el panel de "Nivel completado", frena al jugador y libera el cursor.
// - En Suburbio (US 4.10) ofrece continuar a Oficinas conservando el estado de los jugadores.
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

    private GameObject continueButton;
    private TMP_Text waitingHostText;
    private bool continueUiBuilt;

    private void Awake()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SafeZone.LevelCompleted += OnLevelCompleted;
    }

    private void OnDisable()
    {
        SafeZone.LevelCompleted -= OnLevelCompleted;
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

            if (IsSuburbScene())
                PlayerCarryOver.CaptureLocalOwner(localPlayer.gameObject);
        }

        SetupContinueUi();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetupContinueUi()
    {
        if (levelCompletePanel == null || !IsSuburbScene()) return;

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

        continueButton = CreatePanelButton(
            levelCompletePanel.transform,
            "ContinueToOfficesButton",
            "Continuar a Oficinas",
            new Vector2(0f, -30f),
            ContinueToOffices,
            panelFont);

        GameObject waitingGo = new GameObject("WaitingHostText", typeof(RectTransform));
        waitingGo.transform.SetParent(levelCompletePanel.transform, false);
        RectTransform waitingRect = waitingGo.GetComponent<RectTransform>();
        waitingRect.anchorMin = new Vector2(0.5f, 0.5f);
        waitingRect.anchorMax = new Vector2(0.5f, 0.5f);
        waitingRect.pivot = new Vector2(0.5f, 0.5f);
        waitingRect.anchoredPosition = new Vector2(0f, -30f);
        waitingRect.sizeDelta = new Vector2(420f, 40f);

        waitingHostText = waitingGo.AddComponent<TextMeshProUGUI>();
        waitingHostText.text = "Esperando al host...";
        waitingHostText.alignment = TextAlignmentOptions.Center;
        waitingHostText.fontSize = 22f;
        waitingHostText.color = Color.white;
        if (panelFont != null)
            waitingHostText.font = panelFont;

        waitingGo.SetActive(false);
        if (continueButton != null)
            continueButton.SetActive(false);
    }

    private static GameObject CreatePanelButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction onClick,
        TMP_FontAsset font)
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
        if (font != null)
            tmp.font = font;

        return buttonGo;
    }

    private static bool IsSuburbScene()
    {
        return SceneManager.GetActiveScene().name == PlayerCarryOver.SuburbSceneName;
    }

    // Solo el host. Captura estado de red y carga Oficinas para todos (CA1 + CA4).
    public void ContinueToOffices()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer) return;

        NetworkObject localPlayer = networkManager.LocalClient?.PlayerObject;
        if (localPlayer != null)
            PlayerCarryOver.CaptureLocalOwner(localPlayer.gameObject);

        PlayerCarryOver.CaptureServerState();
        GameSessionManager.StartGame(PlayerCarryOver.OfficesSceneName);
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
}
