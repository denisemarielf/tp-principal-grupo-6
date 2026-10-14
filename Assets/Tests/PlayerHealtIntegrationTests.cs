using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PlayerHealtIntegrationTests
{
    private NetworkManager networkManager;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.LoadScene("MainMenu");

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "MainMenu"
        );

        yield return new WaitUntil(
            () => NetworkManager.Singleton != null
        );

        networkManager = NetworkManager.Singleton;

        Task hostTask = StartHost();

        yield return new WaitUntil(
            () => hostTask.IsCompleted
        );

        if (hostTask.IsFaulted)
        {
            throw hostTask.Exception;
        }

        LogAssert.ignoreFailingMessages = true;

        bool partidaIniciada =
            GameSessionManager.StartGame("SampleScene");

        Assert.That(partidaIniciada, Is.True);

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "SampleScene"
        );

        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator AlIniciarPartidaElJugadorComienzaConVidaMaxima()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(playerHealth.MaxHealth)
        );

        yield return null;
    }

    [UnityTest]
    public IEnumerator AlRecibirDanioLaVidaDisminuye()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        float vidaInicial = playerHealth.CurrentHealth;

        playerHealth.TakeDamage(25f);

        yield return null;

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(vidaInicial - 25f)
        );
    }

    [UnityTest]
    public IEnumerator AlLlegarLaVidaACeroElJugadorMuere()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        playerHealth.TakeDamage(playerHealth.CurrentHealth);

        yield return null;

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));
        Assert.That(playerHealth.IsDead, Is.True);
    }

    [UnityTest]
    public IEnumerator UnJugadorMuertoNoPuedeRecibirDanio()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        playerHealth.TakeDamage(playerHealth.CurrentHealth);

        yield return null;

        Assert.That(playerHealth.IsDead, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));

        playerHealth.TakeDamage(25f);

        yield return null;

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator UnJugadorMuertoNoPuedeSerCurado()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        playerHealth.TakeDamage(playerHealth.CurrentHealth);

        yield return null;

        Assert.That(playerHealth.IsDead, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));

        playerHealth.Heal(25f);

        yield return null;

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator AlMorirElJugadorSeDisparaElEventoDied()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        bool eventoDisparado = false;

        playerHealth.Died += () => eventoDisparado = true;

        playerHealth.TakeDamage(playerHealth.CurrentHealth);

        yield return null;

        Assert.That(playerHealth.IsDead, Is.True);
        Assert.That(eventoDisparado, Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
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
                () =>
                    !networkManager.ShutdownInProgress &&
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
