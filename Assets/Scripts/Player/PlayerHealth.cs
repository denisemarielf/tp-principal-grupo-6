using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

// Salud del jugador (US 3.3). El host es el unico que modifica la vida; los clientes la leen
// de las NetworkVariables y la barra de vida (PlayerHealthBar) escucha HealthChanged.
public class PlayerHealth : NetworkBehaviour
{
    public const float RespawnDelaySeconds = 60f;

    // Se disparan en todos los equipos, cuando cambia el valor sincronizado.
    public event Action<float, float> HealthChanged; // (vida actual, vida maxima)
    public event Action Died;
    public event Action Revived;
    public event Action<float> DamageTaken; // solo se dispara en el dueño, con el daño recibido

    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] hurtSounds;
    [SerializeField] private AudioClip[] deathSounds;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;
    [SerializeField] private float hurtCooldown = 0.25f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private AudioClip[] healSounds;
    [SerializeField, Range(0f, 1f)] private float healVolume = 0.8f;

    private float nextHurtTime;

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
    private NetworkVariable<float> networkMaxHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Tiempo de red del server en el que reaparece (0 = sin timer).
    private NetworkVariable<float> respawnAtServerTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float baseMaxHealth;
    private Coroutine healthBoostRoutine;
    private Coroutine respawnRoutine;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth.Value;
    public bool IsDead => isDead.Value;

    public bool IsFull => currentHealth.Value >= maxHealth;

    public float RespawnSecondsRemaining
    {
        get
        {
            if (!isDead.Value || respawnAtServerTime.Value <= 0f || NetworkManager == null)
                return 0f;

            float now = (float)NetworkManager.ServerTime.Time;
            return Mathf.Max(0f, respawnAtServerTime.Value - now);
        }
    }

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthValueChanged;
        isDead.OnValueChanged += OnDeadValueChanged;
        networkMaxHealth.OnValueChanged += OnMaxHealthChanged;

        if (IsServer)
        {
            baseMaxHealth = maxHealth;
            networkMaxHealth.Value = maxHealth;
            currentHealth.Value = maxHealth;
            isDead.Value = false;
            respawnAtServerTime.Value = 0f;
        }
        else
        {
            maxHealth = networkMaxHealth.Value; // para quien entra tarde
        }

        HealthChanged?.Invoke(currentHealth.Value, maxHealth);
    }

    // Solo server. Restaura vida tras un cambio de escena encadenado (US 4.10).
    public void ApplyCarryOver(float health)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Clamp(health, 0f, maxHealth);
        isDead.Value = currentHealth.Value <= 0f;
        if (!isDead.Value)
            respawnAtServerTime.Value = 0f;
    }

    public override void OnNetworkDespawn()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        currentHealth.OnValueChanged -= OnHealthValueChanged;
        networkMaxHealth.OnValueChanged -= OnMaxHealthChanged;
        isDead.OnValueChanged -= OnDeadValueChanged;
    }

    // Solo el server.
    public void TakeDamage(float amount)
    {
        if (!IsServer || isDead.Value || amount <= 0f) return;
        // Tras extraer / terminar supervivencia no reciben mas dano.
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted) return;
        if (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished) return;

        currentHealth.Value = ApplyDamage(currentHealth.Value, amount);

        // Avisa solo al dueño para el feedback (pantalla roja + golpe de camara).
        NotifyDamageClientRpc(amount, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });

        if (currentHealth.Value <= 0f)
            Die();
    }

    [ClientRpc]
    private void NotifyDamageClientRpc(float amount, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        DamageTaken?.Invoke(amount);
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
        MatchStatsService.NotifyDeath(OwnerClientId);
        respawnAtServerTime.Value = 0f;

        // Solo en multi (2+ jugadores). En solo aplica derrota / fin de partida.
        if (!IsMultiplayerSession())
            return;

        if (NetworkManager != null)
            respawnAtServerTime.Value = (float)NetworkManager.ServerTime.Time + RespawnDelaySeconds;
        else
            respawnAtServerTime.Value = Time.time + RespawnDelaySeconds;

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);
        respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        float waitUntil = Time.time + RespawnDelaySeconds;
        while (Time.time < waitUntil)
        {
            if (!isDead.Value || !IsMultiplayerSession() || MatchEndedForRespawn())
            {
                respawnAtServerTime.Value = 0f;
                respawnRoutine = null;
                yield break;
            }

            yield return null;
        }

        respawnRoutine = null;
        if (!isDead.Value || !IsMultiplayerSession() || MatchEndedForRespawn())
        {
            respawnAtServerTime.Value = 0f;
            yield break;
        }

        ServerRespawn();
    }

    private void ServerRespawn()
    {
        if (!IsServer || !isDead.Value) return;
        if (!IsMultiplayerSession() || MatchEndedForRespawn()) return;

        isDead.Value = false;
        currentHealth.Value = maxHealth;
        respawnAtServerTime.Value = 0f;

        PlayerSpawnPoint spawnPoint = FindAnyObjectByType<PlayerSpawnPoint>();
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.transform.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.transform.rotation : transform.rotation;

        int playerIndex = 0;
        if (NetworkManager != null)
        {
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == OwnerClientId)
                    break;
                playerIndex++;
            }
        }

        Character character = GetComponent<Character>();
        if (character != null)
            character.PlaceAtSpawn(spawnPosition, spawnRotation, playerIndex);
        else
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
    }

    private static bool IsMultiplayerSession()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager != null && manager.ConnectedClientsIds.Count > 1;
    }

    private static bool MatchEndedForRespawn()
    {
        if (GameOverUI.IsShown)
            return true;
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted)
            return true;
        if (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished)
            return true;
        return false;
    }

    private void OnHealthValueChanged(float previous, float current)
    {
        if (IsOwner && current < previous)
            ElectricPanel.CancelIfPlaying();
        HealthChanged?.Invoke(current, maxHealth);
        if (current < previous && current > 0f)
            PlayHurtSound();
        else if (current > previous && previous > 0f)
            PlayHealSound();
    }

    private void OnDeadValueChanged(bool previous, bool current)
    {
        if (current)
        {
            PlayDeathSound();

            // El dueño deja de controlar al personaje; la gravedad de PMovement sigue aplicandose.
            if (IsOwner)
            {
                ElectricPanel.CancelIfPlaying();
                PMovement movement = GetComponent<PMovement>();
                if (movement != null)
                    movement.ResetMovement();

                PlayerInput playerInput = GetComponent<PlayerInput>();
                if (playerInput != null)
                    playerInput.enabled = false;
            }

            Died?.Invoke();
            return;
        }

        // Revive
        if (IsOwner)
        {
            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = true;
        }

        Revived?.Invoke();
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

    private void PlayHurtSound()
    {
        if (!IsOwner) return;
        if (audioSource == null || hurtSounds == null || hurtSounds.Length == 0)
            return;

        if (Time.time < nextHurtTime)
            return;

        nextHurtTime = Time.time + hurtCooldown;

        AudioClip clip = hurtSounds[UnityEngine.Random.Range(0, hurtSounds.Length)];
        audioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip);
    }

    private void PlayDeathSound()
    {
        if (!IsOwner) return;
        if (deathSounds == null || deathSounds.Length == 0)
            return;

        AudioClip clip = deathSounds[UnityEngine.Random.Range(0, deathSounds.Length)];
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, deathVolume);
    }
    private void PlayHealSound()
    {
        if (!IsOwner) return;
        if (audioSource == null || healSounds == null || healSounds.Length == 0)
            return;

        AudioClip clip = healSounds[UnityEngine.Random.Range(0, healSounds.Length)];
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(clip, healVolume);
    }
    private void OnMaxHealthChanged(float previous, float current)
    {

        maxHealth = current;
        HealthChanged?.Invoke(currentHealth.Value, maxHealth);
    }

    public void ApplyHealthBoost(float amount, float duration)
    {

        if (!IsServer || isDead.Value || amount <= 0f) return;

        // Si ya había un boost activo, se reinicia (no se acumula)
        if (healthBoostRoutine != null) StopCoroutine(healthBoostRoutine);
        healthBoostRoutine = StartCoroutine(HealthBoostRoutine(amount, duration));
    }

    private IEnumerator HealthBoostRoutine(float amount, float duration)
    {
        maxHealth = baseMaxHealth + amount;
        networkMaxHealth.Value = maxHealth;
        currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);

        yield return new WaitForSeconds(duration);

        maxHealth = baseMaxHealth;
        networkMaxHealth.Value = maxHealth;
        currentHealth.Value = Mathf.Min(currentHealth.Value, maxHealth);
        healthBoostRoutine = null;
    }
}
