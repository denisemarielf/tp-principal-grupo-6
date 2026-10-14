
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnemyHealthTests
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

        Assert.IsTrue(
            networkManager.IsServer,
            "El Host debería estar iniciado."
        );

        LogAssert.ignoreFailingMessages = true;

        bool partidaIniciada =
            GameSessionManager.StartGame("SampleScene");

        Assert.That(partidaIniciada, Is.True);

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "SampleScene"
        );

        yield return new WaitUntil(
            () =>
            {
                EnemyHealth[] enemigos =
                    Object.FindObjectsByType<EnemyHealth>(
                        FindObjectsSortMode.None
                    );

                foreach (EnemyHealth enemigo in enemigos)
                {
                    NetworkObject objetoRed =
                        enemigo.GetComponent<NetworkObject>();

                    if (objetoRed != null && objetoRed.IsSpawned)
                        return true;
                }

                return false;
            }
        );

        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator LaVidaInicialCoincideConLaVarianteDelEnemigo()
    {
        EnemyHealth enemigo = ObtenerEnemigoSpawneado();

        Assert.AreEqual(
            EnemyHealth.HealthFor(enemigo.Variant),
            enemigo.GetCurrentHealth(),
            0.01f,
            "La vida inicial debería coincidir con la variante del enemigo."
        );

        Assert.AreEqual(
            enemigo.MaxHealth,
            enemigo.GetCurrentHealth(),
            0.01f,
            "La vida actual debería coincidir con la vida máxima."
        );

        yield return null;
    }

    [UnityTest]
    public IEnumerator LaVidaDelEnemigoDisminuyeAlRecibirDano()
    {
        EnemyHealth enemigo = ObtenerEnemigoSpawneado();

        float vidaInicial = enemigo.GetCurrentHealth();

        Assert.Greater(
            vidaInicial,
            25f,
            "El enemigo debe tener vida suficiente para recibir el daño."
        );

        enemigo.TakeDamage(25f);

        yield return null;

        Assert.Less(
            enemigo.GetCurrentHealth(),
            vidaInicial,
            "La vida del enemigo debería disminuir al recibir daño."
        );
    }

    [UnityTest]
    public IEnumerator LaVidaDisminuyeExactamenteLaCantidadDeDanoRecibida()
    {
        EnemyHealth enemigo = ObtenerEnemigoSpawneado();

        float vidaInicial = enemigo.GetCurrentHealth();
        float dano = 25f;

        Assert.Greater(
            vidaInicial,
            dano,
            "El enemigo debe tener vida suficiente para sobrevivir al daño."
        );

        enemigo.TakeDamage(dano);

        yield return null;

        Assert.AreEqual(
            vidaInicial - dano,
            enemigo.GetCurrentHealth(),
            0.01f,
            "La vida debería disminuir exactamente la cantidad de daño recibida."
        );
    }

    [UnityTest]
    public IEnumerator ElEnemigoMuereCuandoSuVidaLlegaACero()
    {
        EnemyHealth enemigo = ObtenerEnemigoSpawneado();

        NetworkObject objetoRed =
            enemigo.GetComponent<NetworkObject>();

        Assert.IsNotNull(
            objetoRed,
            "El enemigo debe tener un NetworkObject."
        );

        Assert.IsTrue(
            objetoRed.IsSpawned,
            "El enemigo debe estar spawneado en la red."
        );

        float vidaActual = enemigo.GetCurrentHealth();
        float duracionMuerte = enemigo.DeathDuration;

        Assert.Greater(
            vidaActual,
            0f,
            "El enemigo debería comenzar con vida mayor que cero."
        );

        enemigo.TakeDamage(vidaActual);

        Assert.IsTrue(
            enemigo.IsDead(),
            "El enemigo debería quedar muerto cuando su vida llega a cero."
        );

        float limite =
            Time.realtimeSinceStartup + duracionMuerte + 3f;

        while (
            objetoRed != null &&
            objetoRed.IsSpawned &&
            Time.realtimeSinceStartup < limite
        )
        {
            yield return null;
        }

        Assert.IsFalse(
            objetoRed != null && objetoRed.IsSpawned,
            "El enemigo debería dejar de estar spawneado después de morir."
        );
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        LogAssert.ignoreFailingMessages = false;

        if (SceneManager.GetActiveScene().name == "SampleScene")
        {
            SceneManager.LoadScene("MainMenu");

            yield return new WaitUntil(
                () => SceneManager.GetActiveScene().name == "MainMenu"
            );
        }

        if (
            networkManager != null &&
            networkManager.IsListening &&
            !networkManager.ShutdownInProgress
        )
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

    private EnemyHealth ObtenerEnemigoSpawneado()
    {
        EnemyHealth[] enemigos =
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            );

        foreach (EnemyHealth enemigo in enemigos)
        {
            NetworkObject objetoRed =
                enemigo.GetComponent<NetworkObject>();

            if (objetoRed != null && objetoRed.IsSpawned)
                return enemigo;
        }

        Assert.Fail(
            "No se encontró ningún enemigo spawneado en la red."
        );

        return null;
    }
}
