using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Maneja las desconexiones durante una partida (ver Docs/Desconexion.md):
// - Si se va un cliente: el host recibe un aviso, se elimina el personaje y la partida sigue.
// - Si se va el host o se pierde la conexion: la partida finaliza para el cliente,
//   se muestra un aviso con cuenta regresiva y se vuelve al menu.
public class DisconnectionHandler : MonoBehaviour
{
    public static event Action SessionEnded;

    [Header("UI (solo Notice Panel y Notice Text son necesarios)")]
    [SerializeField] private GameObject noticePanel;
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private TMP_Text titleText;        // opcional
    [SerializeField] private TMP_Text countdownText;    // opcional
    [SerializeField] private Button acceptButton;       // opcional (si falta se busca dentro del panel)
    [SerializeField] private CanvasGroup noticeGroup;   // opcional (se agrega solo)

    [Header("Tiempos")]
    [SerializeField] private float remotePlayerLeftNoticeSeconds = 4f;
    [SerializeField] private float autoReturnSeconds = 8f;       // 0 = esperar al boton
    [SerializeField] private float fadeSeconds = 0.35f;
    [SerializeField] private float shutdownTimeoutSeconds = 3f;
    [SerializeField] private int noticeSortingOrder = 500;

    private NetworkManager networkManager;
    private Coroutine hideNoticeRoutine;
    private Coroutine fadeRoutine;
    private bool sessionActive;
    private bool returnToMenuOnDismiss;
    private bool leavingToMenu; // candado contra dobles retornos

    private bool HasNoticeUI => noticePanel != null || noticeText != null;
    private bool styledNoticeBuilt;
    private GameObject styledNoticeRoot;
    private Camera fallbackCamera;

