using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Salud del jugador (US 3.3). El host es el unico que modifica la vida; los clientes la leen
// de las NetworkVariables y la barra de vida (PlayerHealthBar) escucha HealthChanged.
public class PlayerHealth : NetworkBehaviour
{
    // Se disparan en todos los equipos, cuando cambia el valor sincronizado.
    public event Action<float, float> HealthChanged; // (vida actual, vida maxima)
    public event Action Died;

    [Header("Vida")]
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

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth.Value;
    public bool IsDead => isDead.Value;

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthValueChanged;
        isDead.OnValueChanged += OnDeadValueChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }

        HealthChanged?.Invoke(currentHealth.Value, maxHealth);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthValueChanged;
        isDead.OnValueChanged -= OnDeadValueChanged;
    }

    // Solo el server.
    public void TakeDamage(float amount)
    {
        if (!IsServer || isDead.Value || amount <= 0f) return;

        currentHealth.Value = ApplyDamage(currentHealth.Value, amount);

        if (currentHealth.Value <= 0f)
            Die();
    }

    // Solo el server. Un jugador muerto no se puede curar.
    public void Heal(float amount)
    {
        if (!IsServer || isDead.Value || amount <= 0f) return;

        currentHealth.Value = ApplyHeal(currentHealth.Value, amount, maxHealth);
    }

    public static float ApplyDamage(float current, float amount)
    {
        return Mathf.Max(0f, current - Mathf.Max(0f, amount));
    }

    public static float ApplyHeal(float current, float amount, float max)
    {
        return Mathf.Min(max, current + Mathf.Max(0f, amount));
    }

    private void Die()
    {
        if (!IsServer || isDead.Value) return;

        isDead.Value = true;
    }

    private void OnHealthValueChanged(float previous, float current)
    {
        HealthChanged?.Invoke(current, maxHealth);
    }

    private void OnDeadValueChanged(bool previous, bool current)
    {
        if (!current) return;

        // El dueño deja de controlar al personaje; la gravedad de PMovement sigue aplicandose.
        if (IsOwner)
        {
            PMovement movement = GetComponent<PMovement>();
            if (movement != null)
                movement.ResetMovement();

            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = false;
        }

        Debug.Log($"PlayerHealth: el jugador {OwnerClientId} murio.");
        Died?.Invoke();
    }

    [ContextMenu("Debug: recibir 25 de dano")]
    private void DebugTakeDamage()
    {
        TakeDamage(25f);
    }

    [ContextMenu("Debug: curar 25")]
    private void DebugHeal()
    {
        Heal(25f);
    }
}
