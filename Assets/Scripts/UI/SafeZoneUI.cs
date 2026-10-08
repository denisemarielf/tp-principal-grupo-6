using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// HUD de la zona segura (US Zona Segura). Va dentro del HUD del Player prefab.
// - Muestra cuantos jugadores vivos estan en la zona segura mientras haya alguno adentro.
// - Al completarse el nivel muestra el panel de "Nivel completado", frena al jugador y libera el cursor.
public class SafeZoneUI : MonoBehaviour
{
    [Tooltip("Texto tipo 'Zona segura: 1/2 jugadores'. Se oculta cuando no hay nadie adentro.")]
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Panel que se muestra al completar el nivel. Puede tener un boton que llame a ReturnToMainMenu().")]
    [SerializeField] private GameObject levelCompletePanel;

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
        if (statusText == null) return;

        SafeZone zone = SafeZone.Current;
        bool show = zone != null && !zone.IsCompleted && zone.PlayersInside > 0;
        statusText.gameObject.SetActive(show);
        if (!show) return;

        statusText.text = zone.AllAlivePlayersInside
            ? "¡Todos a salvo!"
            : $"Zona segura: {zone.PlayersInside}/{zone.AlivePlayers} jugadores";
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
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
