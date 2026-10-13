
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class WeaponDamageTests
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

        // Esperar para comprobar si los enemigos aparecen con retraso.
        yield return new WaitForSeconds(2f);

        int cantidadEnemigos =
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            ).Length;

        Debug.Log($"Enemigos encontrados: {cantidadEnemigos}");

        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator DispararAlEnemigo_AplicaElDanioCorrecto()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        WeaponLogic weaponLogic =
            weaponSwitcher.CurrentWeaponLogic;

        Assert.That(weaponLogic, Is.Not.Null);

        ShootLogic shootLogic =
            weaponLogic.GetComponent<ShootLogic>();

        Assert.That(shootLogic, Is.Not.Null);

        EnemyHealth enemy =
            Object.FindFirstObjectByType<EnemyHealth>();

        Assert.That(
            enemy,
            Is.Not.Null,
            "Debe existir un enemigo en la escena."
        );

        PrepararEnemigoParaPrueba(enemy);

        float vidaInicial = enemy.GetCurrentHealth();

        int danioEsperado = weaponLogic.damageAmount;

        ColocarEnemigoFrenteAlArma(enemy, weaponLogic);

        Physics.SyncTransforms();

        bool impacto = Physics.Raycast(
            weaponLogic.spawnPoint.position,
            weaponLogic.spawnPoint.forward,
            out RaycastHit hit,
            weaponLogic.hitscanRange,
            weaponLogic.hitscanLayers
        );

        Debug.Log(
            $"[TEST ARMA 1] Raycast impactó: " +
            $"{(impacto ? hit.collider.name : "NADA")}"
        );

        MethodInfo tryShoot = typeof(ShootLogic).GetMethod(
            "TryShoot",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(tryShoot, Is.Not.Null);

        tryShoot.Invoke(shootLogic, null);

        // Esperar a que se procese el disparo y el daño.
        yield return new WaitForSeconds(0.5f);

        float vidaFinal = enemy.GetCurrentHealth();

        Debug.Log($"[TEST ARMA 1] Vida inicial: {vidaInicial}");
        Debug.Log($"[TEST ARMA 1] Daño esperado: {danioEsperado}");
        Debug.Log($"[TEST ARMA 1] Vida final: {vidaFinal}");
        Debug.Log($"[TEST ARMA 1] Raycast de diagnóstico: {impacto}");

        Assert.That(
            vidaFinal,
            Is.EqualTo(vidaInicial - danioEsperado),
            "El enemigo debería recibir exactamente el daño configurado en el arma."
        );
    }



    private IEnumerator EsperarEnemigos(int cantidad)
    {
        float tiempoEspera = 0f;

        while (Object.FindObjectsByType<EnemyHealth>(
                   FindObjectsSortMode.None
               ).Length < cantidad &&
               tiempoEspera < 10f)
        {
            tiempoEspera += Time.deltaTime;
            yield return null;
        }

        Assert.That(
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            ).Length,
            Is.GreaterThanOrEqualTo(cantidad),
            $"No aparecieron {cantidad} enemigos dentro del tiempo esperado."
        );
    }

    private void PrepararEnemigosDeLaEscena()
    {
        EnemyHealth[] enemigos =
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            );

        foreach (EnemyHealth enemy in enemigos)
        {
            PrepararEnemigoParaPrueba(enemy);
        }
    }

    private void PrepararEnemigoParaPrueba(EnemyHealth enemy)
    {
        Ai ai = enemy.GetComponent<Ai>();

        if (ai != null)
            ai.enabled = false;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.enabled = false;
    }


    private void ColocarEnemigoFrenteAlArma(
        EnemyHealth enemy,
        WeaponLogic weaponLogic)
    {
        Transform puntoDisparo = weaponLogic.spawnPoint;

        float distancia = weaponLogic.hitscanRange * 0.5f;

        // Colocar el enemigo sobre el eje exacto del disparo.
        Vector3 posicionObjetivo =
            puntoDisparo.position + puntoDisparo.forward * distancia;

        enemy.transform.position = posicionObjetivo;

        // Orientar también el enemigo hacia el arma.
        enemy.transform.LookAt(puntoDisparo.position);

        Physics.SyncTransforms();

        Debug.Log(
            $"[TEST ARMA] Origen: {puntoDisparo.position}, " +
            $"dirección: {puntoDisparo.forward}, " +
            $"posición del enemigo: {enemy.transform.position}"
        );
    }


    [UnityTearDown]
    public IEnumerator TearDown()
    {
        LogAssert.ignoreFailingMessages = true;

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

        LogAssert.ignoreFailingMessages = false;
    }

    private async Task StartHost()
    {
        await GameSessionManager.PrepareHostAsync();

        await RelayConnectionManager.StartHostAsync(
            GameSessionManager.MaxPlayers - 1
        );
    }
}
