using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ciclo de vida de una partida en red:
// - Lobby: el host aprueba conexiones (maximo MaxPlayers) y rechaza a quien intente unirse con la partida ya iniciada.
// - Inicio: el host carga la escena de juego para todos y recien ahi se crean los personajes.
// - Fin: se apaga la red y se vuelve al menu principal.
public static class GameSessionManager
{
    public const int MaxPlayers = 4;
    public const string MainMenuSceneName = "MainMenu";

    private static bool gameStarted;

    // Llamar antes de RelayConnectionManager.StartHostAsync.
    public static async Task PrepareHostAsync()
    {
        NetworkManager networkManager = await WaitForShutdownAsync();

        gameStarted = false;
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApproveConnection;
    }

    // Llamar antes de RelayConnectionManager.StartClientAsync.
    // ConnectionApproval forma parte de la configuracion que se compara con el host, tiene que coincidir.
    public static async Task PrepareClientAsync()
    {
        NetworkManager networkManager = await WaitForShutdownAsync();

        gameStarted = false;
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = null;
    }

    // Solo el host. Carga la escena para todos los conectados; devuelve false si no se pudo iniciar la carga.
    public static bool StartGame(string sceneName)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer) return false;

        gameStarted = true;
        networkManager.SceneManager.OnLoadEventCompleted += OnGameSceneLoaded;

        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"GameSessionManager: no se pudo cargar la escena '{sceneName}' ({status}). ¿Esta agregada en Build Settings?");
            networkManager.SceneManager.OnLoadEventCompleted -= OnGameSceneLoaded;
            gameStarted = false;
            return false;
        }

        return true;
    }

    // Apaga la red, descarta el NetworkManager persistente (MainMenu trae el suyo) y carga el menu.
    public static void ReturnToMainMenu()
    {
        gameStarted = false;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            if (networkManager.IsListening && !networkManager.ShutdownInProgress)
            {
                networkManager.Shutdown(true);
            }
            Object.Destroy(networkManager.gameObject);
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }

    private static void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        bool isHostItself = request.ClientNetworkId == NetworkManager.ServerClientId;

        // Los personajes se crean al cargar la escena de juego, no en el lobby.
        response.CreatePlayerObject = false;

        if (!isHostItself && gameStarted)
        {
            response.Approved = false;
            response.Reason = "La partida ya comenzó.";
            return;
        }

        if (!isHostItself && networkManager.ConnectedClientsIds.Count >= MaxPlayers)
        {
            response.Approved = false;
            response.Reason = "La sala está llena.";
            return;
        }

        response.Approved = true;
    }

    private static void OnGameSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        networkManager.SceneManager.OnLoadEventCompleted -= OnGameSceneLoaded;

        GameObject playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
        if (playerPrefab == null)
        {
            Debug.LogError("GameSessionManager: el NetworkManager no tiene asignado un Player Prefab.");
            return;
        }

        // Sin PlayerSpawnPoint en la escena se usa la posicion del prefab.
        PlayerSpawnPoint spawnPoint = Object.FindAnyObjectByType<PlayerSpawnPoint>();
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.transform.position : playerPrefab.transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.transform.rotation : playerPrefab.transform.rotation;

        int playerIndex = 0;
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (networkManager.ConnectedClients[clientId].PlayerObject != null) continue;

            GameObject player = Object.Instantiate(playerPrefab, spawnPosition, spawnRotation);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);

            Character character = player.GetComponent<Character>();
            if (character != null)
            {
                character.PlaceAtSpawn(spawnPosition, spawnRotation, playerIndex);
            }
            playerIndex++;
        }
    }

    // Si se acaba de salir de una sala, el NetworkManager no puede volver a arrancar hasta terminar de apagarse.
    private static async Task<NetworkManager> WaitForShutdownAsync()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            throw new System.InvalidOperationException("No hay NetworkManager en la escena.");
        }

        if (networkManager.IsListening && !networkManager.ShutdownInProgress)
        {
            networkManager.Shutdown();
        }

        while (networkManager.ShutdownInProgress || networkManager.IsListening)
        {
            await Task.Yield();
        }

        return networkManager;
    }
}
