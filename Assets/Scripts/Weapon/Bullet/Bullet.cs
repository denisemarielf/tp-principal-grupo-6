using Unity.Netcode;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    
    private Rigidbody rb;
    private bool hasHit = false;
    public ParticleSystem sparksImpact;
    public int damageAmount;
    private Transform shooter;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetShooter(Transform shooterTransform)
    {
        shooter = shooterTransform;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        if (shooter != null && collision.transform.root == shooter.root)
            return;

        hasHit = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        ContactPoint contact = collision.GetContact(0);

        bool hitEnemy = collision.collider.GetComponentInParent<EnemyHealth>() != null;
        PlayImpactEffectClientRpc(contact.point, contact.normal, hitEnemy);

        if (IsServer)
        {
            EnemyHealth enemyHealth = collision.collider.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(damageAmount, shooter);

            DespawnBullet();
        }
    }

    [ClientRpc]
    private void PlayImpactEffectClientRpc(Vector3 point, Vector3 normal, bool hitEnemy)
    {
        if (hitEnemy)
        {
            BloodSplatter.Play(point, normal);
            return;
        }

        if (sparksImpact != null)
        {
            ParticleSystem sparks = Instantiate(
                sparksImpact,
                point,
                Quaternion.LookRotation(normal)
            );
            sparks.Play();
            Destroy(sparks.gameObject, sparks.main.duration + sparks.main.startLifetime.constantMax);
        }
    }

    private void DespawnBullet()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void setDamageAmount(int number)
    {
        damageAmount = number;
    }

}