
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class MedkitPickupTests
{
    private NetworkManager networkManager;
    private NetworkObject playerObject;
    private PlayerHealth playerHealth;
    private PlayerInventory inventory;
    private ItemPickup medkit;
    private int medkitItemId;

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
            throw hostTask.Exception;

        LogAssert.ignoreFailingMessages = true;

        bool partidaIniciada =
            GameSessionManager.StartGame("SampleScene");

        Assert.That(partidaIniciada, Is.True);

        yield return new WaitUntil(
            () => SceneManager.GetActiveScene().name == "SampleScene"
        );

        LogAssert.ignoreFailingMessages = false;

        // Esperar al jugador generado por el Host.
        yield return new WaitUntil(
            () => networkManager.ConnectedClients.ContainsKey(
                NetworkManager.ServerClientId
            ) &&
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject != null
        );

        playerObject =
            networkManager.ConnectedClients[
                NetworkManager.ServerClientId
            ].PlayerObject;

        Assert.That(playerObject, Is.Not.Null);

        playerHealth =
            playerObject.GetComponentInChildren<PlayerHealth>();

        inventory =
            playerObject.GetComponentInChildren<PlayerInventory>();

        Assert.That(
            playerHealth,
            Is.Not.Null,
            "No se encontró PlayerHealth en el jugador."
        );

        Assert.That(
            inventory,
            Is.Not.Null,
            "No se encontró PlayerInventory en el jugador ni en sus hijos."
        );

        // Esperar a que se inicialicen los slots del inventario.
        yield return new WaitUntil(
            () => inventory.LoadedSlotCount > 0
        );

        // Buscar un pickup cuyo efecto sea HealEffect.
        yield return new WaitUntil(
            () => FindMedkit() != null
        );

        medkit = FindMedkit();

        Assert.That(
            medkit,
            Is.Not.Null,
            "No se encontró ningún botiquín en SampleScene."
        );

        medkitItemId = medkit.ItemId;

        ItemDefinition definition =
            inventory.GetDefinition(medkitItemId);

        Assert.That(
            definition,
            Is.Not.Null,
            "El ItemId del botiquín no existe en el catálogo."
        );

        Assert.That(
            definition.useEffect,
            Is.InstanceOf<HealEffect>(),
            "El objeto encontrado no tiene un HealEffect."
        );
    }

    private ItemPickup FindMedkit()
    {
        ItemPickup[] pickups =
            Object.FindObjectsByType<ItemPickup>(
                FindObjectsSortMode.None
            );

        foreach (ItemPickup pickup in pickups)
        {
            if (pickup == null || !pickup.IsSpawned)
                continue;

            ItemDefinition definition =
                inventory.GetDefinition(pickup.ItemId);

            if (definition != null &&
                definition.useEffect is HealEffect)
            {
                return pickup;
            }
        }

        return null;
    }

    // Recoger el botiquín y esperar la actualización del inventario.
    private IEnumerator PickUpMedkit()
    {
        Assert.That(medkit, Is.Not.Null);
        Assert.That(medkit.IsSpawned, Is.True);

        // Colocar al jugador junto al botiquín.
        playerObject.transform.position =
            medkit.transform.position;

        yield return null;

        ulong pickupId = medkit.NetworkObjectId;

        inventory.RequestPickupItemServerRpc(pickupId);

        yield return new WaitUntil(
            () => inventory.CountOf(medkitItemId) > 0
        );

        yield return new WaitUntil(
            () => medkit == null || !medkit.IsSpawned
        );

        Assert.That(
            inventory.CountOf(medkitItemId),
            Is.GreaterThan(0)
        );
    }

    // Buscar el slot que contiene el botiquín.
    private int FindMedkitSlot()
    {
        for (int i = 0; i < inventory.LoadedSlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);

            if (!slot.IsEmpty &&
                slot.itemId == medkitItemId)
            {
                return i;
            }
        }

        return -1;
    }

    [UnityTest]
    public IEnumerator AlRecogerUnBotiquinSeAgregaAlInventario()
    {
        Assert.That(
            inventory.CountOf(medkitItemId),
            Is.EqualTo(0)
        );

        yield return PickUpMedkit();

        Assert.That(
            inventory.CountOf(medkitItemId),
            Is.EqualTo(1)
        );

        Assert.That(
            medkit == null || !medkit.IsSpawned,
            Is.True
        );
    }

    [UnityTest]
    public IEnumerator AlUsarUnBotiquinLaVidaDelJugadorAumenta()
    {
        playerHealth.TakeDamage(25f);

        yield return null;

        float vidaInicial =
            playerHealth.CurrentHealth;

        yield return PickUpMedkit();

        int slotIndex = FindMedkitSlot();

        Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));

        inventory.RequestUseSlotServerRpc(slotIndex);

        yield return new WaitUntil(
            () => playerHealth.CurrentHealth > vidaInicial
        );

        Assert.That(
            playerHealth.CurrentHealth,
            Is.GreaterThan(vidaInicial)
        );
    }

    [UnityTest]
    public IEnumerator AlUsarUnBotiquinSeConsumeDelInventario()
    {
        playerHealth.TakeDamage(25f);

        yield return null;

        yield return PickUpMedkit();

        Assert.That(
            inventory.CountOf(medkitItemId),
            Is.EqualTo(1)
        );

        int slotIndex = FindMedkitSlot();

        Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));

        inventory.RequestUseSlotServerRpc(slotIndex);

        yield return new WaitUntil(
            () => inventory.CountOf(medkitItemId) == 0
        );

        Assert.That(
            inventory.CountOf(medkitItemId),
            Is.EqualTo(0)
        );
    }

    [UnityTest]
    public IEnumerator UnJugadorConVidaMaximaNoPuedeUsarUnBotiquin()
    {
        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(playerHealth.MaxHealth)
        );

        yield return PickUpMedkit();

        int slotIndex = FindMedkitSlot();

        Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));

        inventory.RequestUseSlotServerRpc(slotIndex);

        yield return null;
        yield return null;

        Assert.That(
            playerHealth.CurrentHealth,
            Is.EqualTo(playerHealth.MaxHealth)
        );

        Assert.That(
            inventory.CountOf(medkitItemId),
            Is.EqualTo(1),
            "El botiquín no debería consumirse si el jugador tiene la vida completa."
        );
    }

    [UnityTest]
    public IEnumerator ElBotiquinNoPermiteSuperarLaVidaMaxima()
    {
        // Dejamos al jugador con un punto menos de vida.
        playerHealth.TakeDamage(1f);

        yield return null;

        Assert.That(
            playerHealth.CurrentHealth,
            Is.LessThan(playerHealth.MaxHealth)
        );

        yield return PickUpMedkit();

        int slotIndex = FindMedkitSlot();

        Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));

        inventory.RequestUseSlotServerRpc(slotIndex);

        yield return new WaitUntil(
            () => inventory.CountOf(medkitItemId) == 0
        );

        Assert.That(
            playerHealth.CurrentHealth,
            Is.LessThanOrEqualTo(playerHealth.MaxHealth)
        );

        Assert.That(
            playerHealth.CurrentHealth,
            Is.GreaterThan(0f)
        );
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
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

        playerObject = null;
        playerHealth = null;
        inventory = null;
        medkit = null;
        networkManager = null;
    }

    private async Task StartHost()
    {
        await GameSessionManager.PrepareHostAsync();

        await RelayConnectionManager.StartHostAsync(
            GameSessionManager.MaxPlayers - 1
        );
    }
}
