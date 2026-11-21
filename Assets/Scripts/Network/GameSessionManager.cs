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
        MatchSettings.Reset();
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApproveConnection;
    }

    // Llamar antes de RelayConnectionManager.StartClientAsync.
    // ConnectionApproval forma parte de la configuracion que se compara con el host, tiene que coincidir.
    public static async Task PrepareClientAsync()
    {
        NetworkManager networkManager = await WaitForShutdownAsync();

        gameStarted = false;
        MatchSettings.Reset();
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = null;
    }

    // Solo el host. Carga la escena para todos los conectados; devuelve false si no se pudo iniciar la carga.
    // mode: el modo elegido en "Crear partida", que se aplica en todos los clientes junto con la escena.
    // None = no cambia el modo (pasar al siguiente nivel de la misma partida).
    public static bool StartGame(string sceneName, GameMode mode = GameMode.None)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer) return false;

        if (mode != GameMode.None)
            MatchSettings.SetAndBroadcast(mode);

        MatchSettings.SetAndBroadcastDifficulty(CurrentGame.Difficulty);

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

    // Solo el host. Vuelve a cargar el nivel actual para todos los conectados, sin pasar por el menu:
    // la sala y la conexion siguen igual, y se mantienen el modo y la dificultad de la partida.
    // Los personajes se destruyen con la escena (SpawnAsPlayerObject con destroyWithScene = true) y
    // OnGameSceneLoaded los vuelve a crear, igual que al pasar de nivel.
    public static bool RestartMatch()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || !networkManager.IsListening) return false;

        return StartGame(SceneManager.GetActiveScene().name);
    }

    // Solo el host. Lleva a todos los jugadores de vuelta al lobby de la MISMA sala: no se apaga la red,
    // se carga la escena del menu por Netcode. Ahi el host puede elegir otro mapa, modo o dificultad
    // y volver a iniciar con la gente que ya esta conectada.
    public static bool ReturnToLobby()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || !networkManager.IsListening) return false;

        // Los objetos de red creados durante la partida (enemigos, pickups...) no tienen que viajar al lobby:
        // si se crearon sin destroyWithScene, Netcode los conserva al cambiar de escena.
        foreach (NetworkObject spawned in new List<NetworkObject>(networkManager.SpawnManager.SpawnedObjectsList))
        {
            if (spawned == null || !spawned.IsSpawned || spawned.IsPlayerObject) continue;
            if (spawned.IsSceneObject == true) continue; // los de escena se van con la escena

            spawned.Despawn(true);
        }

        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"GameSessionManager: no se pudo volver al lobby ({status}). ¿'{MainMenuSceneName}' esta en Build Settings?");
            return false;
        }

        return true;
    }

    // Lo llama el menu en TODAS las maquinas al aparecer con la sala todavia conectada: descarta el estado
    // de la partida anterior y vuelve a aceptar jugadores nuevos mientras la sala esta en el lobby.
    public static void PrepareLobbyReturn()
    {
        gameStarted = false;
        MatchSettings.Reset();
        PlayerCarryOver.Clear();
        SavedGameLoader.Clear();
        CurrentGame.Clear();
    }

    // Apaga la red, descarta el NetworkManager persistente (MainMenu trae el suyo) y carga el menu.
    public static void ReturnToMainMenu()
    {
        gameStarted = false;
        MatchSettings.Reset();
        PlayerCarryOver.Clear();
        SavedGameLoader.Clear();
        CurrentGame.Clear();

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

        if (SavedGameLoader.TryGetSafePoint(out Vector3 safePoint))
            spawnPosition = safePoint;

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

            if (PlayerCarryOver.HasServerPending)
                PlayerCarryOver.ApplyServerState(player, clientId);

            playerIndex++;
        }

        if (PlayerCarryOver.HasServerPending)
            PlayerCarryOver.MarkServerApplied();

        SavedGameLoader.Clear();
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