    private void Start()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("DisconnectionHandler: no hay NetworkManager en la escena.", this);
            enabled = false;
            return;
        }

        if (noticeText == null)
        {
            Debug.LogWarning("DisconnectionHandler: falta asignar 'Notice Text', los avisos solo se veran en la consola.", this);
        }

        SetupUI();
        HideNotice();

        // La escena de juego se carga desde el lobby con la sesion ya iniciada.
        sessionActive = networkManager.IsListening;

        networkManager.OnServerStarted += OnSessionStarted;
        networkManager.OnClientStarted += OnSessionStarted;
        networkManager.OnConnectionEvent += OnConnectionEvent;
        networkManager.OnTransportFailure += OnTransportFailure;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnServerStarted -= OnSessionStarted;
            networkManager.OnClientStarted -= OnSessionStarted;
            networkManager.OnConnectionEvent -= OnConnectionEvent;
            networkManager.OnTransportFailure -= OnTransportFailure;
        }

        if (styledNoticeRoot != null)
            Destroy(styledNoticeRoot);
        if (fallbackCamera != null)
            Destroy(fallbackCamera.gameObject);
    }

    // Prepara el panel por codigo para no depender del Inspector.
    private void SetupUI()
    {
        EnsureStyledNotice();

        if (noticePanel == null) return;

        // Canvas propio con orden alto: el aviso siempre queda por encima del HUD y otros paneles.
        Canvas canvas = noticePanel.GetComponent<Canvas>();
        if (canvas == null) canvas = noticePanel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = noticeSortingOrder;
        if (noticePanel.GetComponent<GraphicRaycaster>() == null)
        {
            noticePanel.AddComponent<GraphicRaycaster>();
        }

        if (noticeGroup == null) noticeGroup = noticePanel.GetComponent<CanvasGroup>();
        if (noticeGroup == null) noticeGroup = noticePanel.AddComponent<CanvasGroup>();

        if (acceptButton == null) acceptButton = noticePanel.GetComponentInChildren<Button>(true);
        if (acceptButton != null)
        {
            // Si tambien lo enlazaste en el Inspector no pasa nada: DismissNotice es idempotente.
            acceptButton.onClick.RemoveListener(DismissNotice);
            acceptButton.onClick.AddListener(DismissNotice);
        }
    }

    // El panel de escena es una franja clara. Este overlay usa la misma paleta que el menú.
    private void EnsureStyledNotice()
    {
        if (styledNoticeBuilt)
            return;

        styledNoticeBuilt = true;

        if (noticePanel != null)
            noticePanel.SetActive(false);

        styledNoticeRoot = new GameObject("AvisoDesconexion", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        styledNoticeRoot.transform.SetParent(null, false);

        Canvas canvas = styledNoticeRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = noticeSortingOrder;

        CanvasScaler scaler = styledNoticeRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = styledNoticeRoot.GetComponent<CanvasGroup>();

        RectTransform rootRect = styledNoticeRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = CreateNoticeImage(styledNoticeRoot.transform, "Fondo", new Color(0.02f, 0.02f, 0.025f, 0.78f));
        Stretch(dim.rectTransform);

        Image card = CreateNoticeImage(styledNoticeRoot.transform, "Tarjeta", new Color(0.03f, 0.035f, 0.04f, 0.94f));
        RectTransform cardRect = card.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(760f, 420f);

        Image accent = CreateNoticeImage(card.transform, "Acento", new Color(0.80f, 0.12f, 0.12f, 1f));
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 1f);
        accentRect.anchoredPosition = new Vector2(0f, 0f);
        accentRect.sizeDelta = new Vector2(8f, 420f);
        accent.raycastTarget = false;

        TMP_FontAsset titleFont = GeneradorMenuPrincipal.FuenteTituloActual;
        if (titleFont == null)
            titleFont = GeneradorMenuPrincipal.FuenteTextoActual;
        if (titleFont == null)
            titleFont = TMP_Settings.defaultFontAsset;

        TMP_FontAsset bodyFont = GeneradorMenuPrincipal.FuenteTextoActual;
        if (bodyFont == null)
            bodyFont = TMP_Settings.defaultFontAsset;

        TMP_Text title = CreateNoticeText(card.transform, "Titulo", 48f, new Color(0.95f, 0.25f, 0.15f, 1f), titleFont, FontStyles.Bold);
        PlaceInCard(title.rectTransform, 36f, -36f, 64f);

        TMP_Text body = CreateNoticeText(card.transform, "Mensaje", 26f, new Color(0.93f, 0.94f, 0.92f, 1f), bodyFont, FontStyles.Normal);
        PlaceInCard(body.rectTransform, 36f, -120f, 120f);

        TMP_Text countdown = CreateNoticeText(card.transform, "Cuenta", 22f, new Color(0.95f, 0.78f, 0.28f, 1f), bodyFont, FontStyles.Normal);
        PlaceInCard(countdown.rectTransform, 36f, -250f, 40f);
        countdown.gameObject.SetActive(false);

        Button button = CreateNoticeButton(card.transform, "VOLVER AL MENÚ", bodyFont);
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.offsetMin = new Vector2(36f, 36f);
        buttonRect.offsetMax = new Vector2(-36f, 100f);
        button.onClick.AddListener(DismissNotice);

        noticePanel = styledNoticeRoot;
        noticeGroup = group;
        titleText = title;
        noticeText = body;
        countdownText = countdown;
        acceptButton = button;
    }

    private static Image CreateNoticeImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateNoticeText(Transform parent, string name, float size, Color color, TMP_FontAsset font, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        if (font != null)
            label.font = font;
        return label;
    }

    private static void PlaceInCard(RectTransform rect, float left, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, top - height);
        rect.offsetMax = new Vector2(-left, top);
    }

    private static Button CreateNoticeButton(Transform parent, string label, TMP_FontAsset font)
    {
        GameObject go = new GameObject("Boton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.80f, 0.12f, 0.12f, 0.90f);

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.8f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text text = CreateNoticeText(go.transform, "Texto", 26f, Color.white, font, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        text.text = label;
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---------- API publica (botones) ----------

    // Para un boton "Salir de la partida".
    public void LeaveSession()
    {
        if (leavingToMenu) return;

        if (sessionActive)
        {
            sessionActive = false;
            UnlockCursor();
            SessionEnded?.Invoke();
        }

        GoToMenu(notifyClients: true);
    }

    // Para el boton "Aceptar" / "Volver al menu" del aviso.
    public void DismissNotice()
    {
        if (returnToMenuOnDismiss)
        {
            returnToMenuOnDismiss = false;
            GoToMenu(notifyClients: false);
            return;
        }

        HideNotice();
    }

    // ---------- Eventos de red ----------

    private void OnSessionStarted()
    {
        sessionActive = true;
    }

    private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        if (!sessionActive) return;

        switch (data.EventType)
        {
            case ConnectionEvent.ClientDisconnected:
                if (manager.IsServer)
                {
                    if (data.ClientId != NetworkManager.ServerClientId)
                    {
                        OnRemotePlayerLeft(data.ClientId);
                    }
                }
                else
                {
                    EndSession("Host desconectado", "El host cerró la partida o se perdió la conexión.");
                }
                break;

            case ConnectionEvent.PeerDisconnected:
                // En el host ya se maneja con ClientDisconnected.
                // Si el que se va es el host, lo maneja ClientDisconnected (no mostrar un aviso intermedio).
                if (!manager.IsServer && data.ClientId != NetworkManager.ServerClientId)
                {
                    ShowNotice(null, "Otro jugador se desconectó.", remotePlayerLeftNoticeSeconds);
                }
                break;
        }
    }

    private void OnTransportFailure()
    {
        if (!sessionActive) return;

        EndSession("Conexión perdida", "Se perdió la conexión de red con la partida.");
    }

    private void OnRemotePlayerLeft(ulong clientId)
    {
        Debug.Log($"DisconnectionHandler: el cliente {clientId} se desconecto.");

        DespawnOrphanPlayers();
        ShowNotice(null, "El otro jugador se desconectó. Podés seguir jugando.", remotePlayerLeftNoticeSeconds);
    }

    // Netcode ya despawnea el personaje del cliente que se va (DontDestroyWithOwner desactivado en el prefab).
    // Esto cubre el caso en que alguien lo active: el personaje pasaria a ser del server y seguiria bloqueando colisiones.
    private void DespawnOrphanPlayers()
    {
        NetworkObject hostPlayer = networkManager.LocalClient?.PlayerObject;
        var orphans = new List<NetworkObject>();

        foreach (NetworkObject networkObject in networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!networkObject.IsPlayerObject || networkObject == hostPlayer) continue;

            if (!networkManager.ConnectedClients.ContainsKey(networkObject.OwnerClientId)
                || networkObject.OwnerClientId == NetworkManager.ServerClientId)
            {
                orphans.Add(networkObject);
            }
        }

        foreach (NetworkObject orphan in orphans)
        {
            orphan.Despawn(true);
        }
    }

    // ---------- Fin de sesion ----------

    private void EndSession(string title, string message)
    {
        if (!sessionActive) return; // evita doble disparo (ClientDisconnected + TransportFailure)

        sessionActive = false;
        Debug.Log($"DisconnectionHandler: {title} - {message}");

        UnlockCursor();
        SessionEnded?.Invoke(); // otros sistemas (musica, input, HUD) pueden frenarse aca

        StartCoroutine(EndSessionRoutine(title, message));
    }

    private IEnumerator EndSessionRoutine(string title, string message)
    {
        // Salimos del callback de red antes de apagar Netcode.
        yield return null;
        ShutdownNetwork(discardMessageQueue: true);

        // Sin UI no hay boton: se vuelve directo al menu.
        if (!HasNoticeUI)
        {
            GoToMenu(notifyClients: false);
            yield break;
        }

        ShowNotice(title, message, 0f);
        returnToMenuOnDismiss = true;
        EnsureRenderingCamera();

        float remaining = autoReturnSeconds;
        while (returnToMenuOnDismiss)
        {
            EnsureRenderingCamera();

            if (autoReturnSeconds > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(true);
                    countdownText.text = $"Volviendo al menú en {Mathf.CeilToInt(Mathf.Max(0f, remaining))}...";
                }

                if (remaining <= 0f)
                {
                    DismissNotice();
                    yield break;
                }
            }
            yield return null;
        }
    }

    private void GoToMenu(bool notifyClients)
    {
        if (leavingToMenu) return;
        leavingToMenu = true;

        returnToMenuOnDismiss = false;
        HideNotice();
        StartCoroutine(GoToMenuRoutine(notifyClients));
    }

    private IEnumerator GoToMenuRoutine(bool notifyClients)
    {
        if (notifyClients) NotifyClientsHostLeft();

        ShutdownNetwork(discardMessageQueue: !notifyClients);

        // Esperamos a que Netcode termine de cerrarse antes de cambiar de escena.
        float waited = 0f;
        while (networkManager != null
               && (networkManager.IsListening || networkManager.ShutdownInProgress)
               && waited < shutdownTimeoutSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        GameSessionManager.ReturnToMainMenu();
    }

    // Si el host sale a proposito, los clientes reciben un motivo claro.
    // No esta garantizado que llegue antes del cierre: si no llega, se usa el mensaje por defecto.
    private void NotifyClientsHostLeft()
    {
        if (networkManager == null || !networkManager.IsServer) return;

        var ids = new List<ulong>(networkManager.ConnectedClientsIds);
        foreach (ulong id in ids)
        {
            if (id == NetworkManager.ServerClientId) continue;
            networkManager.DisconnectClient(id, "El host cerró la partida.");
        }
    }

    private void ShutdownNetwork(bool discardMessageQueue)
    {
        if (networkManager != null && networkManager.IsListening && !networkManager.ShutdownInProgress)
        {
            networkManager.Shutdown(discardMessageQueue);
        }
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Unity muestra "No cameras rendering" si, al irse el host, se destruye la cámara del jugador.
    private void EnsureRenderingCamera()
    {
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam != null && cam.enabled && cam != fallbackCamera)
                return;
        }

        if (fallbackCamera == null)
        {
            GameObject cameraObject = new GameObject("DisconnectCamera");
            fallbackCamera = cameraObject.AddComponent<Camera>();
            fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
            fallbackCamera.backgroundColor = new Color(0.02f, 0.02f, 0.025f, 1f);
            fallbackCamera.cullingMask = 0;
            fallbackCamera.depth = -100;
        }

        fallbackCamera.enabled = true;
    }

    // ---------- Aviso en pantalla ----------

    // seconds <= 0: el aviso queda hasta que se llame a DismissNotice.
    // Los avisos temporales no bloquean clicks; el de fin de partida si.
    private void ShowNotice(string title, string message, float seconds)
    {
        Debug.Log($"DisconnectionHandler: aviso -> {message}");

        StopNoticeRoutines();
        bool persistent = seconds <= 0f;

        if (noticeText != null)
        {
            noticeText.text = message;
            noticeText.gameObject.SetActive(true);
        }
        if (titleText != null)
        {
            titleText.text = title ?? string.Empty;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (acceptButton != null) acceptButton.gameObject.SetActive(persistent);

        if (noticePanel != null)
        {
            noticePanel.SetActive(true);
            if (noticeGroup != null)
            {
                noticeGroup.blocksRaycasts = persistent;
                noticeGroup.interactable = persistent;
                fadeRoutine = StartCoroutine(FadeIn());
            }
        }

        if (!persistent)
        {
            hideNoticeRoutine = StartCoroutine(HideNoticeAfter(seconds));
        }
    }

    private IEnumerator FadeIn()
    {
        noticeGroup.alpha = 0f;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime; // funciona aunque timeScale sea 0
            noticeGroup.alpha = Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }
        noticeGroup.alpha = 1f;
        fadeRoutine = null;
    }

    private IEnumerator HideNoticeAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        hideNoticeRoutine = null;
        HideNotice();
    }

    private void StopNoticeRoutines()
    {
        if (hideNoticeRoutine != null) { StopCoroutine(hideNoticeRoutine); hideNoticeRoutine = null; }
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
    }

    private void HideNotice()
    {
        StopNoticeRoutines();

        if (noticePanel != null)
        {
            noticePanel.SetActive(false);
        }
        else if (noticeText != null)
        {
            noticeText.gameObject.SetActive(false);
        }
    }
}