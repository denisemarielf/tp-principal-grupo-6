using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class AmmoCase : NetworkBehaviour
{
    [Header("Config")]
    public float amount = 25f;
    public float respawnTime = 15f;
    public AudioClip pickupSound;

    [Header("Pickup")]
    [SerializeField] private Collider pickupCollider;

    private NetworkVariable<bool> isAvailable = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Renderer[] renderers;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();

        if (pickupCollider == null)
        {
            Debug.LogError("AmmoCase: No se asignó el Pickup Collider.");
        }
        else
        {
            pickupCollider.isTrigger = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        isAvailable.OnValueChanged += OnAvailabilityChanged;

        UpdateVisuals(isAvailable.Value);
    }

    public override void OnDestroy()
    {
        isAvailable.OnValueChanged -= OnAvailabilityChanged;
        base.OnDestroy();
    }

    private void OnAvailabilityChanged(bool oldValue, bool newValue)
    {
        UpdateVisuals(newValue);
    }

    private void UpdateVisuals(bool available)
    {
        foreach (Renderer r in renderers)
        {
            if (r != null)
                r.enabled = available;
        }

        if (pickupCollider != null)
            pickupCollider.enabled = available;
    }

    private void OnTriggerEnter(Collider other)
    {
       

        if (!IsServer)
            return;

        if (!isAvailable.Value)
            return;

        // ---------------------------------------------------------
        // BUSCAR AL PLAYER
        // ---------------------------------------------------------

        CharacterController characterController =
            other.GetComponentInParent<CharacterController>();

        if (characterController == null)
        {

            return;
        }

        // ---------------------------------------------------------
        // BUSCAR WEAPON SWITCHER
        // ---------------------------------------------------------

        WeaponSwitcher weaponSwitcher =
            characterController.GetComponentInParent<WeaponSwitcher>();

        if (weaponSwitcher == null)
        {
            weaponSwitcher =
                characterController.GetComponentInChildren<WeaponSwitcher>();
        }

        if (weaponSwitcher == null)
        {
            Debug.LogError(
                $"[AmmoCase] Encontré al Player, " +
                $"pero no encontré WeaponSwitcher."
            );

            return;
        }

        // ---------------------------------------------------------
        // OBTENER AMMO MANAGER DEL ARMA EQUIPADA
        // ---------------------------------------------------------

        AmmoManager ammoManager =
            weaponSwitcher.CurrentAmmoManager;

        if (ammoManager == null)
        {
            Debug.Log(
                "[AmmoCase] El jugador no tiene un arma equipada " +
                "o el arma equipada no tiene AmmoManager."
            );

            return;
        }

       

        ammoManager.AddReserveAmmo((int)amount);

     

        PlayPickupSoundClientRpc();

        
        isAvailable.Value = false;

        
        StartCoroutine(RespawnRoutine());
    }

 

    [ClientRpc]
    private void PlayPickupSoundClientRpc()
    {
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(
                pickupSound,
                transform.position
            );
        }
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);

        if (IsServer)
        {
            isAvailable.Value = true;
        }
    }
}