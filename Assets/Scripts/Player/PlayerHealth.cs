using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Netcode.Components;

// Tipo de dano, para que el feedback de pantalla pueda distinguir el origen (rojo = generico, verde = acido).
public enum DamageKind
{
    Generic = 0,
    Acid = 1
}

// Salud del jugador (US 3.3). El host es el unico que modifica la vida; los clientes la leen
// de las NetworkVariables y la barra de vida (PlayerHealthBar) escucha HealthChanged.
public class PlayerHealth : NetworkBehaviour
{
    public const float RespawnDelaySeconds = 60f;
    private const float AllyRespawnOffset = 1.5f; // distancia lateral al reaparecer junto a un aliado

    // Se disparan en todos los equipos, cuando cambia el valor sincronizado.
    public event Action<float, float> HealthChanged; // (vida actual, vida maxima)
    public event Action Died;
    public event Action Revived;
    public event Action<float> DamageTaken; // solo se dispara en el dueño, con el daño recibido
    public event Action<float, DamageKind> DamageTakenTyped; // igual que DamageTaken, pero con el tipo de daño

    // Tipo del ultimo dano recibido. Solo es valido en el dueño; ya esta seteado cuando se dispara DamageTaken,
    // asi que los suscriptores actuales pueden leerlo dentro de su handler sin cambiar la firma.
    public DamageKind LastDamageKind { get; private set; } = DamageKind.Generic;

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

    [Header("Animacion")]
    [SerializeField] private float hitReactionCooldown = 0.8f;

    private static readonly int HitHash = Animator.StringToHash("Hit");
    private float nextHitReactionTime;

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

    // Lo que se apaga al morir; solo eso se restaura al revivir.
    private readonly System.Collections.Generic.List<Renderer> hiddenRenderers =
        new System.Collections.Generic.List<Renderer>();
    private readonly System.Collections.Generic.List<Canvas> hiddenCanvases =
        new System.Collections.Generic.List<Canvas>();
    private readonly System.Collections.Generic.List<Collider> disabledColliders =
        new System.Collections.Generic.List<Collider>();
    private readonly System.Collections.Generic.List<CharacterController> hiddenControllers =
        new System.Collections.Generic.List<CharacterController>();

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

        // Para quien entra tarde y el jugador ya esta muerto.
        if (isDead.Value)
            SetBodyVisible(false);

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
    public void TakeDamage(float amount, DamageKind kind = DamageKind.Generic)
    {
        if (!IsServer || isDead.Value || amount <= 0f) return;
        // Tras extraer / terminar supervivencia no reciben mas dano.
        if (SafeZone.Current != null && SafeZone.Current.IsCompleted) return;
        if (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished) return;

        currentHealth.Value = ApplyDamage(currentHealth.Value, amount);

        if (currentHealth.Value > 0f)
            PlayHitReaction();

        // Avisa solo al dueño para el feedback (pantalla roja + golpe de camara).
        NotifyDamageClientRpc(amount, kind, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });

