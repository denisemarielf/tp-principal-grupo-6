using UnityEngine;
using Unity.Netcode;

public class WeaponLogic : NetworkBehaviour
{
    [Header("Weapon")]
    public Transform spawnPoint;
    public GameObject bullet;
    public float shootForce = 1500f;
    public int damageAmount = 20;

    [Header("Effects")]
    public AudioSource audioSource;
    public AudioClip shootSound;
    public ParticleSystem muzzleFlash;
    public WeaponSway weaponSway;

    /// <summary>Reproduce sonido/fogonazo/recoil localmente.</summary>
    public void PlayShootEffectsLocal()
    {
        if (audioSource != null && shootSound != null)
            audioSource.PlayOneShot(shootSound);

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
        }

        if (weaponSway != null)
            weaponSway.AddRecoil();
    }

    /// <summary>Pide al servidor que dispare de verdad (spawnea la bala). Llamado por ShootLogic cuando ya validó cooldown/munición.</summary>
    public void RequestFire()
    {
        ShootServerRpc(spawnPoint.position, spawnPoint.rotation);
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 position, Quaternion rotation)
    {
        GameObject newBullet = Instantiate(bullet, position, rotation);

        Bullet bulletScript = newBullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.setDamageAmount(damageAmount);

            // NetworkObject.transform es el Player dueño de esta arma (mismo NetworkObject
            // raiz que usa WeaponSwitcher). Esto corre YA en el server, asi que no hace
            // falta serializar nada por RPC: accedemos directo a la referencia local.
            bulletScript.SetShooter(NetworkObject.transform);
        }

        NetworkObject netObj = newBullet.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        Rigidbody rb = newBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(rotation * Vector3.forward * shootForce);
        }
        Destroy(newBullet, 3);
        // Avisamos a todos los clientes para que reproduzcan sonido/fogonazo/recoil
        PlayShootEffectsClientRpc();
    }

    [ClientRpc]
    private void PlayShootEffectsClientRpc()
    {
        if (IsOwner) return; // el dueño ya los reprodujo localmente, evitamos duplicar

        PlayShootEffectsLocal();
    }
}