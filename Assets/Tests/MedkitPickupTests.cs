using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MedkitPickupTests
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
    public IEnumerator AlUsarUnBotiquinLaVidaDelJugadorAumenta()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        MedkitPickup medkit =
            Object.FindFirstObjectByType<MedkitPickup>();

        Assert.That(medkit, Is.Not.Null);

        playerHealth.TakeDamage(25f);

        yield return null;

        float vidaAntesDelBotiquin =
            playerHealth.CurrentHealth;

        float vidaEsperada =
            vidaAntesDelBotiquin + medkit.GetHealAmount();

        playerObject.transform.position =
            medkit.transform.position;

        medkit.RequestUse();

        yield return new WaitUntil(
            () =>
                playerHealth.CurrentHealth >= vidaEsperada
        );

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(vidaEsperada)
        );
    }

    [UnityTest]
    public IEnumerator UnJugadorConVidaMaximaNoPuedeUsarUnBotiquin()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        MedkitPickup medkit =
            Object.FindFirstObjectByType<MedkitPickup>();

        Assert.That(medkit, Is.Not.Null);

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(playerHealth.MaxHealth)
        );

        playerObject.transform.position =
            medkit.transform.position;

        medkit.RequestUse();

        yield return null;

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(playerHealth.MaxHealth)
        );

        Assert.That(
            Object.FindFirstObjectByType<MedkitPickup>(),
            Is.Not.Null
        );
    }

    [UnityTest]
    public IEnumerator AlUsarUnBotiquinEsteDesaparece()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        MedkitPickup medkit =
            Object.FindFirstObjectByType<MedkitPickup>();

        Assert.That(medkit, Is.Not.Null);

        playerHealth.TakeDamage(25f);

        yield return null;

        playerObject.transform.position =
            medkit.transform.position;

        medkit.RequestUse();

        yield return new WaitUntil(
            () => medkit == null || !medkit.IsSpawned
        );

        Assert.That(
            medkit == null || !medkit.IsSpawned,
            Is.True
        );
    }

    [UnityTest]
    public IEnumerator UnJugadorMuertoNoPuedeUsarUnBotiquin()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        MedkitPickup medkit =
            Object.FindFirstObjectByType<MedkitPickup>();

        Assert.That(medkit, Is.Not.Null);

        playerHealth.TakeDamage(
            playerHealth.CurrentHealth
        );

        yield return null;

        Assert.That(playerHealth.IsDead, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));

        playerObject.transform.position =
            medkit.transform.position;

        medkit.RequestUse();

        yield return null;

        Assert.That(playerHealth.IsDead, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));

        Assert.That(
            Object.FindFirstObjectByType<MedkitPickup>(),
            Is.Not.Null
        );
    }

    [UnityTest]
    public IEnumerator ElBotiquinNoPermiteSuperarLaVidaMaxima()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        MedkitPickup medkit =
            Object.FindFirstObjectByType<MedkitPickup>();

        Assert.That(medkit, Is.Not.Null);

        float healAmount = medkit.GetHealAmount();

        Assert.That(healAmount, Is.GreaterThan(0f));

        // Dejamos al jugador con menos vida que la máxima,
        // pero con una cantidad faltante menor a la curación del botiquín.
        float damage = healAmount / 2f;

        playerHealth.TakeDamage(damage);

        yield return null;

        Assert.That(
            playerHealth.CurrentHealth,
            Is.LessThan(playerHealth.MaxHealth)
        );

        playerObject.transform.position =
            medkit.transform.position;

        medkit.RequestUse();

        yield return new WaitUntil(
            () =>
                playerHealth.CurrentHealth ==
                playerHealth.MaxHealth
        );

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(playerHealth.MaxHealth)
        );
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
