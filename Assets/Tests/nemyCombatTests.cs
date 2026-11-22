
using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnemyCombatTests
{
    private NetworkManager networkManager;
    private NetworkObject playerObject;
    private PlayerHealth playerHealth;
    private EnemyCombat enemy;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Cargar TestScene desde cero.
        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Single);

        Assert.That(loadOperation, Is.Not.Null);

        yield return new WaitUntil(() => loadOperation.isDone);
        yield return null;

        // Esperar al NetworkManager.
        float timeout = Time.realtimeSinceStartup + 10f;

        while (NetworkManager.Singleton == null)
        {
            Assert.Less(
                Time.realtimeSinceStartup,
                timeout,
                "No se encontró el NetworkManager en TestScene."
            );

            yield return null;
        }

        networkManager = NetworkManager.Singleton;

        // Comprobar que no haya otro Host activo.
        Assert.That(
            networkManager.IsListening,
            Is.False,
            "El NetworkManager ya está escuchando."
        );

        // Iniciar el Host.
        bool hostStarted = networkManager.StartHost();

        Assert.That(
            hostStarted,
            Is.True,
            "No se pudo iniciar el Host."
        );

        timeout = Time.realtimeSinceStartup + 10f;

        while (!networkManager.IsListening)
        {
            Assert.Less(
                Time.realtimeSinceStartup,
                timeout,
                "El Host no llegó a iniciar."
            );

            yield return null;
        }

        // Esperar a que Netcode procese la conexión del Host.
        yield return null;
        yield return null;

        // Generar manualmente el jugador.
        // El proyecto deshabilita el spawn automático mediante Connection Approval.

        GameObject playerPrefab =
            networkManager.NetworkConfig.PlayerPrefab;

        Assert.That(
            playerPrefab,
            Is.Not.Null,
            "El NetworkManager no tiene asignado el PlayerPrefab."
        );

        GameObject player = Object.Instantiate(
            playerPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        NetworkObject playerNetworkObject =
            player.GetComponent<NetworkObject>();

        Assert.That(
            playerNetworkObject,
            Is.Not.Null,
            "El PlayerPrefab no tiene NetworkObject."
        );

        playerNetworkObject.SpawnAsPlayerObject(
            NetworkManager.ServerClientId,
            true
        );

        // Obtener el jugador generado.
        playerObject =
            networkManager.SpawnManager.GetLocalPlayerObject();

        Assert.That(
            playerObject,
            Is.Not.Null,
            "No se pudo generar el PlayerObject del Host."
        );

        playerHealth =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(
            playerHealth,
            Is.Not.Null,
            "El jugador no tiene el componente PlayerHealth."
        );

        // Esperar al enemigo.
        timeout = Time.realtimeSinceStartup + 10f;

        while (Object.FindFirstObjectByType<EnemyCombat>() == null)
        {
            Assert.Less(
                Time.realtimeSinceStartup,
                timeout,
                "No se encontró EnemyCombat en TestScene."
            );

            yield return null;
        }

        enemy = Object.FindFirstObjectByType<EnemyCombat>();

        Assert.That(enemy, Is.Not.Null);
        Assert.That(enemy.IsSpawned, Is.True);

        Assert.That(
            enemy.gameObject.activeInHierarchy,
            Is.True
        );
    }

    [UnityTest]
    public IEnumerator ElEnemigoAtacaAlJugadorDentroDelRango()
    {
        float vidaInicial = playerHealth.CurrentHealth;

        Vector3 attackPosition =
            enemy.transform.position + Vector3.forward * 1f;

        attackPosition.y = playerObject.transform.position.y;

        playerObject.transform.position = attackPosition;

        float timeout = Time.realtimeSinceStartup + 5f;

        while (playerHealth.CurrentHealth >= vidaInicial)
        {
            Assert.Less(
                Time.realtimeSinceStartup,
                timeout,
                "El enemigo no dañó al jugador dentro del rango."
            );

            yield return null;
        }

        Assert.That(
            playerHealth.CurrentHealth,
            Is.LessThan(vidaInicial)
        );
    }

    [UnityTest]
    public IEnumerator ElEnemigoNoAtacaAlJugadorFueraDelRango()
    {
        float vidaInicial = playerHealth.CurrentHealth;

        Vector3 distantPosition =
            enemy.transform.position + Vector3.forward * 20f;

        distantPosition.y = playerObject.transform.position.y;

        playerObject.transform.position = distantPosition;

        yield return new WaitForSeconds(1f);

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(vidaInicial),
            "El enemigo no debería dañar al jugador fuera del rango."
        );
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Apagar el Host.
        if (networkManager != null &&
            networkManager.IsListening &&
            !networkManager.ShutdownInProgress)
        {
            networkManager.Shutdown();

            float timeout = Time.realtimeSinceStartup + 10f;

            while (networkManager.IsListening ||
                   networkManager.ShutdownInProgress)
            {
                Assert.Less(
                    Time.realtimeSinceStartup,
                    timeout,
                    "El NetworkManager no terminó de apagarse."
                );

                yield return null;
            }
        }

        // Liberar referencias.
        networkManager = null;
        playerObject = null;
        playerHealth = null;
        enemy = null;

        // Descargar TestScene.
        if (SceneManager.GetActiveScene().name == "TestScene")
        {
            AsyncOperation unloadOperation =
                SceneManager.UnloadSceneAsync("TestScene");

            if (unloadOperation != null)
            {
                yield return new WaitUntil(
                    () => unloadOperation.isDone
                );
            }
        }

        yield return null;
    }
}
