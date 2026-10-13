
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class NpcTests
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
    public IEnumerator LaVidaDelNpcDisminuyeAlRecibirDano()
    {
        RescuableNpc npc =
            Object.FindFirstObjectByType<RescuableNpc>();

        Assert.That(npc, Is.Not.Null);
        Assert.That(npc.IsSpawned, Is.True);

        float vidaInicial = npc.CurrentHealth;
        float dano = 10f;

        npc.TakeDamage(dano);

        yield return null;

        Assert.That(
            npc.CurrentHealth,
            Is.EqualTo(vidaInicial - dano)
        );
    }

    [UnityTest]
    public IEnumerator ElNpcMuereAlAgotarSuVida()
    {
        RescuableNpc npc =
            Object.FindFirstObjectByType<RescuableNpc>();

        Assert.That(npc, Is.Not.Null);
        Assert.That(npc.IsSpawned, Is.True);
        Assert.That(npc.CurrentHealth, Is.GreaterThan(0f));

        npc.TakeDamage(npc.CurrentHealth);

        yield return new WaitUntil(
            () => npc == null || !npc.IsSpawned
        );

        Assert.That(
            npc == null || !npc.IsSpawned,
            Is.True
        );
    }

    [UnityTest]
    public IEnumerator ElNpcNoRecibeDanoEnZonaSegura()
    {
        RescuableNpc npc =
            Object.FindFirstObjectByType<RescuableNpc>();

        Assert.That(npc, Is.Not.Null);
        Assert.That(npc.IsSpawned, Is.True);

        npc.MarkSafe();

        yield return null;

        Assert.That(npc.IsTargetable, Is.False);

        float vidaAntesDelDano = npc.CurrentHealth;
        float dano = 10f;

        npc.TakeDamage(dano);

        yield return null;

        Assert.That(
            npc.CurrentHealth,
            Is.EqualTo(vidaAntesDelDano)
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
