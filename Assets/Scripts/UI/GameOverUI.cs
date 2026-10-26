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
public class GameOverUI : MonoBehaviour
{
    private const float CheckInterval = 0.25f;

    // true mientras se muestra el panel de fin (para que la camara no vuelva a trabar el cursor).
    public static bool IsShown { get; private set; }

    private float checkTimer;
    private bool shown;
    private GameObject panelCanvas;
    private GameObject survivalHud;
    private TextMeshProUGUI hudText;

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
                Show("VICTORIA", $"Sobreviviste\nPuntos: {mode.EndScore}", new Color(0.25f, 0.85f, 0.35f));
                break;
            case SurvivalMatchOutcome.DefeatCivilians:
                Show("DERROTA", "Todos los civiles murieron", new Color(0.85f, 0.15f, 0.15f));
                break;
            default:
                Show("DERROTA", "Todos los jugadores murieron", new Color(0.85f, 0.15f, 0.15f));
                break;
        }
    }

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
        CanvasScaler scaler = survivalHud.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        hudText = CreateChild<TextMeshProUGUI>("Tiempo", survivalHud.transform);
        hudText.fontSize = 36f;
        hudText.alignment = TextAlignmentOptions.Center;
        hudText.color = Color.white;
        RectTransform rect = hudText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(900f, 220f);
    }

    private void UpdateSurvivalHud(SurvivalModeController mode)
    {
        if (hudText == null || mode.IsFinished)
            return;

        int total = Mathf.CeilToInt(Mathf.Max(0f, mode.TimeRemaining));
        int minutes = total / 60;
        int seconds = total % 60;
        SurvivalWaveDirector waves = mode.GetComponent<SurvivalWaveDirector>();
        int round = waves != null ? waves.WaveNumber : 0;
        int zombies = waves != null ? waves.ZombiesRemaining : 0;
        string roundLabel = round > 0 ? round.ToString() : "-";
        hudText.text = $"SUPERVIVENCIA  {minutes:00}:{seconds:00}\nCiviles vivos: {mode.AliveCivilians}\nRonda {roundLabel}\nZombies: {zombies}";
    }

    private void Show(string title, string subtitle, Color titleColor)
    {
        shown = true;
        IsShown = true;
        Debug.Log($"GameOverUI: {title}. {subtitle}");

        EnsureEventSystem();
        BuildPanel(title, subtitle, titleColor);
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
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        Image background = CreateChild<Image>("Fondo", canvasGo.transform);
        background.color = new Color(0f, 0f, 0f, 0.8f);
        Stretch(background.rectTransform);

        TextMeshProUGUI title = CreateChild<TextMeshProUGUI>("Titulo", background.transform);
        title.text = titleText;
        title.fontSize = 90f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = titleColor;
        SetRect(title.rectTransform, new Vector2(0f, 140f), new Vector2(1400f, 160f));

        TextMeshProUGUI subtitle = CreateChild<TextMeshProUGUI>("Subtitulo", background.transform);
        subtitle.text = subtitleText;
        subtitle.fontSize = 44f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = Color.white;
        SetRect(subtitle.rectTransform, new Vector2(0f, 10f), new Vector2(1400f, 160f));

        Image buttonImage = CreateChild<Image>("BotonVolverAlMenu", background.transform);
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        SetRect(buttonImage.rectTransform, new Vector2(0f, -160f), new Vector2(520f, 100f));
        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(ReturnToMainMenu);

        TextMeshProUGUI buttonText = CreateChild<TextMeshProUGUI>("Texto", buttonImage.transform);
        buttonText.text = "Volver al menu";
        buttonText.fontSize = 40f;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;
        Stretch(buttonText.rectTransform);
    }

    private static T CreateChild<T>(string name, Transform parent) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void ReturnToMainMenu()
    {
        DisconnectionHandler handler = FindAnyObjectByType<DisconnectionHandler>();
        if (handler != null)
            handler.LeaveSession();
        else
            GameSessionManager.ReturnToMainMenu();
    }
}
