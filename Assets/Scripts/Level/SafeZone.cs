using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Zona segura del nivel (US Zona Segura). Va colocada en la escena, con un NetworkObject y un Collider marcado como Trigger.
// - El trigger detecta que jugadores entran y salen (solo lo procesa el server).
// - Cada cierto tiempo el server cuenta los jugadores vivos y cuantos estan adentro, y lo sincroniza para la UI.
// - Si todos los jugadores vivos estan adentro durante 'completeDelay' segundos seguidos, se completa el nivel.
// Los NPCs rescatados se suman en la proxima US.
[RequireComponent(typeof(Collider))]
public class SafeZone : NetworkBehaviour
{
    // Se dispara en todos los equipos cuando el nivel se completa.
    public static event Action LevelCompleted;

    // La zona de la escena actual (hay una por nivel).
    public static SafeZone Current { get; private set; }

    [Tooltip("Segundos que todos los jugadores vivos tienen que permanecer juntos adentro para completar el nivel.")]
    [SerializeField] private float completeDelay = 2f;
    [Tooltip("Cada cuantos segundos el server revisa quienes estan adentro.")]
    [SerializeField] private float checkInterval = 0.2f;

    private NetworkVariable<int> playersInside = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> alivePlayers = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> completed = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Solo el server: jugadores que estan dentro del trigger.
    private readonly HashSet<PlayerHealth> inside = new HashSet<PlayerHealth>();
    private float checkTimer;
    private float allInsideTimer;

    public int PlayersInside => playersInside.Value;
    public int AlivePlayers => alivePlayers.Value;
    public bool IsCompleted => completed.Value;
    public bool AllAlivePlayersInside => alivePlayers.Value > 0 && playersInside.Value >= alivePlayers.Value;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        Current = this;
        completed.OnValueChanged += OnCompletedChanged;

        if (!GetComponent<Collider>().isTrigger)
        {
            Debug.LogWarning("SafeZone: el Collider no esta marcado como Trigger, no se van a detectar jugadores.", this);
        }
    }

    public override void OnNetworkDespawn()
    {
        completed.OnValueChanged -= OnCompletedChanged;
        if (Current == this) Current = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || completed.Value) return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
            inside.Add(player);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
            inside.Remove(player);
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || completed.Value) return;

        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;

        float elapsed = checkInterval - checkTimer;
        checkTimer = checkInterval;
        CheckPlayers(elapsed);
    }

    private void CheckPlayers(float elapsed)
    {
        // Jugadores que se desconectaron estando adentro (no disparan OnTriggerExit).
        inside.RemoveWhere(player => player == null || !player.IsSpawned);

        int alive = 0;
        int aliveInside = 0;
        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            PlayerHealth player = client.PlayerObject.GetComponent<PlayerHealth>();
            if (player == null || player.IsDead) continue;

            alive++;
            if (inside.Contains(player))
                aliveInside++;
        }

        if (alivePlayers.Value != alive) alivePlayers.Value = alive;
        if (playersInside.Value != aliveInside) playersInside.Value = aliveInside;

        if (CountsAsAllInside(alive, aliveInside))
        {
            allInsideTimer += elapsed;
            if (allInsideTimer >= completeDelay)
                Complete();
        }
        else
        {
            allInsideTimer = 0f;
        }
    }

    // Todos los jugadores vivos adentro. Si no queda nadie vivo, el nivel no se completa.
    public static bool CountsAsAllInside(int alive, int aliveInside)
    {
        return alive > 0 && aliveInside >= alive;
    }

    private void Complete()
    {
        if (!IsServer || completed.Value) return;

        Debug.Log("SafeZone: todos los jugadores vivos llegaron a la zona segura. Nivel completado.");
        completed.Value = true;
    }

    private void OnCompletedChanged(bool previous, bool current)
    {
        if (current)
            LevelCompleted?.Invoke();
    }
}
