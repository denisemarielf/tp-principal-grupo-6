using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnemyHealthTests
{
    [UnitySetUp]
    public IEnumerator PrepararEscena()
    {
        yield return SceneManager.LoadSceneAsync("TestMovimiento");

        NetworkManager networkManager = NetworkManager.Singleton;

        Assert.IsNotNull(
            networkManager,
            "No se encontró un NetworkManager en la escena."
        );

        if (!networkManager.IsListening)
        {
            Assert.IsTrue(
                networkManager.StartHost(),
                "No se pudo iniciar el Host."
            );
        }

        yield return new WaitUntil(() =>
            networkManager.IsServer
        );

        yield return new WaitUntil(() =>
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            ).Length > 0
        );
    }

    [UnityTearDown]
    public IEnumerator LimpiarEscena()
    {
    NetworkManager networkManager = NetworkManager.Singleton;

    if (networkManager != null)
    {
        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        yield return null;

        Object.Destroy(networkManager.gameObject);

        yield return null;
    }

    if (SceneManager.GetSceneByName("TestMovimiento").isLoaded)
    {
        yield return SceneManager.UnloadSceneAsync("TestMovimiento");
    }
}

    [UnityTest]
    public IEnumerator LaVidaDelEnemigoDisminuyeAlRecibirDano()
    {
        EnemyHealth enemigo = Object.FindObjectsByType<EnemyHealth>(
            FindObjectsSortMode.None
        )[0];

        float vidaInicial = enemigo.GetCurrentHealth();

        enemigo.TakeDamage(25f);

        yield return null;

        float vidaFinal = enemigo.GetCurrentHealth();

        Assert.Less(
            vidaFinal,
            vidaInicial,
            "La vida del enemigo debería disminuir al recibir daño."
        );
    }

    [UnityTest]
public IEnumerator LaVidaDisminuyeExactamenteLaCantidadDeDanoRecibida()
{
    EnemyHealth enemigo = Object.FindObjectsByType<EnemyHealth>(
        FindObjectsSortMode.None
    )[0];

    float vidaInicial = enemigo.GetCurrentHealth();
    float dano = 25f;

    enemigo.TakeDamage(dano);

    yield return null;

    float vidaFinal = enemigo.GetCurrentHealth();

    Assert.AreEqual(
        vidaInicial - dano,
        vidaFinal,
        "La vida debería disminuir exactamente la cantidad de daño recibida."
    );
}

[UnityTest]
    public IEnumerator ElEnemigoMuereCuandoSuVidaLlegaACero()
    {
        EnemyHealth enemigo = Object.FindObjectsByType<EnemyHealth>(
            FindObjectsSortMode.None
        )[0];

        NetworkObject objetoRed = enemigo.GetComponent<NetworkObject>();

        float vidaActual = enemigo.GetCurrentHealth();

        Assert.Greater(
            vidaActual,
            0f,
            "El enemigo debería comenzar con vida mayor que 0."
        );

        enemigo.TakeDamage(vidaActual);

        yield return null;

        Assert.IsFalse(
            objetoRed.IsSpawned,
            "El enemigo debería dejar de estar spawneado cuando su vida llega a 0."
        );
    }
}