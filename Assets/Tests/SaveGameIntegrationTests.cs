

using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SaveGameIntegrationTests
{
    private NetworkManager networkManager;
    private string savePath;

    // =====================================================
    // CONFIGURACION INICIAL
    // =====================================================

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

        yield return new WaitUntil(() => hostTask.IsCompleted);

        if (hostTask.IsFaulted)
            throw hostTask.Exception;

        LogAssert.ignoreFailingMessages = true;

        bool partidaIniciada =
            GameSessionManager.StartGame("SampleScene");

        Assert.That(partidaIniciada, Is.True);

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "SampleScene"
        );

        LogAssert.ignoreFailingMessages = false;

        // Esperar a que el jugador del host esté disponible.
        yield return new WaitUntil(() =>
            networkManager != null &&
            networkManager.IsListening &&
            networkManager.ConnectedClients.ContainsKey(
                NetworkManager.ServerClientId
            ) &&
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject != null
        );

        // Esperar a que la SafeZone esté registrada e inicializada.
        yield return new WaitUntil(() =>
            SafeZone.Current != null &&
            SafeZone.Current.IsSpawned
        );

        // Obtener exactamente la misma ruta que utiliza SaveGameService.
        savePath = SaveSystem.PathFor(CurrentGame.EnsureId());

        Debug.Log("SaveGameIntegrationTests: ruta de guardado: " + savePath);

        // Eliminar guardados anteriores para aislar la prueba.
        if (File.Exists(savePath))
            File.Delete(savePath);
    }

    // =====================================================
    // TEST 1: SALUD DEL JUGADOR
    // =====================================================

    [UnityTest]
    public IEnumerator GuardarPartida_RegistraCorrectamenteLaSalud()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        PlayerHealth health =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(health, Is.Not.Null);

        yield return MoverHostAZonaSegura(playerObject);

        float vidaMaximaEsperada = health.MaxHealth;
        float vidaInicial = health.CurrentHealth;

        Assert.That(health.IsDead, Is.False);
        Assert.That(vidaInicial, Is.GreaterThan(0f));

        float dano = Mathf.Min(35f, vidaInicial / 2f);

        health.TakeDamage(dano);

        float tiempoInicio = Time.realtimeSinceStartup;

        yield return new WaitUntil(() =>
            health.CurrentHealth < vidaInicial ||
            Time.realtimeSinceStartup - tiempoInicio > 5f
        );

        Assert.That(
            health.CurrentHealth,
            Is.LessThan(vidaInicial),
            "La salud del jugador no disminuyó después de aplicar daño."
        );

        float vidaEsperada = health.CurrentHealth;
        bool muertoEsperado = health.IsDead;

        bool terminado = false;
        bool guardadoCorrectamente = false;
        string mensaje = null;

        SaveGameService.RequestSave((ok, resultado) =>
        {
            guardadoCorrectamente = ok;
            mensaje = resultado;
            terminado = true;
        });

        yield return new WaitUntil(() => terminado);

        Assert.That(guardadoCorrectamente, Is.True, mensaje);

        Assert.That(
            File.Exists(savePath),
            Is.True,
            "No se encontró el archivo en: " + savePath
        );

        GameSaveData data =
            JsonUtility.FromJson<GameSaveData>(
                File.ReadAllText(savePath)
            );

        Assert.That(data, Is.Not.Null);
        Assert.That(data.players, Is.Not.Null);
        Assert.That(data.players.Count, Is.EqualTo(1));

        PlayerSaveData jugadorGuardado = data.players[0];

        Assert.That(
            jugadorGuardado.health,
            Is.EqualTo(vidaEsperada).Within(0.01f)
        );

        Assert.That(
            jugadorGuardado.maxHealth,
            Is.EqualTo(vidaMaximaEsperada).Within(0.01f)
        );

        Assert.That(
            jugadorGuardado.isDead,
            Is.EqualTo(muertoEsperado)
        );
    }

    // =====================================================
    // TEST 2: ARMAS DEL JUGADOR
    // =====================================================

    [UnityTest]
    public IEnumerator GuardarPartida_RegistraCorrectamenteLasArmas()
    {
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        WeaponLoadout loadout =
            playerObject.GetComponentInChildren<WeaponLoadout>();

        Assert.That(
            loadout,
            Is.Not.Null,
            "El jugador debe tener un WeaponLoadout."
        );

        yield return new WaitUntil(() =>
            loadout != null &&
            loadout.Count > 0
        );

        // Consultar los tipos reales de las ranuras.
        int[] tiposEsperados = new int[loadout.Count];

        for (int i = 0; i < loadout.Count; i++)
        {
            tiposEsperados[i] = loadout.GetSlotType(i);
        }

        // El guardado manual requiere que el host esté en zona segura.
        yield return MoverHostAZonaSegura(playerObject);

        bool terminado = false;
        bool guardadoCorrectamente = false;
        string mensaje = null;

        SaveGameService.RequestSave((ok, resultado) =>
        {
            guardadoCorrectamente = ok;
            mensaje = resultado;
            terminado = true;
        });

        yield return new WaitUntil(() => terminado);

        Assert.That(
            guardadoCorrectamente,
            Is.True,
            mensaje
        );

        Assert.That(
            File.Exists(savePath),
            Is.True,
            "No se encontró el archivo en: " + savePath
        );

        GameSaveData data =
            JsonUtility.FromJson<GameSaveData>(
                File.ReadAllText(savePath)
            );

        Assert.That(data, Is.Not.Null);
        Assert.That(data.players, Is.Not.Null);
        Assert.That(data.players.Count, Is.EqualTo(1));

        PlayerSaveData jugadorGuardado = data.players[0];

        Assert.That(
            jugadorGuardado.weapons,
            Is.Not.Null,
            "El jugador guardado debe tener una lista de armas."
        );

        Assert.That(
            jugadorGuardado.weapons.Count,
            Is.EqualTo(tiposEsperados.Length),
            "La cantidad de armas guardadas debe coincidir con las ranuras."
        );

        for (int i = 0; i < tiposEsperados.Length; i++)
        {
            WeaponSave armaGuardada =
                jugadorGuardado.weapons.Find(
                    arma => arma.slot == i
                );

            Assert.That(
                armaGuardada,
                Is.Not.Null,
                $"No se guardó el arma de la ranura {i}."
            );

            Assert.That(
                armaGuardada.weaponType,
                Is.EqualTo(tiposEsperados[i]),
                $"El tipo de arma guardado en la ranura {i} no coincide."
            );
        }
    }

    // =====================================================
    // TEST 3: INVENTARIO DEL JUGADOR
    // =====================================================

    [UnityTest]
    public IEnumerator GuardarPartida_RegistraCorrectamenteElInventario()
    {
        // Obtener el jugador anfitrión.
        NetworkObject playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        // Obtener su inventario.
        PlayerInventory inventory =
            playerObject.GetComponent<PlayerInventory>();

        Assert.That(inventory, Is.Not.Null);

        yield return new WaitUntil(
            () => inventory != null &&
                  inventory.LoadedSlotCount > 0
        );

        // ID del botiquín en el catálogo.
        const int idBotiquin = 1;

        Assert.That(
            inventory.GetDefinition(idBotiquin),
            Is.Not.Null,
            "El ID 1 debe corresponder a un objeto del catálogo."
        );

        // Agregar un botiquín al inventario.
        int cantidadAgregada = inventory.TryAdd(idBotiquin, 1);

        Assert.That(
            cantidadAgregada,
            Is.EqualTo(1),
            "El botiquín debe poder agregarse al inventario."
        );

        // Ubicar al anfitrión dentro de la zona segura.
        yield return MoverHostAZonaSegura(playerObject);

        // Registrar el contenido esperado de cada ranura.
        int cantidadRanuras = inventory.LoadedSlotCount;

        int[] idsEsperados = new int[cantidadRanuras];
        int[] cantidadesEsperadas = new int[cantidadRanuras];

        for (int i = 0; i < cantidadRanuras; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);

            idsEsperados[i] = slot.IsEmpty ? -1 : slot.itemId;
            cantidadesEsperadas[i] = slot.IsEmpty ? 0 : slot.quantity;
        }

        // Solicitar el guardado.
        bool terminado = false;
        bool guardadoCorrectamente = false;
        string mensaje = null;

        SaveGameService.RequestSave((ok, resultado) =>
        {
            guardadoCorrectamente = ok;
            mensaje = resultado;
            terminado = true;
        });

        yield return new WaitUntil(() => terminado);

        Assert.That(
            guardadoCorrectamente,
            Is.True,
            "No se pudo guardar la partida: " + mensaje
        );

        // Leer el archivo generado.
        Assert.That(
            File.Exists(savePath),
            Is.True,
            "No se encontró el archivo en: " + savePath
        );

        GameSaveData data =
            JsonUtility.FromJson<GameSaveData>(
                File.ReadAllText(savePath)
            );

        Assert.That(data, Is.Not.Null);
        Assert.That(data.players, Is.Not.Null);
        Assert.That(data.players.Count, Is.EqualTo(1));

        // Comprobar el inventario guardado.
        PlayerSaveData jugadorGuardado = data.players[0];

        Assert.That(
            jugadorGuardado.inventory,
            Is.Not.Null,
            "El guardado debe contener la lista del inventario."
        );

        Assert.That(
            jugadorGuardado.inventory.Count,
            Is.EqualTo(cantidadRanuras),
            "La cantidad de ranuras guardadas debe coincidir con la original."
        );

        for (int i = 0; i < cantidadRanuras; i++)
        {
            InventorySlotSave slotGuardado =
                jugadorGuardado.inventory[i];

            Assert.That(
                slotGuardado.itemId,
                Is.EqualTo(idsEsperados[i]),
                $"El ID del objeto en la ranura {i} no coincide."
            );

            Assert.That(
                slotGuardado.quantity,
                Is.EqualTo(cantidadesEsperadas[i]),
                $"La cantidad en la ranura {i} no coincide."
            );
        }

        // Confirmar que el botiquín quedó registrado en el guardado.
        Assert.That(
            jugadorGuardado.inventory.Exists(
                slot => slot.itemId == idBotiquin &&
                        slot.quantity >= 1
            ),
            Is.True,
            "El guardado debe contener al menos un botiquín."
        );
    }

    // =====================================================
    // UTILIDAD: MOVER HOST A ZONA SEGURA
    // =====================================================

    private IEnumerator MoverHostAZonaSegura(
        NetworkObject playerObject
    )
    {
        SafeZone zone = SafeZone.Current;

        Assert.That(
            zone,
            Is.Not.Null,
            "No se encontró una SafeZone registrada."
        );

        Assert.That(
            zone.IsSpawned,
            Is.True,
            "La SafeZone todavía no fue inicializada por Netcode."
        );

        Assert.That(
            zone.IsServer,
            Is.True,
            "La SafeZone debe ejecutarse en el servidor."
        );

        Collider zoneCollider = zone.GetComponent<Collider>();

        Assert.That(
            zoneCollider,
            Is.Not.Null,
            "La SafeZone no tiene Collider."
        );

        Assert.That(
            zoneCollider.enabled,
            Is.True,
            "El Collider de la SafeZone está deshabilitado."
        );

        Assert.That(
            zoneCollider.isTrigger,
            Is.True,
            "El Collider de la SafeZone debe estar marcado como Trigger."
        );

        PlayerHealth health =
            playerObject.GetComponent<PlayerHealth>();

        Assert.That(
            health,
            Is.Not.Null,
            "El jugador no tiene PlayerHealth."
        );

        // Primero sacar al jugador de la zona.
        Vector3 centro = zoneCollider.bounds.center;

        Vector3 posicionExterior =
            centro +
            zoneCollider.bounds.extents +
            Vector3.one * 2f;

        playerObject.transform.position = posicionExterior;

        Physics.SyncTransforms();

        yield return new WaitForFixedUpdate();

        // Después introducirlo en la zona.
        playerObject.transform.position = centro;

        Physics.SyncTransforms();

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        float tiempoInicio = Time.realtimeSinceStartup;

        yield return new WaitUntil(() =>
            zone == null ||
            health == null ||
            zone.IsPlayerInside(health) ||
            Time.realtimeSinceStartup - tiempoInicio > 5f
        );

        Assert.That(
            zone != null &&
            health != null &&
            zone.IsPlayerInside(health),
            Is.True,
            "El host no pudo registrarse dentro de la zona segura."
        );
    }

    // =====================================================
    // LIMPIEZA
    // =====================================================

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Volver al menú principal.
        if (SceneManager.GetActiveScene().name == "SampleScene")
        {
            SceneManager.LoadScene("MainMenu");

            yield return new WaitUntil(
                () => SceneManager.GetActiveScene().name == "MainMenu"
            );
        }

        // Cerrar la sesión de red.
        if (networkManager != null &&
            networkManager.IsListening &&
            !networkManager.ShutdownInProgress)
        {
            networkManager.Shutdown();

            yield return new WaitUntil(() =>
                !networkManager.ShutdownInProgress &&
                !networkManager.IsListening
            );
        }

        // Eliminar el archivo generado por el test.
        if (!string.IsNullOrEmpty(savePath) &&
            File.Exists(savePath))
        {
            File.Delete(savePath);
        }

        LogAssert.ignoreFailingMessages = false;
    }

    // =====================================================
    // INICIO DE SESION HOST
    // =====================================================

    private async Task StartHost()
    {
        await GameSessionManager.PrepareHostAsync();

        await RelayConnectionManager.StartHostAsync(
            GameSessionManager.MaxPlayers - 1
        );
    }
}


