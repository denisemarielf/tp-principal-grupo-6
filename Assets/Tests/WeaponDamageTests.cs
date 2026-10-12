
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

        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator DispararAlEnemigo_AplicaElDanioCorrecto()
    {
        yield return EsperarEnemigos(1);
        PrepararEnemigosDeLaEscena();

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

        float vidaInicial =
            enemy.GetCurrentHealth();

        int danioEsperado =
            weaponLogic.damageAmount;

        ColocarEnemigoFrenteAlArma(enemy, weaponLogic);

        bool impacto =
            Physics.Raycast(
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

        MethodInfo tryShoot =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        Assert.That(tryShoot, Is.Not.Null);

        tryShoot.Invoke(shootLogic, null);

        yield return null;

        Assert.That(
            enemy.GetCurrentHealth(),
            Is.EqualTo(vidaInicial - danioEsperado),
            "El enemigo debería recibir exactamente el daño configurado en el arma."
        );
    }

    [UnityTest]
    public IEnumerator DispararSinImpactar_NoModificaLaVidaDelEnemigo()
    {
        yield return EsperarEnemigos(1);
        PrepararEnemigosDeLaEscena();

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

        float vidaInicial =
            enemy.GetCurrentHealth();

        // Colocar al enemigo fuera del alcance del Raycast.
        enemy.transform.position =
            weaponLogic.spawnPoint.position +
            weaponLogic.spawnPoint.forward *
            (weaponLogic.hitscanRange + 10f);

        Physics.SyncTransforms();

        MethodInfo tryShoot =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        Assert.That(tryShoot, Is.Not.Null);

        tryShoot.Invoke(shootLogic, null);

        yield return null;

        Assert.That(
            enemy.GetCurrentHealth(),
            Is.EqualTo(vidaInicial),
            "Un disparo que no impacta al enemigo no debería modificar su vida."
        );
    }

    [UnityTest]
    public IEnumerator DispararConDistintasArmas_CadaUnaAplicaSuDanio()
    {
        yield return EsperarEnemigos(2);
        PrepararEnemigosDeLaEscena();

        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        // Esperar a que estén disponibles las dos armas.
        yield return new WaitUntil(
            () =>
                weaponSwitcher.WeaponCount > 1 &&
                weaponSwitcher.GetShootAt(0) != null &&
                weaponSwitcher.GetShootAt(1) != null
        );

        WeaponLogic arma1 =
            weaponSwitcher.GetShootAt(0)
                .GetComponent<WeaponLogic>();

        WeaponLogic arma2 =
            weaponSwitcher.GetShootAt(1)
                .GetComponent<WeaponLogic>();

        Assert.IsNotNull(
            arma1,
            "No se encontró WeaponLogic en el arma 1."
        );

        Assert.IsNotNull(
            arma2,
            "No se encontró WeaponLogic en el arma 2."
        );

        EnemyHealth[] enemigos =
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            );

        Assert.IsTrue(
            enemigos.Length >= 2,
            "Se necesitan al menos dos enemigos para ejecutar esta prueba."
        );

        EnemyHealth enemigo1 = enemigos[0];
        EnemyHealth enemigo2 = enemigos[1];

        // Disparar con la primera arma.
        float vidaInicialEnemigo1 =
            enemigo1.GetCurrentHealth();

        ColocarEnemigoFrenteAlArma(enemigo1, arma1);

        Physics.SyncTransforms();

        MethodInfo metodoDisparo =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        Assert.IsNotNull(
            metodoDisparo,
            "No se encontró el método TryShoot."
        );

        metodoDisparo.Invoke(
            weaponSwitcher.GetShootAt(0),
            null
        );

        yield return null;

        float vidaFinalEnemigo1 =
            enemigo1.GetCurrentHealth();

        float danioRecibido1 =
            vidaInicialEnemigo1 - vidaFinalEnemigo1;

        Assert.AreEqual(
            arma1.damageAmount,
            danioRecibido1,
            0.01f,
            "La primera arma no aplicó el daño esperado."
        );

        // Alejar el primer enemigo para que no bloquee el siguiente disparo.
        enemigo1.transform.position =
            new Vector3(10000f, 10000f, 10000f);

        Physics.SyncTransforms();

        // Seleccionar la segunda arma.
        weaponSwitcher.SelectWeapon(1);

        yield return null;

        // Disparar con la segunda arma.
        float vidaInicialEnemigo2 =
            enemigo2.GetCurrentHealth();

        ColocarEnemigoFrenteAlArma(enemigo2, arma2);

        Physics.SyncTransforms();

        metodoDisparo.Invoke(
            weaponSwitcher.GetShootAt(1),
            null
        );

        yield return null;

        float vidaFinalEnemigo2 =
            enemigo2.GetCurrentHealth();

        float danioRecibido2 =
            vidaInicialEnemigo2 - vidaFinalEnemigo2;

        Assert.AreEqual(
            arma2.damageAmount,
            danioRecibido2,
            0.01f,
            "La segunda arma no aplicó el daño esperado."
        );
    }

    // Espera a que aparezca la cantidad de enemigos necesaria.
    // Si no aparecen en 15 segundos, falla con un mensaje claro.
    private IEnumerator EsperarEnemigos(int cantidad)
    {
        float tiempoLimite = 15f;
        float tiempoTranscurrido = 0f;

        while (
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            ).Length < cantidad &&
            tiempoTranscurrido < tiempoLimite
        )
        {
            tiempoTranscurrido += Time.unscaledDeltaTime;
            yield return null;
        }

        int enemigosEncontrados =
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            ).Length;

        Assert.That(
            enemigosEncontrados,
            Is.GreaterThanOrEqualTo(cantidad),
            $"Se esperaban {cantidad} enemigos, pero solo se encontraron {enemigosEncontrados}."
        );
    }

    // Prepara todos los enemigos presentes en la escena.
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

    // Desactiva la IA y el movimiento del enemigo.
    private void PrepararEnemigoParaPrueba(EnemyHealth enemy)
    {
        Ai ai = enemy.GetComponent<Ai>();

        if (ai != null)
            ai.enabled = false;

        NavMeshAgent agent =
            enemy.GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.enabled = false;
    }

    private void ColocarEnemigoFrenteAlArma(
        EnemyHealth enemy,
        WeaponLogic weaponLogic)
    {
        float distancia =
            weaponLogic.hitscanRange * 0.5f;

        enemy.transform.position =
            weaponLogic.spawnPoint.position +
            weaponLogic.spawnPoint.forward * distancia;

        Physics.SyncTransforms();
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
