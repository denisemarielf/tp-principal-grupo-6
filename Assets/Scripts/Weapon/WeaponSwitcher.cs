using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;

public class WeaponSwitcher : NetworkBehaviour
{
    public GameObject[] weapons;
    public AudioSource audioSource;
    public AudioClip switchSound;
    public AmmoUI ammoUI;

    private Shoot currentWeaponShoot;

    private NetworkVariable<int> networkWeaponIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public int CurrentWeaponIndex => networkWeaponIndex.Value;
    public int WeaponCount => weapons.Length;

    public override void OnNetworkSpawn()
    {
        // Aseguramos que TODOS los GameObjects de arma esten activos siempre,
        // sin importar en que estado haya quedado guardado el prefab.
        foreach (var weapon in weapons)
        {
            if (weapon != null) weapon.SetActive(true);
        }

        // Asignamos la layer según sea mi arma o la de otro jugador.
        int targetLayer = IsOwner
            ? LayerMask.NameToLayer("Weapon")
            : LayerMask.NameToLayer("Default");

        foreach (var weapon in weapons)
        {
            if (weapon != null) SetLayerRecursively(weapon, targetLayer);
        }

        networkWeaponIndex.OnValueChanged += OnWeaponIndexChanged;
        if (IsOwner)
        {
            StartCoroutine(BuscarAmmoUI());
            SelectWeapon(0);

            // El jugador persiste entre reinicios de partida, pero la escena
            // (y con ella el AmmoUI viejo) se destruye y se recrea. Cada vez
            // que carga una escena nueva, volvemos a buscar el AmmoUI actual.
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
        // La escena vieja (y su AmmoUI) ya no existen; la referencia quedó
        // apuntando a un objeto destruido. Buscamos el AmmoUI de la escena nueva.
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
        yield return null; // espera un frame
        yield return null; // y otro mas, para dar margen a la sincronizacion inicial
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
            }
        }
    }

    public void SelectWeapon(int index)
    {
        UpdateWeaponVisuals(index);

        currentWeaponShoot = GetShootAt(index);

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

    public Shoot GetShootAt(int index)
    {
        return (index >= 0 && index < weapons.Length && weapons[index] != null)
            ? weapons[index].GetComponent<Shoot>()
            : null;
    }

    private void PlaySwitchSound()
    {
        if (audioSource != null && switchSound != null)
        {
            audioSource.PlayOneShot(switchSound);
        }
    }
}