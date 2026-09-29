using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RelayConnectionTests
{
    [UnityTest]
    public IEnumerator NoSePuedeUnirSinCodigo()
    {
        SceneManager.LoadScene("MainMenu");

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "MainMenu"
        );

        yield return new WaitUntil(
            () => NetworkManager.Singleton != null
        );

        Task clientTask = RelayConnectionManager.StartClientAsync("");

        float tiempoLimite = 5f;
        float tiempoInicio = Time.time;

        yield return new WaitUntil(
            () => clientTask.IsCompleted ||
                  Time.time - tiempoInicio >= tiempoLimite
        );

        Assert.That(
            clientTask.IsFaulted || !NetworkManager.Singleton.IsClient,
            Is.True
        );
    }

    [UnityTest]
public IEnumerator NoSePuedeUnirConCodigoInexistente()
{
    SceneManager.LoadScene("MainMenu");

    yield return new WaitUntil(
        () => SceneManager.GetActiveScene().name == "MainMenu"
    );

    yield return new WaitUntil(
        () => NetworkManager.Singleton != null
    );

    Task clientTask = RelayConnectionManager.StartClientAsync("ABC123");

    float tiempoLimite = 5f;
    float tiempoInicio = Time.time;

    yield return new WaitUntil(
        () => clientTask.IsCompleted ||
              Time.time - tiempoInicio >= tiempoLimite
    );

    Assert.That(
        clientTask.IsFaulted || !NetworkManager.Singleton.IsClient,
        Is.True
    );
}
}