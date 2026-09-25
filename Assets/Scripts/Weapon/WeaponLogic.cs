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
       
        PlayShootEffectsClientRpc();
    }

    [ClientRpc]
    private void PlayShootEffectsClientRpc()
    {
        if (IsOwner) return; 

        PlayShootEffectsLocal();
    }
}