using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

// Maneja las desconexiones durante una partida (ver Docs/Desconexion.md):
// - Si se va un cliente: el host recibe un aviso, se elimina el personaje del cliente y la partida sigue.
// - Si se va el host o se pierde la conexion: la partida finaliza para el cliente y vuelve al menu.
public class DisconnectionHandler : MonoBehaviour
{
    public static event Action SessionEnded;

    [SerializeField] private GameObject noticePanel;
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private float remotePlayerLeftNoticeSeconds = 4f;

    private NetworkManager networkManager;
    private Coroutine hideNoticeRoutine;
    private bool sessionActive;
    private bool returnToMenuOnDismiss;

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
            Debug.LogWarning("DisconnectionHandler: falta asignar 'Notice Text' en el Inspector, los avisos solo se veran en la consola.", this);
        }

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
        if (networkManager == null) return;

        networkManager.OnServerStarted -= OnSessionStarted;
        networkManager.OnClientStarted -= OnSessionStarted;
        networkManager.OnConnectionEvent -= OnConnectionEvent;
        networkManager.OnTransportFailure -= OnTransportFailure;
    }

    // Para un boton "Salir de la partida".
    public void LeaveSession()
    {
        if (!sessionActive) return;

        sessionActive = false;
        ShutdownNetwork();
        UnlockCursor();
        HideNotice();
        SessionEnded?.Invoke();
        GameSessionManager.ReturnToMainMenu();
    }

    // Para un boton "Aceptar" en el aviso.
    public void DismissNotice()
    {
        HideNotice();

        if (returnToMenuOnDismiss)
        {
            returnToMenuOnDismiss = false;
            GameSessionManager.ReturnToMainMenu();
        }
    }

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
                    string reason = string.IsNullOrEmpty(manager.DisconnectReason)
                        ? "El host cerro la partida o se perdio la conexion."
                        : manager.DisconnectReason;
                    EndSession($"Partida finalizada: {reason}");
                }
                break;

            case ConnectionEvent.PeerDisconnected:
                // En el host ya se maneja con ClientDisconnected.
                if (!manager.IsServer)
                {
                    ShowNotice("Otro jugador se desconecto.", remotePlayerLeftNoticeSeconds);
                }
                break;
        }
    }

    private void OnTransportFailure()
    {
        if (!sessionActive) return;

        EndSession("Partida finalizada: se perdio la conexion de red.");
    }

    private void OnRemotePlayerLeft(ulong clientId)
    {
        Debug.Log($"DisconnectionHandler: el cliente {clientId} se desconecto.");

        DespawnOrphanPlayers();
        ShowNotice("El otro jugador se desconecto. Podes seguir jugando.", remotePlayerLeftNoticeSeconds);
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

    private void EndSession(string message)
    {
        sessionActive = false;
        Debug.Log($"DisconnectionHandler: {message}");

        ShutdownNetwork();
        UnlockCursor();
        SessionEnded?.Invoke();

        // Sin aviso en pantalla no hay boton "Aceptar": se vuelve directo al menu.
        if (noticePanel == null && noticeText == null)
        {
            GameSessionManager.ReturnToMainMenu();
            return;
        }

        ShowNotice(message, 0f);
        returnToMenuOnDismiss = true;
    }

    private void ShutdownNetwork()
    {
        if (networkManager.IsListening && !networkManager.ShutdownInProgress)
        {
            networkManager.Shutdown(true);
        }
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // seconds <= 0: el aviso queda hasta que se llame a DismissNotice.
    private void ShowNotice(string message, float seconds)
    {
        Debug.Log($"DisconnectionHandler: aviso -> {message}");

        if (hideNoticeRoutine != null)
        {
            StopCoroutine(hideNoticeRoutine);
            hideNoticeRoutine = null;
        }

        if (noticeText != null)
        {
            noticeText.text = message;
            noticeText.gameObject.SetActive(true);
        }
        if (noticePanel != null)
        {
            noticePanel.SetActive(true);
        }

        if (seconds > 0f)
        {
            hideNoticeRoutine = StartCoroutine(HideNoticeAfter(seconds));
        }
    }

    private IEnumerator HideNoticeAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        hideNoticeRoutine = null;
        HideNotice();
    }

    private void HideNotice()
    {
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
