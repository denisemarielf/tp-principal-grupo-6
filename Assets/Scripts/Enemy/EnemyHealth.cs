using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : NetworkBehaviour
{
    [Header("Vida")]
    [SerializeField] private EnemyVariant variant = EnemyVariant.Normal;
    [SerializeField] private float maxHealth = 100f;

    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Ai ai;

    public float MaxHealth => maxHealth;
    public EnemyVariant Variant => variant;

    public override void OnNetworkSpawn()
    {
        ai = GetComponent<Ai>();
        if (!IsServer) return;

        maxHealth = HealthFor(variant);
        currentHealth.Value = maxHealth;
    }

    public void Configure(EnemyVariant newVariant)
    {
        variant = newVariant;
        maxHealth = HealthFor(variant);
    }

    public static float HealthFor(EnemyVariant value)
    {
        switch (value)
        {
            case EnemyVariant.Fragil:
                return 50f;
            case EnemyVariant.Resistente:
                return 200f;
            default:
                return 100f;
        }
    }

    public void TakeDamage(float amount, Transform attacker = null)
    {
        if (!IsServer) return;
        if (isDead.Value) return;

        currentHealth.Value -= amount;

        if (attacker != null && ai != null)
            ai.SetAggroTarget(attacker);

        if (currentHealth.Value <= 0f)
            Die();
    }

    [ContextMenu("Debug: recibir 25 de dano")]
    private void DebugTakeDamage()
    {
        TakeDamage(25f);
    }

    private void Die()
    {
        if (!IsServer || isDead.Value) return;

        isDead.Value = true;

        if (ai != null)
            ai.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    public bool IsDead()
    {
        return isDead.Value;
    }
}
