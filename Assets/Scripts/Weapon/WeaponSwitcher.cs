using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;
using System;

public class WeaponSwitcher : NetworkBehaviour
{
    [System.Serializable]
    public struct WeaponEntry
    {
        public WeaponType type;
        public GameObject weaponObject;
    }

    public GameObject[] weapons;
    public AudioSource audioSource;
    public AudioClip switchSound;
    public AmmoUI ammoUI;

    [Header("Catálogo de armas (TODAS las que existen bajo WeaponPivot)")]
    public WeaponEntry[] allWeapons;

    [Header("Drop de armas")]
    [SerializeField] private GameObject weaponPickupPrefab; // debe estar registrado en NetworkManager > Network Prefabs

    [Header("CrossHair")]
    public CrosshairController crosshairController;

    [Header("Brazos")]
    public GameObject armsModel;
    public ArmsGripController armsGripController;
    private ShootLogic currentWeaponShoot;

    private NetworkVariable<int> networkWeaponIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // Qué WeaponType ocupa cada slot. -1 = slot vacío. Solo el servidor escribe,
    // todos leen: así cada cliente reconstruye weapons[] de forma consistente.
    private NetworkVariable<int> slot0Type = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> slot1Type = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int CurrentWeaponIndex => networkWeaponIndex.Value;
    public int WeaponCount => weapons.Length;
    public WeaponLogic CurrentWeaponLogic => currentWeaponShoot != null ? currentWeaponShoot.GetComponent<WeaponLogic>() : null;

    public override void OnNetworkSpawn()
    {
        foreach (var weapon in weapons)
        {
            if (weapon != null) weapon.SetActive(true);
        }

        int targetLayer = IsOwner
            ? LayerMask.NameToLayer("Weapon")
            : LayerMask.NameToLayer("Default");

        foreach (var entry in allWeapons)
        {
            if (entry.weaponObject != null) SetLayerRecursively(entry.weaponObject, targetLayer);
        }

        // El servidor inicializa los slots en base a lo que ya venía asignado
        // a mano en el Inspector (el loadout inicial por defecto).
        if (IsServer)
        {
            slot0Type.Value = weapons.Length > 0 ? (int)GetTypeForWeaponObject(weapons[0]) : -1;
            slot1Type.Value = weapons.Length > 1 ? (int)GetTypeForWeaponObject(weapons[1]) : -1;
        }

        slot0Type.OnValueChanged += (_, __) => RebuildSlot(0);
        slot1Type.OnValueChanged += (_, __) => RebuildSlot(1);

        networkWeaponIndex.OnValueChanged += OnWeaponIndexChanged;
        if (IsOwner)
        {
            StartCoroutine(BuscarAmmoUI());
            SelectWeapon(0);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            UpdateWeaponVisuals(networkWeaponIndex.Value);
            StartCoroutine(ReforzarVisualTrasSpawn());
        }
    }

    public override void OnNetworkDespawn()
    {
        networkWeaponIndex.OnValueChanged -= OnWeaponIndexChanged;

        if (IsOwner)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ammoUI = null;
        StartCoroutine(BuscarAmmoUI());
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private IEnumerator ReforzarVisualTrasSpawn()
    {
        yield return null;
        yield return null;
        UpdateWeaponVisuals(networkWeaponIndex.Value);
    }

    private IEnumerator BuscarAmmoUI()
    {
        while (ammoUI == null)
        {
            ammoUI = FindAnyObjectByType<AmmoUI>();
            if (ammoUI == null) yield return null;
        }
        ammoUI.SetWeapon(currentWeaponShoot);
    }

    private void OnWeaponIndexChanged(int oldIndex, int newIndex)
    {
        UpdateWeaponVisuals(newIndex);
    }

    private void UpdateWeaponVisuals(int index)
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null) continue;

            bool isSelected = (i == index);
            foreach (var renderer in weapons[i].GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = isSelected;
                Debug.Log($"[UpdateWeaponVisuals] {weapons[i].name} -> {renderer.name}.enabled = {isSelected}");
            }

