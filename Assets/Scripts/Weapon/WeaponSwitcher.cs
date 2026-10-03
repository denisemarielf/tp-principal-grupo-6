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

    [System.Serializable]
    public struct WeaponPickupEntry
    {
        public WeaponType type;
        public GameObject pickupPrefab;
    }

    public GameObject[] weapons;
    public AudioSource audioSource;
    public AudioClip switchSound;
    public AmmoUI ammoUI;

    [Header("Catálogo de armas (TODAS las que existen bajo WeaponPivot)")]
    public WeaponEntry[] allWeapons;

    [Header("Drop de armas")]
    [Tooltip("Un prefab de pickup por cada tipo de arma (cada uno ya con su propio mesh y weaponType configurado).")]
    [SerializeField] private WeaponPickupEntry[] pickupPrefabs;

    [Header("CrossHair")]
    public CrosshairController crosshairController;

    [Header("Brazos")]
    public GameObject armsModel;
    public ArmsGripController armsGripController;
    public ArmsGripController characterModelGripController;

    private ShootLogic currentWeaponShoot;


    private NetworkVariable<int> networkWeaponIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // Qué WeaponType ocupa cada slot.
    // -1 = slot vacío.
    private NetworkVariable<int> slot0Type = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> slot1Type = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int CurrentWeaponIndex => networkWeaponIndex.Value;

    public int WeaponCount => weapons.Length;

    public WeaponLogic CurrentWeaponLogic =>
        currentWeaponShoot != null
            ? currentWeaponShoot.GetComponent<WeaponLogic>()
            : null;
    public AmmoManager CurrentAmmoManager
    {
        get
        {
            if (currentWeaponShoot == null)
                return null;

            return currentWeaponShoot.GetComponent<AmmoManager>();
        }
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        foreach (var entry in allWeapons)
        {
            if (entry.weaponObject != null)
            {
                entry.weaponObject.SetActive(true);
            }
        }

        // NUEVO: los brazos en primera persona (ArmsSoldier) SOLO los debe
        // renderizar la WeaponCamera propia de cada jugador. Nadie más -ni
        // siquiera otros jugadores mirando a este personaje- debe verlos
        // jamás, sea o no el dueño. Por eso van siempre a "Weapon", sin
        // depender de IsOwner.
        if (armsModel != null)
        {
            SetLayerRecursively(armsModel, LayerMask.NameToLayer("Weapon"));
        }

        // Layer de las armas: Owner -> Weapon, Otros jugadores -> Default
        int targetLayer = IsOwner
            ? LayerMask.NameToLayer("Weapon")
            : LayerMask.NameToLayer("Default");

        foreach (var entry in allWeapons)
        {
            if (entry.weaponObject != null)
            {
                SetLayerRecursively(entry.weaponObject, targetLayer);
            }
        }


        // El servidor inicializa los slots
        // según las armas asignadas inicialmente en el Inspector.
        if (IsServer)
        {
            slot0Type.Value =
                weapons.Length > 0
                    ? (int)GetTypeForWeaponObject(weapons[0])
                    : -1;

            slot1Type.Value =
                weapons.Length > 1
                    ? (int)GetTypeForWeaponObject(weapons[1])
                    : -1;
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

        StartCoroutine(DeferredCatalogWeaponsActivity());
    }


    public override void OnNetworkDespawn()
    {
        networkWeaponIndex.OnValueChanged -= OnWeaponIndexChanged;

        if (IsOwner)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }


    // =========================================================
    // SCENE / UI
    // =========================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ammoUI = null;

        StartCoroutine(BuscarAmmoUI());
    }


    private IEnumerator BuscarAmmoUI()
    {
        while (ammoUI == null)
        {
            ammoUI = FindAnyObjectByType<AmmoUI>();

            if (ammoUI == null)
            {
                yield return null;
            }
        }

        ammoUI.SetWeapon(currentWeaponShoot);
    }


    private IEnumerator ReforzarVisualTrasSpawn()
    {
        yield return null;
        yield return null;

        UpdateWeaponVisuals(networkWeaponIndex.Value);
    }


    // =========================================================
    // LAYERS
    // =========================================================


    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }


    // =========================================================
    // VISIBILIDAD DE ARMAS NO EQUIPADAS (catálogo completo)
    // =========================================================

    /// <summary>
    /// Recorre TODO el catálogo (allWeapons). Si un arma no está en el loadout
    /// actual (weapons[]), desactiva el GameObject de su WeaponModel para que
    /// no quede molestando en el mundo (colisiones, renders fantasma, etc.).
    /// Las armas que SÍ están en el loadout (aunque no sean la equipada en mano)
    /// mantienen su WeaponModel activo; UpdateWeaponVisuals ya se encarga de
    /// mostrarlas/ocultarlas con Renderer.enabled nada más.
    /// </summary>
    private void UpdateCatalogWeaponsActivity()
    {
        foreach (var entry in allWeapons)
        {
            if (entry.weaponObject == null) continue;

            bool isInLoadout = System.Array.IndexOf(weapons, entry.weaponObject) != -1;

            Transform weaponModel = entry.weaponObject.transform.Find("WeaponModel");
            if (weaponModel != null)
            {
                weaponModel.gameObject.SetActive(isInLoadout);
            }
        }
    }
    private IEnumerator DeferredCatalogWeaponsActivity()
    {
        // Esperamos un frame para que Netcode termine de procesar el
        // OnNetworkSpawn de TODOS los NetworkBehaviour de este jugador
        // (incluido WeaponSway en cada arma) antes de desactivar nada.
        yield return null;
        UpdateCatalogWeaponsActivity();
    }

    // =========================================================
    // VISIBILIDAD DE ARMAS
    // =========================================================

    /// <summary>
    /// Muestra u oculta visualmente un arma.
    /// NO desactiva el GameObject ni sus scripts.
    /// </summary>
    private void SetWeaponVisible(GameObject weapon, bool visible)
    {
        if (weapon == null)
            return;

        Renderer[] renderers =
            weapon.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = visible;
        }
    }


    // =========================================================
    // CAMBIO DE ARMA
    // =========================================================

    private void OnWeaponIndexChanged(int oldIndex, int newIndex)
    {
        UpdateWeaponVisuals(newIndex);
    }


    private void UpdateWeaponVisuals(int index)
    {
        string arrayState = string.Join(
            ", ",
            System.Array.ConvertAll(
                weapons,
                w => w != null ? w.name : "null"
            )
        );

      



        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null)
                continue;

            bool isSelected = (i == index);

            // -------------------------------------------------
            // VISUAL
            // -------------------------------------------------
            // Solo ocultamos/mostramos los Renderer.
            // El GameObject permanece activo.
            SetWeaponVisible(weapons[i], isSelected);


            // -------------------------------------------------
            // LÓGICA DE DISPARO
            // -------------------------------------------------
            ShootLogic shootLogic =
                weapons[i].GetComponent<ShootLogic>();

            if (shootLogic != null)
            {
                shootLogic.SetEquipped(isSelected);
            }
        }
    }


    public void SelectWeapon(int index)
    {
        // -1 es el índice reservado para "sin arma" (enfundar) y SIEMPRE debe
        // poder procesarse: NO es un índice inválido, es un valor centinela
        // intencional. Solo bloqueamos índices realmente fuera de rango.
        if (index != -1 && (index < 0 || index >= weapons.Length))
            return;

        // Para un slot real (0, 1, ...) vacío, no hay nada que equipar todavía.
        if (index >= 0 && weapons[index] == null)
            return;


        // Actualiza visuales y ShootLogic
        UpdateWeaponVisuals(index);


        // Obtiene el ShootLogic del arma seleccionada (null si index es -1)
        currentWeaponShoot = GetShootAt(index);


        // =====================================================
        // BRAZOS
        // =====================================================

        if (armsModel != null)
        {
            armsModel.SetActive(currentWeaponShoot != null);
        }

        if (armsGripController != null && currentWeaponShoot != null)
        {
            armsGripController.SetGripsForWeapon(
                weapons[index]
            );
        }


        // =====================================================
        // CROSSHAIR
        // =====================================================

        if (crosshairController != null)
        {
            crosshairController.SetVisible(
                currentWeaponShoot != null
            );
        }


        // =====================================================
        // AMMO UI
        // =====================================================

        if (ammoUI != null)
        {
            ammoUI.SetWeapon(currentWeaponShoot);
        }


        // =====================================================
        // SONIDO
        // =====================================================

        PlaySwitchSound();


        // =====================================================
        // NETWORK
        // =====================================================

        if (IsOwner)
        {
            networkWeaponIndex.Value = index;
        }
    }


    // =========================================================
    // INPUT
    // =========================================================

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


    // =========================================================
    // OBTENER SHOOT LOGIC
    // =========================================================

    public ShootLogic GetShootAt(int index)
    {
        if (
            index >= 0 &&
            index < weapons.Length &&
            weapons[index] != null
        )
        {
            return weapons[index].GetComponent<ShootLogic>();
        }

        return null;
    }


    // =========================================================
    // SONIDO
    // =========================================================

    private void PlaySwitchSound()
    {
        if (
            audioSource != null &&
            switchSound != null
        )
        {
            audioSource.PlayOneShot(switchSound);
        }
    }


    // =========================================================
    // SISTEMA DE PICKUP
    // =========================================================

    private GameObject GetWeaponObjectByType(WeaponType type)
    {
        foreach (var entry in allWeapons)
        {
            if (entry.type == type)
            {
                return entry.weaponObject;
            }
        }

        return null;
    }


    private WeaponType GetTypeForWeaponObject(
        GameObject weaponObject
    )
    {
        foreach (var entry in allWeapons)
        {
            if (entry.weaponObject == weaponObject)
            {
                return entry.type;
            }
        }

        return (WeaponType)(-1);
    }


    /// <summary>Busca, dentro de 'pickupPrefabs', el prefab de pickup específico configurado para ese tipo de arma.</summary>
    private GameObject GetPickupPrefabByType(WeaponType type)
    {
        foreach (var entry in pickupPrefabs)
        {
            if (entry.type == type)
            {
                return entry.pickupPrefab;
            }
        }

        return null;
    }


    private bool HasWeaponType(WeaponType type)
    {
        return
            slot0Type.Value == (int)type ||
            slot1Type.Value == (int)type;
    }


    // =========================================================
    // RECONSTRUIR SLOT
    // =========================================================

    /// <summary>
    /// Se llama cada vez que slot0Type/slot1Type cambian.
    /// Se ejecuta en todos los clientes.
    /// </summary>
    private void RebuildSlot(int slotIndex)
    {
        int typeValue =
            slotIndex == 0
                ? slot0Type.Value
                : slot1Type.Value;


        GameObject newWeaponObject =
            typeValue >= 0
                ? GetWeaponObjectByType(
                    (WeaponType)typeValue
                )
                : null;


        GameObject oldWeaponObject =
            weapons[slotIndex];


        // =====================================================
        // OCULTAR ARMA ANTERIOR
        // =====================================================

        if (
            oldWeaponObject != null &&
            oldWeaponObject != newWeaponObject
        )
        {
            // IMPORTANTE:
            // No hacemos SetActive(false).
            // Solo ocultamos sus Renderer.
            SetWeaponVisible(
                oldWeaponObject,
                false
            );


            ShootLogic oldShoot =
                oldWeaponObject.GetComponent<ShootLogic>();

            if (oldShoot != null)
            {
                oldShoot.SetEquipped(false);
            }
        }


        // =====================================================
        // ASIGNAR NUEVA ARMA AL SLOT
        // =====================================================

        weapons[slotIndex] =
            newWeaponObject;


        if (newWeaponObject != null)
        {
            // El GameObject permanece activo.
            newWeaponObject.SetActive(true);


            int targetLayer =
                IsOwner
                    ? LayerMask.NameToLayer("Weapon")
                    : LayerMask.NameToLayer("Default");


            SetLayerRecursively(
                newWeaponObject,
                targetLayer
            );
        }


        // =====================================================
        // ACTUALIZAR VISUAL
        // =====================================================


        UpdateCatalogWeaponsActivity();
        if (slotIndex == networkWeaponIndex.Value)
        {
            SelectWeapon(slotIndex);
        }
        else
        {
            UpdateWeaponVisuals(
                networkWeaponIndex.Value
            );
        }
    }


    // =========================================================
    // PICKUP - SERVER
    // =========================================================

    /// <summary>
    /// Llamado por Character.OnInteraction()
    /// cuando el dueño presiona E cerca de un WeaponPickup.
    /// </summary>
    [ServerRpc]
    public void RequestPickupWeaponServerRpc(
        int weaponTypeIndex,
        ulong pickupNetworkObjectId
    )
    {
        WeaponType type =
            (WeaponType)weaponTypeIndex;


        // Ya tenemos esta arma.
        // No hacemos nada y el pickup permanece.
        if (HasWeaponType(type))
            return;


        GameObject weaponObj =
            GetWeaponObjectByType(type);


        if (weaponObj == null)
            return;


        // =====================================================
        // SLOT 0 LIBRE
        // =====================================================

        if (slot0Type.Value < 0)
        {
            slot0Type.Value =
                weaponTypeIndex;
        }


        // =====================================================
        // SLOT 1 LIBRE
        // =====================================================

        else if (slot1Type.Value < 0)
        {
            slot1Type.Value =
                weaponTypeIndex;
        }


        // =====================================================
        // AMBOS SLOTS OCUPADOS
        // =====================================================

        else
        {
            // Reemplazamos el arma que tenemos en la mano.
            int slotToReplace =
                networkWeaponIndex.Value >= 0
                    ? networkWeaponIndex.Value
                    : 0;


            int droppedTypeValue =
                slotToReplace == 0
                    ? slot0Type.Value
                    : slot1Type.Value;


            if (slotToReplace == 0)
            {
                slot0Type.Value =
                    weaponTypeIndex;
            }
            else
            {
                slot1Type.Value =
                    weaponTypeIndex;
            }


            // Tiramos al suelo el arma reemplazada.
            SpawnDroppedWeapon(
                (WeaponType)droppedTypeValue,
                transform.position +
                transform.forward * 1f +
                Vector3.up * 0.3f
            );
        }


        // =====================================================
        // ELIMINAR PICKUP DEL MUNDO
        // =====================================================

        if (
            NetworkManager.SpawnManager.SpawnedObjects
                .TryGetValue(
                    pickupNetworkObjectId,
                    out NetworkObject pickupNetObj
                )
        )
        {
            WeaponPickup pickup =
                pickupNetObj.GetComponent<WeaponPickup>();

            pickup?.Consume();
        }
    }


    // =========================================================
    // SPAWN DEL ARMA DROPEADA
    // =========================================================

    private void SpawnDroppedWeapon(
        WeaponType type,
        Vector3 position
    )
    {
        // Cada arma tiene su propio prefab de pickup ya configurado
        // (mesh correcto + weaponType correcto), no uno genérico.
        GameObject prefabToSpawn = GetPickupPrefabByType(type);

        if (prefabToSpawn == null)
            return;


        GameObject dropped =
            Instantiate(
                prefabToSpawn,
                position,
                Quaternion.identity
            );


        NetworkObject netObj =
            dropped.GetComponent<NetworkObject>();


        if (netObj != null)
        {
            netObj.Spawn();
        }
    }

    
}