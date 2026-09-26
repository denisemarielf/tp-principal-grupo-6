using System;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(WeaponSwitcher))]
public class WeaponPowerUp : NetworkBehaviour
{
    private WeaponSwitcher weaponSwitcher;

    private void Awake()
    {
        weaponSwitcher = GetComponent<WeaponSwitcher>();
    }

    public void GrantAmmo(int amount)
    {
        if (!IsServer) return; // esto lo llama el PowerUp, que corre en el servidor
        GrantAmmoClientRpc(amount);
    }

    [ClientRpc]
    private void GrantAmmoClientRpc(int amount)
    {
        if (!IsOwner) return; // solo le importa al dueño de esta arma

        for (int i = 0; i < weaponSwitcher.WeaponCount; i++)
        {
            ShootLogic shootComponent = weaponSwitcher.GetShootAt(i);
            shootComponent?.AddReserveAmmo(amount);
        }
    }

    /*
    public void ApplyDamageBoost(float multiplier, float duration)
    {
        if (!IsServer) return;
        StartCoroutine(DamageBoostRoutine(multiplier, duration));
    }


    private IEnumerator DamageBoostRoutine(float multiplier, float duration)
    {
        Shoot boostedShoot = weaponSwitcher.GetShootAt(weaponSwitcher.CurrentWeaponIndex);
        if (boostedShoot == null) yield break;

        int originalDamage = boostedShoot.damageAmount;
        boostedShoot.damageAmount = Mathf.RoundToInt(originalDamage * multiplier);

        yield return new WaitForSeconds(duration);

        boostedShoot.damageAmount = originalDamage;
    }*/
}