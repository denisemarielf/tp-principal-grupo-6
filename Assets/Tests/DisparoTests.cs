using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class DisparoTests
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
    public IEnumerator DispararUnaVez_DisminuyeLaMunicionEnUnaBala()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        ShootLogic shootLogic =
            weaponSwitcher.CurrentWeaponLogic
                .GetComponent<ShootLogic>();

        Assert.That(shootLogic, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        int municionInicial =
            ammoManager.CurrentAmmo;

        Assert.That(
            municionInicial,
            Is.GreaterThan(0),
            "El arma debe comenzar con munición para realizar el disparo."
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
            ammoManager.CurrentAmmo,
            Is.EqualTo(municionInicial - 1),
            "Disparar una vez debería consumir exactamente una bala."
        );
    }

    [UnityTest]
    public IEnumerator DispararAntesDelTiempoPermitido_NoConsumeOtraBala()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        ShootLogic shootLogic =
            weaponSwitcher.CurrentWeaponLogic
                .GetComponent<ShootLogic>();

        Assert.That(shootLogic, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        MethodInfo tryShoot =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        Assert.That(tryShoot, Is.Not.Null);

        int municionInicial =
            ammoManager.CurrentAmmo;

        // Primer disparo
        tryShoot.Invoke(shootLogic, null);

        yield return null;

        int municionDespuesPrimerDisparo =
            ammoManager.CurrentAmmo;

        Assert.That(
            municionDespuesPrimerDisparo,
            Is.EqualTo(municionInicial - 1)
        );

        // Segundo disparo inmediatamente
        tryShoot.Invoke(shootLogic, null);

        yield return null;

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(municionDespuesPrimerDisparo),
            "No debería poder realizar otro disparo antes de que transcurra shootRate."
        );
    }

    [UnityTest]
    public IEnumerator SinMunicionEnElCargador_NoConsumeMunicion()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        ShootLogic shootLogic =
            weaponSwitcher.CurrentWeaponLogic
                .GetComponent<ShootLogic>();

        Assert.That(shootLogic, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        MethodInfo tryShoot =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        Assert.That(tryShoot, Is.Not.Null);

        // Dejamos el cargador vacío.
        while (ammoManager.CurrentAmmo > 0)
        {
            ammoManager.ConsumeShot();
        }

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(0)
        );

        int reservaInicial =
            ammoManager.ReserveAmmo;

        // Intentamos disparar sin munición.
        tryShoot.Invoke(shootLogic, null);

        yield return null;

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(0),
            "El arma no debería consumir munición cuando el cargador está vacío."
        );

        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(reservaInicial),
            "Este test no debería modificar la reserva de munición."
        );
    }

    [UnityTest]
    public IEnumerator Recargar_NoSuperaElTamanioMaximoDelCargador()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        int cargadorInicial =
            ammoManager.CurrentAmmo;

        int reservaInicial =
            ammoManager.ReserveAmmo;

        Assert.That(
            cargadorInicial,
            Is.GreaterThan(0)
        );

        // Consumimos algunas balas para que sea necesario recargar.
        ammoManager.ConsumeShot();
        ammoManager.ConsumeShot();

        int municionAntesDeRecargar =
            ammoManager.CurrentAmmo;

        Assert.That(
            municionAntesDeRecargar,
            Is.LessThan(cargadorInicial)
        );

        ammoManager.TryReload();

        yield return new WaitForSeconds(
            ammoManager.IsReloading
                ? 1.6f
                : 0.1f
        );

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(cargadorInicial),
            "La recarga no debería superar la capacidad máxima del cargador."
        );

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.LessThanOrEqualTo(cargadorInicial),
            "El cargador nunca debería superar su capacidad máxima."
        );
    }

    [UnityTest]
    public IEnumerator RecargarConReservaInsuficiente_CargaSoloLaMunicionDisponible()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        // Vaciamos completamente el cargador.
        while (ammoManager.CurrentAmmo > 0)
        {
            ammoManager.ConsumeShot();
        }

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(0)
        );

        // Dejamos solamente 3 balas en reserva.
        int reservaActual = ammoManager.ReserveAmmo;

        // Primero consumimos la reserva existente.
        ammoManager.AddReserveAmmo(-reservaActual);
        ammoManager.AddReserveAmmo(3);

        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(3)
        );

        ammoManager.TryReload();

        yield return new WaitForSeconds(1.6f);

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(3),
            "La recarga debería utilizar solamente las balas disponibles en la reserva."
        );

        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(0),
            "La reserva debería quedar en cero después de utilizar sus últimas balas."
        );
    }

    [UnityTest]
    public IEnumerator RecargarSinMunicionDeReserva_NoModificaLaMunicion()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        // Dejamos el cargador parcialmente vacío.
        ammoManager.ConsumeShot();
        ammoManager.ConsumeShot();

        int municionInicial =
            ammoManager.CurrentAmmo;

        // Dejamos la reserva en cero.
        int reservaActual =
            ammoManager.ReserveAmmo;

        ammoManager.AddReserveAmmo(-reservaActual);

        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(0)
        );

        ammoManager.TryReload();

        yield return null;

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(municionInicial),
            "No debería modificarse el cargador cuando no hay munición de reserva."
        );

        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(0),
            "La reserva debería permanecer en cero."
        );

        Assert.That(
            ammoManager.IsReloading,
            Is.False,
            "No debería iniciarse una recarga cuando no hay munición de reserva."
        );
    }

    [UnityTest]
    public IEnumerator DispararSinMunicion_IniciaLaRecargaAutomatica()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        ShootLogic shootLogic =
            weaponSwitcher.CurrentWeaponLogic
                .GetComponent<ShootLogic>();

        Assert.That(shootLogic, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        // Dejamos el cargador vacío.
        while (ammoManager.CurrentAmmo > 0)
        {
            ammoManager.ConsumeShot();
        }

        Assert.That(ammoManager.CurrentAmmo, Is.EqualTo(0));
        Assert.That(ammoManager.ReserveAmmo, Is.GreaterThan(0));

        MethodInfo tryShoot =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        Assert.That(tryShoot, Is.Not.Null);

        // Intentamos disparar sin balas.
        tryShoot.Invoke(shootLogic, null);

        yield return null;

        Assert.That(
            ammoManager.IsReloading,
            Is.True,
            "El arma debería iniciar la recarga automática al intentar disparar sin munición."
        );

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(0),
            "La recarga no debería llenar el cargador instantáneamente."
        );
    }

    [UnityTest]
    public IEnumerator DispararDuranteLaRecarga_NoConsumeMunicion()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        ShootLogic shootLogic =
            weaponSwitcher.CurrentWeaponLogic
                .GetComponent<ShootLogic>();

        Assert.That(shootLogic, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        // Consumimos dos balas para que sea necesario recargar.
        ammoManager.ConsumeShot();
        ammoManager.ConsumeShot();

        int municionAntesDeRecargar =
            ammoManager.CurrentAmmo;

        // Iniciamos la recarga.
        ammoManager.TryReload();

        Assert.That(
            ammoManager.IsReloading,
            Is.True,
            "La recarga debería haberse iniciado."
        );

        MethodInfo tryShoot =
            typeof(ShootLogic).GetMethod(
                "TryShoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        Assert.That(tryShoot, Is.Not.Null);

        // Intentamos disparar mientras recarga.
        tryShoot.Invoke(shootLogic, null);

        yield return null;

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(municionAntesDeRecargar),
            "No debería consumirse una bala mientras el arma está recargando."
        );

        Assert.That(
            ammoManager.IsReloading,
            Is.True,
            "El intento de disparo no debería cancelar la recarga."
        );
    }
        
    [UnityTest]
    public IEnumerator Recargar_DescuentaDeLaReservaLaCantidadCargada()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        // Dejamos el cargador con dos balas menos.
        ammoManager.ConsumeShot();
        ammoManager.ConsumeShot();

        int municionInicial = ammoManager.CurrentAmmo;
        int reservaInicial = ammoManager.ReserveAmmo;

        Assert.That(reservaInicial, Is.GreaterThanOrEqualTo(2));

        ammoManager.TryReload();

        yield return new WaitUntil(
            () => !ammoManager.IsReloading
        );

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(municionInicial + 2),
            "La recarga debería agregar las dos balas faltantes."
        );

        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(reservaInicial - 2),
            "La reserva debería disminuir exactamente en la cantidad de balas cargadas."
        );
    }

    
    [UnityTest]
    public IEnumerator RecargarConCargadorLleno_NoModificaLaMunicion()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponSwitcher weaponSwitcher =
            playerObject.GetComponentInChildren<WeaponSwitcher>();

        Assert.That(weaponSwitcher, Is.Not.Null);

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        Assert.That(ammoManager, Is.Not.Null);

        int municionInicial = ammoManager.CurrentAmmo;
        int reservaInicial = ammoManager.ReserveAmmo;

        Assert.That(
            municionInicial,
            Is.GreaterThan(0),
            "El arma debe tener munición en el cargador."
        );

        ammoManager.TryReload();

        yield return null;

        Assert.That(
            ammoManager.CurrentAmmo,
            Is.EqualTo(municionInicial),
            "La cantidad del cargador no debería cambiar."
        );

        Assert.That(
            ammoManager.ReserveAmmo,
            Is.EqualTo(reservaInicial),
            "La reserva debería permanecer igual si el cargador está lleno."
        );

        Assert.That(
            ammoManager.IsReloading,
            Is.False,
            "No debería iniciarse una recarga con el cargador lleno."
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