            ShootLogic shootLogic = weapons[i].GetComponent<ShootLogic>();
            if (shootLogic != null)
            {
                shootLogic.SetEquipped(isSelected);
            }
        }
    }

    public void SelectWeapon(int index)
    {
        UpdateWeaponVisuals(index);

        currentWeaponShoot = GetShootAt(index);

        if (armsModel != null)
        {
            armsModel.SetActive(currentWeaponShoot != null);
        }

        if (armsGripController != null && currentWeaponShoot != null)
        {
            armsGripController.SetGripsForWeapon(weapons[index]);
        }

        if (crosshairController != null)
        {
            crosshairController.SetVisible(currentWeaponShoot != null);
        }

        if (ammoUI != null)
        {
            ammoUI.SetWeapon(currentWeaponShoot);
        }
        PlaySwitchSound();

        if (IsOwner)
        {
            networkWeaponIndex.Value = index;
        }
    }

    public void Shoot(InputAction.CallbackContext context)
    {
        if (currentWeaponShoot != null)
        {
            currentWeaponShoot.OnShoot(context);
        }
    }

    public void Reload(InputAction.CallbackContext context)
    {
        if (currentWeaponShoot != null)
        {
            currentWeaponShoot.OnReload(context);
        }
    }

    public ShootLogic GetShootAt(int index)
    {
        return (index >= 0 && index < weapons.Length && weapons[index] != null)
            ? weapons[index].GetComponent<ShootLogic>()
            : null;
    }

    private void PlaySwitchSound()
    {
        if (audioSource != null && switchSound != null)
        {
            audioSource.PlayOneShot(switchSound);
        }
    }

    // ---------- Sistema de pickup ----------

    private GameObject GetWeaponObjectByType(WeaponType type)
    {
        foreach (var entry in allWeapons)
        {
            if (entry.type == type) return entry.weaponObject;
        }
        return null;
    }

    private WeaponType GetTypeForWeaponObject(GameObject weaponObject)
    {
        foreach (var entry in allWeapons)
        {
            if (entry.weaponObject == weaponObject) return entry.type;
        }
        return (WeaponType)(-1);
    }

    private bool HasWeaponType(WeaponType type)
    {
        return slot0Type.Value == (int)type || slot1Type.Value == (int)type;
    }

    // Se llama cada vez que slot0Type/slot1Type cambian, en TODOS los clientes.
    private void RebuildSlot(int slotIndex)
    {
        int typeValue = slotIndex == 0 ? slot0Type.Value : slot1Type.Value;
        GameObject newWeaponObject = typeValue >= 0 ? GetWeaponObjectByType((WeaponType)typeValue) : null;

        GameObject oldWeaponObject = weapons[slotIndex];

        // Si había otra arma antes en este slot, la apagamos a mano: al salir
        // del array weapons[], UpdateWeaponVisuals ya nunca más la va a tocar.
        if (oldWeaponObject != null && oldWeaponObject != newWeaponObject)
        {
            foreach (var renderer in oldWeaponObject.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            ShootLogic oldShoot = oldWeaponObject.GetComponent<ShootLogic>();
            oldShoot?.SetEquipped(false);
        }

        weapons[slotIndex] = newWeaponObject;

        if (newWeaponObject != null)
        {
            newWeaponObject.SetActive(true);
            int targetLayer = IsOwner ? LayerMask.NameToLayer("Weapon") : LayerMask.NameToLayer("Default");
            SetLayerRecursively(newWeaponObject, targetLayer);
            Debug.Log($"[RebuildSlot] {newWeaponObject.name} | activeInHierarchy: {newWeaponObject.activeInHierarchy} | layer: {newWeaponObject.layer} | renderers encontrados: {newWeaponObject.GetComponentsInChildren<Renderer>(true).Length}");
        }


        if (slotIndex == networkWeaponIndex.Value)
        {
            SelectWeapon(slotIndex);
        }
        else
        {
            UpdateWeaponVisuals(networkWeaponIndex.Value);
        }
    }

    /// <summary>Llamado por Character.OnInteraction() cuando el dueño presiona E cerca de un WeaponPickup.</summary>
    [ServerRpc]
    public void RequestPickupWeaponServerRpc(int weaponTypeIndex, ulong pickupNetworkObjectId)
    {
        WeaponType type = (WeaponType)weaponTypeIndex;

        // Ya tenés esta arma: no hacer nada (el pickup se queda en el piso).
        if (HasWeaponType(type)) return;

        GameObject weaponObj = GetWeaponObjectByType(type);
        if (weaponObj == null) return; // esta arma no existe en el catálogo de este jugador

        if (slot0Type.Value < 0)
        {
            slot0Type.Value = weaponTypeIndex;
        }
        else if (slot1Type.Value < 0)
        {
            slot1Type.Value = weaponTypeIndex;
        }
        else
        {
            // Los 2 slots están ocupados: reemplazamos el arma que tenés EN LA MANO,
            // y esa cae al piso.
            int slotToReplace = networkWeaponIndex.Value >= 0 ? networkWeaponIndex.Value : 0;
            int droppedTypeValue = slotToReplace == 0 ? slot0Type.Value : slot1Type.Value;

            if (slotToReplace == 0) slot0Type.Value = weaponTypeIndex;
            else slot1Type.Value = weaponTypeIndex;

            SpawnDroppedWeapon((WeaponType)droppedTypeValue, transform.position + transform.forward * 1f + Vector3.up * 0.3f);
        }

        // Hacemos desaparecer el pickup del mundo para todos.
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickupNetworkObjectId, out NetworkObject pickupNetObj))
        {
            WeaponPickup pickup = pickupNetObj.GetComponent<WeaponPickup>();
            pickup?.Consume();
        }
    }

    private void SpawnDroppedWeapon(WeaponType type, Vector3 position)
    {
        if (weaponPickupPrefab == null) return;

        GameObject dropped = Instantiate(weaponPickupPrefab, position, Quaternion.identity);
        WeaponPickup pickup = dropped.GetComponent<WeaponPickup>();
        if (pickup != null)
        {
            pickup.weaponType = type;
        }

        NetworkObject netObj = dropped.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
    }
}