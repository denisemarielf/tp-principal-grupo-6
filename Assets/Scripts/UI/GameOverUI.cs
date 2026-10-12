using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fin de partida por derrota: cuando todos los jugadores de la sesion estan muertos se muestra "Partida terminada"
// con un boton para volver al menu. Se crea solo en las escenas de juego (las que tienen jugadores con PlayerHealth),
// y cada equipo lo calcula a partir de PlayerHealth.IsDead, que ya esta sincronizado.
public class GameOverUI : MonoBehaviour
{
    private const float CheckInterval = 0.25f;

    private float checkTimer;
    private bool shown;

    // Se engancha una sola vez al arrancar y crea el chequeo en cada escena de juego que se carga.
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

    private void Update()
    {
        if (shown) return;

        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f) return;
        checkTimer = CheckInterval;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        bool nivelCompletado = SafeZone.Current != null && SafeZone.Current.IsCompleted;

        if (AllDead(players) && !nivelCompletado)
            Show();
    }

    // Todos los jugadores muertos. Sin jugadores creados todavia no es derrota.
    private static bool AllDead(PlayerHealth[] players)
    {
        if (players == null || players.Length == 0) return false;

        foreach (PlayerHealth player in players)
        {
            if (player != null && player.IsSpawned && !player.IsDead)
                return false;
        }
        return true;
    }

    private void Show()
    {
        shown = true;
        Debug.Log("GameOverUI: todos los jugadores murieron. Partida terminada.");

        EnsureEventSystem();
        BuildPanel();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildPanel()
    {
        GameObject canvasGo = new GameObject("GameOverCanvas");
        canvasGo.transform.SetParent(transform, false);
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
        title.text = "PARTIDA TERMINADA";
        title.fontSize = 90f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.85f, 0.15f, 0.15f);
        SetRect(title.rectTransform, new Vector2(0f, 120f), new Vector2(1400f, 160f));

        TextMeshProUGUI subtitle = CreateChild<TextMeshProUGUI>("Subtitulo", background.transform);
        subtitle.text = "Todos los jugadores murieron";
        subtitle.fontSize = 44f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = Color.white;
        SetRect(subtitle.rectTransform, new Vector2(0f, 20f), new Vector2(1400f, 80f));

        Image buttonImage = CreateChild<Image>("BotonVolverAlMenu", background.transform);
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        SetRect(buttonImage.rectTransform, new Vector2(0f, -120f), new Vector2(520f, 100f));
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
