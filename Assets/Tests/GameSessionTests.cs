using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;


public class GameSessionTests
{
    [UnityTest]
    public IEnumerator HostPuedeIniciarse()
    {
        SceneManager.LoadScene("MainMenu");

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "MainMenu"
        );

        yield return new WaitUntil(
            () => NetworkManager.Singleton != null
        );

        Task hostTask = StartHost();

        yield return new WaitUntil(() => hostTask.IsCompleted);

        if (hostTask.IsFaulted)
        {
            throw hostTask.Exception;
        }

        Assert.That(NetworkManager.Singleton.IsHost, Is.True);
    }

 [UnityTest]
public IEnumerator AlIniciarPartidaSeInstanciaElMapa()
{
    SceneManager.LoadScene("MainMenu");

    yield return new WaitUntil(
        () => SceneManager.GetActiveScene().name == "MainMenu"
    );

    yield return new WaitUntil(
        () => NetworkManager.Singleton != null
    );

    Task hostTask = StartHost();

    yield return new WaitUntil(() => hostTask.IsCompleted);

    if (hostTask.IsFaulted)
    {
        throw hostTask.Exception;
    }

    LogAssert.ignoreFailingMessages = true;

    bool partidaIniciada = GameSessionManager.StartGame("SampleScene");

    Assert.That(partidaIniciada, Is.True);

    yield return new WaitUntil(
        () => SceneManager.GetActiveScene().name == "SampleScene"
    );

    LogAssert.ignoreFailingMessages = false;

    Assert.That(
        SceneManager.GetActiveScene().name,
        Is.EqualTo("SampleScene")
    );
}

[UnityTest]
public IEnumerator AlIniciarPartidaSeInstanciaElJugador()
{
    SceneManager.LoadScene("MainMenu");

    yield return new WaitUntil(
        () => SceneManager.GetActiveScene().name == "MainMenu"
    );

    yield return new WaitUntil(
        () => NetworkManager.Singleton != null
    );

    Task hostTask = StartHost();

    yield return new WaitUntil(() => hostTask.IsCompleted);

    if (hostTask.IsFaulted)
    {
        throw hostTask.Exception;
    }

    LogAssert.ignoreFailingMessages = true;

    bool partidaIniciada = GameSessionManager.StartGame("SampleScene");

    Assert.That(partidaIniciada, Is.True);

    yield return new WaitUntil(
        () => SceneManager.GetActiveScene().name == "SampleScene"
    );

    LogAssert.ignoreFailingMessages = false;

    NetworkManager networkManager = NetworkManager.Singleton;

    Assert.That(
        networkManager.ConnectedClients[NetworkManager.ServerClientId].PlayerObject,
        Is.Not.Null
    );
}

[UnityTest]
public IEnumerator SeInstanciaLaCantidadCorrectaDeJugadores()
{
    SceneManager.LoadScene("MainMenu");

    yield return new WaitUntil(
        () => SceneManager.GetActiveScene().name == "MainMenu"
    );

    yield return new WaitUntil(
        () => NetworkManager.Singleton != null
    );

    Task hostTask = StartHost();

    yield return new WaitUntil(() => hostTask.IsCompleted);

    if (hostTask.IsFaulted)
    {
        throw hostTask.Exception;
    }

    LogAssert.ignoreFailingMessages = true;

    bool partidaIniciada = GameSessionManager.StartGame("SampleScene");

    Assert.That(partidaIniciada, Is.True);

    yield return new WaitUntil(
        () => SceneManager.GetActiveScene().name == "SampleScene"
    );

    LogAssert.ignoreFailingMessages = false;

    NetworkManager networkManager = NetworkManager.Singleton;

    int cantidadDeJugadores = 0;

    foreach (NetworkObject networkObject in networkManager.SpawnManager.SpawnedObjectsList)
    {
        if (networkObject.IsPlayerObject)
        {
            cantidadDeJugadores++;
        }
    }

    Assert.That(
        cantidadDeJugadores,
        Is.EqualTo(networkManager.ConnectedClientsIds.Count)
    );
}

[UnityTearDown]
public IEnumerator TearDown()
{
    NetworkManager networkManager = NetworkManager.Singleton;

    if (SceneManager.GetActiveScene().name == "SampleScene")
    {
        SceneManager.LoadScene("MainMenu");

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "MainMenu"
        );
    }

    if (networkManager != null &&
        networkManager.IsListening &&
        !networkManager.ShutdownInProgress)
    {
        networkManager.Shutdown();

        yield return new WaitUntil(
            () => !networkManager.ShutdownInProgress &&
                  !networkManager.IsListening
        );
    }
}

    private async Task StartHost()
    {
        await GameSessionManager.PrepareHostAsync();

        await RelayConnectionManager.StartHostAsync(
            GameSessionManager.MaxPlayers - 1
        );
    }
}