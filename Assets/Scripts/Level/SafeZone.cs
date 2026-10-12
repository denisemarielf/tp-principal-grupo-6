using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Zona segura del nivel (US Zona Segura). Va colocada en la escena, con un NetworkObject y un Collider marcado como Trigger.
// - El trigger detecta que jugadores entran y salen (solo lo procesa el server).
// - Cada cierto tiempo el server cuenta los jugadores vivos y cuantos estan adentro, y lo sincroniza para la UI.
// - Los civiles escoltados (RescuableNpc) se detectan por posicion, porque no tienen Rigidbody y el trigger no los avisaria.
//   Al entrar quedan a salvo y suman puntos al equipo.
// - Si todos los jugadores vivos estan adentro durante 'completeDelay' segundos seguidos y se rescato al menos
//   'minRescued' civiles, se completa el nivel.
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
    [Tooltip("Civiles que hay que rescatar como minimo para poder completar el nivel (si en la escena hay menos, alcanza con todos).")]
    [SerializeField] private int minRescued = 1;
    [Tooltip("Puntos del equipo por cada civil rescatado.")]
    [SerializeField] private int pointsPerNpc = 100;

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

    private NetworkVariable<int> rescuedNpcs = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Cantidad de civiles que hubo en el nivel (los que murieron siguen contando).
    private NetworkVariable<int> totalNpcs = new NetworkVariable<int>(
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
    public int RescuedNpcs => rescuedNpcs.Value;
    public int TotalNpcs => totalNpcs.Value;
    public int TeamScore => rescuedNpcs.Value * pointsPerNpc;
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

        int rescued = CheckNpcs();
        int required = Mathf.Min(minRescued, totalNpcs.Value);

        if (RequirementsMet(alive, aliveInside, rescued, required))
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

    // Marca a salvo a los civiles escoltados que entraron a la zona y devuelve cuantos hay a salvo en total.
    private int CheckNpcs()
    {
        Collider zone = GetComponent<Collider>();
        int rescued = 0;

        foreach (RescuableNpc npc in RescuableNpc.All)
        {
            if (npc == null || npc.IsDead) continue;

            if (npc.State == NpcState.Following && IsInsideZone(zone, npc.transform.position + Vector3.up * 0.9f))
                npc.MarkSafe();

            if (npc.State == NpcState.Safe)
                rescued++;
        }

        if (RescuableNpc.All.Count > totalNpcs.Value) totalNpcs.Value = RescuableNpc.All.Count;
        if (rescuedNpcs.Value != rescued) rescuedNpcs.Value = rescued;

        return rescued;
    }

    private static bool IsInsideZone(Collider zone, Vector3 point)
    {
        return (zone.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }

    // Regla para completar el nivel: todos los jugadores vivos adentro y los civiles minimos rescatados.
    public static bool RequirementsMet(int alive, int aliveInside, int rescued, int required)
    {
        return CountsAsAllInside(alive, aliveInside) && rescued >= required;
    }

    // Todos los jugadores vivos adentro. Si no queda nadie vivo, el nivel no se completa.
    public static bool CountsAsAllInside(int alive, int aliveInside)
    {
        return alive > 0 && aliveInside >= alive;
    }

    private void Complete()
    {
        if (!IsServer || completed.Value) return;

        Debug.Log($"SafeZone: todos los jugadores vivos llegaron a la zona segura con {rescuedNpcs.Value} civiles rescatados. Nivel completado.");
        completed.Value = true;
    }

    private void OnCompletedChanged(bool previous, bool current)
    {
        if (current)
            LevelCompleted?.Invoke();
    }
}
