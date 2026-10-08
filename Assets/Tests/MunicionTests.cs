using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MunicionTests
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
    public IEnumerator RecogerMunicion_AgregaLaCantidadCorrectaALaReserva()
    {
        // Obtenemos el jugador del Host.
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        // Obtenemos el WeaponSwitcher del jugador.
        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        // Obtenemos el AmmoManager del arma actualmente equipada.
        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        // Buscamos una AmmoCase existente en la escena.
        AmmoCase ammoCase =
            Object.FindFirstObjectByType<AmmoCase>();

        Assert.That(ammoCase, Is.Not.Null);

        // Guardamos el estado inicial.
        int reservaInicial =
            ammoManager.ReserveAmmo;

        int cargadorInicial =
            ammoManager.CurrentAmmo;

        int cantidadCaja =
            (int)ammoCase.amount;

        // Movemos al jugador hacia la caja.
        playerObject.transform.position =
            ammoCase.transform.position;

        // Esperamos a que se procese la recogida.
        yield return new WaitUntil(
            () =>
                ammoManager.ReserveAmmo ==
                reservaInicial + cantidadCaja
        );

        // La cantidad de la caja se agrega a la reserva.
        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(reservaInicial + cantidadCaja),
            "La cantidad de munición de la caja debería agregarse a la reserva."
        );

        // El cargador no debe modificarse.
        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(cargadorInicial),
            "Recoger una caja de munición no debería modificar las balas del cargador."
        );
    }

    [UnityTest]
    public IEnumerator RecogerMunicion_SoloAfectaAlArmaEquipada()
    {
        // Obtenemos el jugador del Host.
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        // Obtenemos el WeaponSwitcher.
        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        // Buscamos cualquier pickup de arma disponible.
        WeaponPickup weaponPickup =
            Object.FindFirstObjectByType<WeaponPickup>();

        Assert.That(
            weaponPickup,
            Is.Not.Null,
            "Debe existir al menos un WeaponPickup en la escena."
        );

        // Obtenemos el NetworkObject del pickup.
        NetworkObject pickupNetworkObject =
            weaponPickup.GetComponent<NetworkObject>();

        Assert.That(
            pickupNetworkObject,
            Is.Not.Null,
            "El WeaponPickup debe tener un NetworkObject."
        );

        // Recogemos la segunda arma utilizando el flujo real del juego.
        weaponSwitcher.RequestPickupWeaponServerRpc(
            (int)weaponPickup.weaponType,
            pickupNetworkObject.NetworkObjectId
        );

        // Esperamos a que el segundo slot tenga el arma.
        yield return new WaitUntil(
            () =>
                weaponSwitcher.weapons.Length > 1 &&
                weaponSwitcher.weapons[1] != null
        );

        Assert.That(
            weaponSwitcher.weapons[1],
            Is.Not.Null,
            "El jugador debería tener una segunda arma después de recoger el pickup."
        );

        // Obtenemos el AmmoManager de cada arma.
        AmmoManager ammoArma1 =
            weaponSwitcher.weapons[0]
                .GetComponent<AmmoManager>();

        AmmoManager ammoArma2 =
            weaponSwitcher.weapons[1]
                .GetComponent<AmmoManager>();

        Assert.That(ammoArma1, Is.Not.Null);
        Assert.That(ammoArma2, Is.Not.Null);

        // Equipamos el primer arma.
        weaponSwitcher.SelectWeapon(0);

        yield return null;

        // Guardamos las reservas iniciales.
        int reservaArma1Inicial =
            ammoArma1.ReserveAmmo;

        int reservaArma2Inicial =
            ammoArma2.ReserveAmmo;

        // Buscamos la caja de munición.
        AmmoCase ammoCase =
            Object.FindFirstObjectByType<AmmoCase>();

        Assert.That(
            ammoCase,
            Is.Not.Null,
            "Debe existir una caja de munición en la escena."
        );

        int cantidadCaja =
            (int)ammoCase.amount;

        // Movemos al jugador hacia la caja.
        playerObject.transform.position =
            ammoCase.transform.position;

        // Esperamos a que el arma equipada reciba la munición.
        yield return new WaitUntil(
            () =>
                ammoArma1.ReserveAmmo ==
                reservaArma1Inicial + cantidadCaja
        );

        // El arma equipada debe recibir la munición.
        Assert.That(
            ammoArma1.ReserveAmmo,
            Is.EqualTo(reservaArma1Inicial + cantidadCaja),
            "El arma equipada debería recibir la munición."
        );

        // El arma no equipada no debe recibir munición.
        Assert.That(
            ammoArma2.ReserveAmmo,
            Is.EqualTo(reservaArma2Inicial),
            "El arma no equipada no debería recibir munición."
        );
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Ignoramos errores producidos por la escena durante la limpieza.
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
