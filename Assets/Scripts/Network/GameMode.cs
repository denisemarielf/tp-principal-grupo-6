using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Modo de juego elegido por el host al crear la partida.
public enum GameMode
{
    None = 0,          // todavia no se eligio (escenas abiertas directo desde el editor)
    Extraccion = 1,
    Supervivencia = 2
}

// Modo y dificultad de la partida en curso. Estatico: sobrevive a los cambios de escena.
// El host lo fija al iniciar la partida y lo envia a todos los clientes (MatchSettingsSync).
public static class MatchSettings
{
    // Indices de mapa del selector del menu.
    public const int MapSuburbio = 0;
    public const int MapOficinas = 1;
    public const int MapEstacion = 2;

    private const string ModeMessage = "MatchSettings.Mode";
    private const string DifficultyMessage = "MatchSettings.Difficulty";

    private static readonly GameMode[] ModosExtraccion = { GameMode.Extraccion };
    private static readonly GameMode[] ModosEstacion = { GameMode.Extraccion, GameMode.Supervivencia };

    public static GameMode Mode { get; private set; }
    public static int Difficulty { get; private set; } = 1;

    // Cada mapa solo ofrece los modos que le corresponden. El primero es el que viene elegido por defecto.
    public static GameMode[] ModesForMap(int mapIndex)
    {
        return mapIndex == MapEstacion ? ModosEstacion : ModosExtraccion;
    }

    public static bool IsAllowed(int mapIndex, GameMode mode)
    {
        return System.Array.IndexOf(ModesForMap(mapIndex), mode) >= 0;
    }

    // El modo que sigue al actual dentro de los que ofrece el mapa (para el boton ciclico).
    public static GameMode NextMode(int mapIndex, GameMode current)
    {
        GameMode[] modos = ModesForMap(mapIndex);
        int indice = System.Array.IndexOf(modos, current);

        return modos[(indice + 1) % modos.Length];
    }

    public static string Label(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Extraccion: return "EXTRACCIÓN";
            case GameMode.Supervivencia: return "SUPERVIVENCIA";
            default: return "SIN DEFINIR";
        }
    }

    // La Estacion de tren trae la partida de Supervivencia armada en la escena; en modo Extraccion
    // (el nivel de la plaza) no tiene que arrancar. En el resto de las escenas y sin modo definido
    // (escena abierta desde el editor) todo funciona como siempre.
    public static bool SurvivalEnabled
    {
        get
        {
            if (Mode != GameMode.Extraccion)
                return true;

            return SceneManager.GetActiveScene().name != PlayerCarryOver.StationSceneName;
        }
    }

    public static void Set(GameMode mode)
    {
        Mode = mode;
    }

    public static void Reset()
    {
        Mode = GameMode.None;
        Difficulty = 1;
    }

    // Solo el host: fija el modo y se lo manda a los clientes conectados.
    public static void SetAndBroadcast(GameMode mode)
    {
        Mode = mode;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null)
            return;

        foreach (ulong clientId in manager.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId)
                continue;

            using (FastBufferWriter writer =
                new FastBufferWriter(4, Allocator.Temp))
            {
                writer.WriteValueSafe((int)mode);
                manager.CustomMessagingManager.SendNamedMessage(ModeMessage, clientId, writer);
            }
        }
    }

    // Solo el host: fija la dificultad y se la manda a los clientes conectados.
    public static void SetAndBroadcastDifficulty(int difficulty)
    {
        Difficulty = Mathf.Clamp(difficulty, 0, 2);
        CurrentGame.ApplyHostDifficulty(Difficulty);

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null)
            return;

        foreach (ulong clientId in manager.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId)
                continue;

            using (FastBufferWriter writer =
                new FastBufferWriter(4, Allocator.Temp))
            {
                writer.WriteValueSafe(Difficulty);
                manager.CustomMessagingManager.SendNamedMessage(DifficultyMessage, clientId, writer);
            }
        }
    }

    // Cliente: llega el modo elegido por el host.
    internal static void RegisterClientHandler(NetworkManager manager)
    {
        manager.CustomMessagingManager.RegisterNamedMessageHandler(ModeMessage, OnModeReceived);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(DifficultyMessage, OnDifficultyReceived);
    }

    private static void OnModeReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int value);
        Mode = (GameMode)value;
        Debug.Log("MatchSettings: modo de la partida -> " + Mode);
    }

    private static void OnDifficultyReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int value);
        Difficulty = Mathf.Clamp(value, 0, 2);
        CurrentGame.ApplyHostDifficulty(Difficulty);
        Debug.Log("MatchSettings: dificultad de la partida -> " + Difficulty);
    }
}

// Registra el mensaje del modo apenas el NetworkManager actual empieza a escuchar
// (el de MainMenu se destruye al salir de la partida, asi que se vuelve a registrar con el siguiente).
public class MatchSettingsSync : MonoBehaviour
{
    private NetworkManager registeredOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("MatchSettingsSync");
        DontDestroyOnLoad(host);
        host.AddComponent<MatchSettingsSync>();
    }

    private void Update()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || manager == registeredOn || manager.CustomMessagingManager == null)
            return;

        MatchSettings.RegisterClientHandler(manager);
        registeredOn = manager;
    }
}