        if (currentHealth.Value <= 0f)
            Die();
    }

    // Solo server. Dispara el trigger en todos los NetworkAnimator activos del jugador
    // (hay un CharacterModel por skin y no siempre es el primero el que se usa).
    private void PlayHitReaction()
    {
        if (Time.time < nextHitReactionTime) return;
        nextHitReactionTime = Time.time + hitReactionCooldown;

        bool needsOwner = false;
        foreach (NetworkAnimator animator in GetComponentsInChildren<NetworkAnimator>(false))
        {
            if (!animator.IsSpawned) continue;

            if (animator.IsServerAuthoritative())
                animator.SetTrigger(HitHash);
            else
                needsOwner = true;
        }

        // Authority Mode Owner: el trigger lo tiene que setear el dueño.
        if (needsOwner)
        {
            HitReactionClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }
    }

    [ClientRpc]
    private void HitReactionClientRpc(ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        foreach (NetworkAnimator animator in GetComponentsInChildren<NetworkAnimator>(false))
        {
            if (animator.IsSpawned && !animator.IsServerAuthoritative())
                animator.SetTrigger(HitHash);
        }
    }

    [ClientRpc]
    private void NotifyDamageClientRpc(float amount, DamageKind kind, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        LastDamageKind = kind;   // antes de los eventos, para que los handlers lo puedan leer
        DamageTaken?.Invoke(amount);
        DamageTakenTyped?.Invoke(amount, kind);
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

        // Reaparece junto a un jugador vivo al azar. Si no hay ninguno, usa el PlayerSpawnPoint.
        Character character = GetComponent<Character>();
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        int playerIndex = 0;

        if (TryGetAliveAllyTransform(out Transform ally))
        {
            spawnRotation = ally.rotation;
            // Pequeño desplazamiento lateral para no aparecer adentro del aliado.
            spawnPosition = ally.position + ally.right * AllyRespawnOffset;
        }
        else
        {
            PlayerSpawnPoint spawnPoint = FindAnyObjectByType<PlayerSpawnPoint>();
            spawnPosition = spawnPoint != null ? spawnPoint.transform.position : transform.position;
            spawnRotation = spawnPoint != null ? spawnPoint.transform.rotation : transform.rotation;

            if (NetworkManager != null)
            {
                foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                {
                    if (clientId == OwnerClientId)
                        break;
                    playerIndex++;
                }
            }
        }

        if (character != null)
            character.PlaceAtSpawn(spawnPosition, spawnRotation, playerIndex);
        else
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
    }

    // Solo server. Elige al azar un jugador vivo (distinto de este) para reaparecer a su lado.
    private bool TryGetAliveAllyTransform(out Transform ally)
    {
        ally = null;
        if (NetworkManager == null || NetworkManager.SpawnManager == null)
            return false;

        var candidates = new System.Collections.Generic.List<Transform>();
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId == OwnerClientId) continue;

            NetworkObject playerObject = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject == null) continue;

            PlayerHealth otherHealth = playerObject.GetComponent<PlayerHealth>();
            if (otherHealth == null || otherHealth.IsDead) continue;

            candidates.Add(playerObject.transform);
        }

        if (candidates.Count == 0)
            return false;

        ally = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
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

    // Muerto: sin modelo, sin barra de vida en el mundo y sin colisiones. Vivo: todo vuelve como estaba.
    private void SetBodyVisible(bool visible)
    {
        if (!visible)
        {
            if (hiddenRenderers.Count > 0 || hiddenCanvases.Count > 0
                || disabledColliders.Count > 0 || hiddenControllers.Count > 0)
                return; // ya esta oculto

            foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled)
                    continue;

                r.enabled = false;
                hiddenRenderers.Add(r);
            }

            // Barras de vida y demas UI flotante sobre el personaje.
            foreach (Canvas c in GetComponentsInChildren<Canvas>(false))
            {
                if (c == null || !c.enabled || c.renderMode != RenderMode.WorldSpace)
                    continue;

                c.enabled = false;
                hiddenCanvases.Add(c);
            }

            foreach (Collider col in GetComponentsInChildren<Collider>(false))
            {
                if (col == null || !col.enabled)
                    continue;

                // El CharacterController no se desactiva (PMovement lo sigue moviendo por la gravedad):
                // solo se le saca la deteccion de colisiones para que nadie choque con el.
                CharacterController controller = col as CharacterController;
                if (controller != null)
                {
                    if (controller.detectCollisions)
                    {
                        controller.detectCollisions = false;
                        hiddenControllers.Add(controller);
                    }
                    continue;
                }

                col.enabled = false;
                disabledColliders.Add(col);
            }
            return;
        }

        foreach (Renderer r in hiddenRenderers)
        {
            if (r != null)
                r.enabled = true;
        }
        hiddenRenderers.Clear();

        foreach (Canvas c in hiddenCanvases)
        {
            if (c != null)
                c.enabled = true;
        }
        hiddenCanvases.Clear();

        foreach (Collider col in disabledColliders)
        {
            if (col != null)
                col.enabled = true;
        }
        disabledColliders.Clear();

        foreach (CharacterController controller in hiddenControllers)
        {
            if (controller != null)
                controller.detectCollisions = true;
        }
        hiddenControllers.Clear();
    }

    private void OnDeadValueChanged(bool previous, bool current)
    {
        if (current)
        {
            SetBodyVisible(false);
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
        SetBodyVisible(true);

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
