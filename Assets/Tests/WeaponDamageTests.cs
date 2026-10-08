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

        float vidaInicial =
            enemy.GetCurrentHealth();

        int danioEsperado =
            weaponLogic.damageAmount;

        ColocarEnemigoFrenteAlArma(
            enemy,
            weaponLogic
        );

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

        float vidaInicial =
            enemy.GetCurrentHealth();

        // Lo colocamos fuera del alcance del Raycast.
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
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        // Obtener la segunda arma mediante un pickup.
        WeaponPickup weaponPickup =
            Object.FindFirstObjectByType<WeaponPickup>();

        Assert.That(
            weaponPickup,
            Is.Not.Null,
            "Debe existir un WeaponPickup en la escena."
        );

        NetworkObject pickupNetworkObject =
            weaponPickup.GetComponent<NetworkObject>();

        Assert.That(
            pickupNetworkObject,
            Is.Not.Null,
            "El WeaponPickup debe tener un NetworkObject."
        );

        weaponSwitcher.RequestPickupWeaponServerRpc(
            (int)weaponPickup.weaponType,
            pickupNetworkObject.NetworkObjectId
        );

        yield return new WaitUntil(
            () =>
                weaponSwitcher.weapons.Length > 1 &&
                weaponSwitcher.weapons[1] != null
        );

        WeaponLogic arma1 =
            weaponSwitcher.weapons[0]
                .GetComponent<WeaponLogic>();

        WeaponLogic arma2 =
            weaponSwitcher.weapons[1]
                .GetComponent<WeaponLogic>();

        Assert.That(arma1, Is.Not.Null);
        Assert.That(arma2, Is.Not.Null);

        Debug.Log($"[TEST] Daño arma 1: {arma1.damageAmount}");
        Debug.Log($"[TEST] Daño arma 2: {arma2.damageAmount}");

        // Obtener dos enemigos diferentes.
        EnemyHealth[] enemigos =
            Object.FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            );

        Assert.That(
            enemigos.Length,
            Is.GreaterThanOrEqualTo(2),
            "Se necesitan al menos dos enemigos para probar las dos armas."
        );

        EnemyHealth enemigo1 = enemigos[0];
        EnemyHealth enemigo2 = enemigos[1];

        PrepararEnemigoParaPrueba(enemigo1);
        PrepararEnemigoParaPrueba(enemigo2);

        MethodInfo tryShoot =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        Assert.That(tryShoot, Is.Not.Null);

        // ================================================
        // ARMA 1
        // ================================================

        weaponSwitcher.SelectWeapon(0);

        yield return null;

        float vidaInicialEnemigo1 =
            enemigo1.GetCurrentHealth();

        ColocarEnemigoFrenteAlArma(enemigo1, arma1);

        bool impacto1 =
            Physics.Raycast(
                arma1.spawnPoint.position,
                arma1.spawnPoint.forward,
                out RaycastHit hit1,
                arma1.hitscanRange,
                arma1.hitscanLayers
            );

        Debug.Log(
            $"[TEST ARMA 1] Raycast impactó: " +
            $"{(impacto1 ? hit1.collider.name : "NADA")}"
        );

        ShootLogic shootLogic1 =
            arma1.GetComponent<ShootLogic>();

        Assert.That(shootLogic1, Is.Not.Null);

        tryShoot.Invoke(shootLogic1, null);

        yield return null;

        float vidaPerdidaEnemigo1 =
            vidaInicialEnemigo1 -
            enemigo1.GetCurrentHealth();

        Debug.Log(
            $"[TEST ARMA 1] Vida inicial: {vidaInicialEnemigo1} | " +
            $"Vida final: {enemigo1.GetCurrentHealth()} | " +
            $"Vida perdida: {vidaPerdidaEnemigo1}"
        );

        Assert.That(
            vidaPerdidaEnemigo1,
            Is.EqualTo(arma1.damageAmount),
            "El arma 1 debería aplicar exactamente el daño configurado."
        );

        // Apartar el primer enemigo para evitar que bloquee
        // el disparo de la segunda arma.
        enemigo1.transform.position =
            new Vector3(10000f, 10000f, 10000f);

        Physics.SyncTransforms();

        // ================================================
        // ARMA 2
        // ================================================

        weaponSwitcher.SelectWeapon(1);

        yield return null;

        float vidaInicialEnemigo2 =
            enemigo2.GetCurrentHealth();

        ColocarEnemigoFrenteAlArma(enemigo2, arma2);

        bool impacto2 =
            Physics.Raycast(
                arma2.spawnPoint.position,
                arma2.spawnPoint.forward,
                out RaycastHit hit2,
                arma2.hitscanRange,
                arma2.hitscanLayers
            );

        Debug.Log(
            $"[TEST ARMA 2] Raycast impactó: " +
            $"{(impacto2 ? hit2.collider.name : "NADA")}"
        );

        ShootLogic shootLogic2 =
            arma2.GetComponent<ShootLogic>();

        Assert.That(shootLogic2, Is.Not.Null);

        tryShoot.Invoke(shootLogic2, null);

        yield return null;

        float vidaPerdidaEnemigo2 =
            vidaInicialEnemigo2 -
            enemigo2.GetCurrentHealth();

        Debug.Log(
            $"[TEST ARMA 2] Vida inicial: {vidaInicialEnemigo2} | " +
            $"Vida final: {enemigo2.GetCurrentHealth()} | " +
            $"Vida perdida: {vidaPerdidaEnemigo2}"
        );

        Assert.That(
            vidaPerdidaEnemigo2,
            Is.EqualTo(arma2.damageAmount),
            "El arma 2 debería aplicar exactamente el daño configurado."
        );
    }

    private void PrepararEnemigoParaPrueba(
        EnemyHealth enemy)
    {
        Ai ai =
            enemy.GetComponent<Ai>();

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
            weaponLogic.spawnPoint.forward *
            distancia;

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