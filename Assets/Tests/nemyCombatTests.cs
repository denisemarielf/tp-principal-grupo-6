using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnemyCombatTests
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
    public IEnumerator ElEnemigoAtacaAlJugadorDentroDelRango()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(playerHealth, Is.Not.Null);

        EnemyCombat enemy =
            Object.FindFirstObjectByType<EnemyCombat>();

        Assert.That(enemy, Is.Not.Null);

        float vidaInicial =
            playerHealth.CurrentHealth;

        playerObject.transform.position =
            enemy.transform.position +
            Vector3.forward * 1f;

        yield return new WaitForSeconds(1f);

        Assert.That(
            playerHealth.CurrentHealth,
            Is.LessThan(vidaInicial),
            "El enemigo debería dañar al jugador cuando está dentro del rango de ataque."
        );
    }

    [UnityTest]
public IEnumerator ElEnemigoNoAtacaAlJugadorFueraDelRango()
{
    NetworkObject playerObject =
        networkManager.ConnectedClients[
            NetworkManager.ServerClientId
        ].PlayerObject;

    Assert.That(playerObject, Is.Not.Null);

    PlayerHealth playerHealth =
        playerObject.GetComponent<PlayerHealth>();

    Assert.That(playerHealth, Is.Not.Null);

    EnemyCombat enemy =
        Object.FindFirstObjectByType<EnemyCombat>();

    Assert.That(enemy, Is.Not.Null);

    float vidaInicial =
        playerHealth.CurrentHealth;

    playerObject.transform.position =
        enemy.transform.position +
        Vector3.forward * 20f;

    yield return new WaitForSeconds(0.5f);

    Assert.That(
        playerHealth.CurrentHealth,
        Is.EqualTo(vidaInicial),
        "El enemigo no debería atacar al jugador cuando está fuera del rango de ataque."
    );
}

[UnityTest]
public IEnumerator ElEnemigoRespetaElTiempoEntreAtaques()
{
    NetworkObject playerObject =
        networkManager.ConnectedClients[
            NetworkManager.ServerClientId
        ].PlayerObject;

    Assert.That(playerObject, Is.Not.Null);

    PlayerHealth playerHealth =
        playerObject.GetComponent<PlayerHealth>();

    Assert.That(playerHealth, Is.Not.Null);

    EnemyCombat enemy =
        Object.FindFirstObjectByType<EnemyCombat>();

    Assert.That(enemy, Is.Not.Null);

    float vidaInicial =
        playerHealth.CurrentHealth;

    playerObject.transform.position =
        enemy.transform.position +
        Vector3.forward * 1f;

    // Esperamos hasta que ocurra el primer ataque.
    yield return new WaitUntil(
        () => playerHealth.CurrentHealth < vidaInicial
    );

    float vidaDespuesDelPrimerAtaque =
        playerHealth.CurrentHealth;

    // Esperamos menos que el cooldown de 0.8 segundos.
    yield return new WaitForSeconds(0.3f);

    Assert.That(
        playerHealth.CurrentHealth,
        Is.EqualTo(vidaDespuesDelPrimerAtaque),
        "El enemigo no debería volver a atacar antes de que termine el cooldown."
    );

    // Esperamos lo suficiente para superar el cooldown.
    yield return new WaitForSeconds(0.6f);

    Assert.That(
        playerHealth.CurrentHealth,
        Is.LessThan(vidaDespuesDelPrimerAtaque),
        "El enemigo debería poder volver a atacar una vez finalizado el cooldown."
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
