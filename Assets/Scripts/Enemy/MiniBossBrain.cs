using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// IA del mini jefe (solo corre en el server). Camina hacia el blanco mas cercano, de a ratos le da un
// impulso y corre, pega con golpes que mandan a volar (estilo Tank) y salta para aplastar.
// La vida, la muerte y el dano que recibe los maneja EnemyHealth (asi las armas siguen funcionando igual).
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MiniBossAnimation))]
[RequireComponent(typeof(EnemyHealth))]
public class MiniBossBrain : NetworkBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float runSpeed = 9f;          // el jugador camina a 7: corriendo lo alcanza
    [SerializeField] private float targetSearchInterval = 0.3f;
    [SerializeField] private float repathInterval = 0.2f;

    [Header("Impulso (corrida)")]
    [SerializeField] private Vector2 runCooldown = new Vector2(6f, 12f);   // camina entre corridas
    [SerializeField] private Vector2 runDuration = new Vector2(3f, 5f);
    [SerializeField] private float minDistanceToRun = 8f;

    [Header("Golpe")]
    [SerializeField] private float meleeRange = 3.5f;       // a esta distancia empieza el golpe
    [SerializeField] private float meleeHitRange = 5f;      // alcance real al momento del impacto
    [SerializeField] private float meleeHalfAngle = 70f;
    [SerializeField] private float meleeDamage = 45f;
    [Range(0.05f, 0.95f)]
    [SerializeField] private float attackHitNormalized = 0.45f; // momento del impacto dentro del clip
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float recoverTime = 0.4f;      // queda quieto un instante despues de cada accion

    [Header("Salto")]
    [SerializeField] private float jumpMinRange = 7f;
    [SerializeField] private float jumpMaxRange = 18f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpChance = 0.6f;       // se tira una vez por segundo cuando esta en rango
    [SerializeField] private Vector2 jumpCooldown = new Vector2(7f, 12f);
    [Range(0.05f, 0.9f)]
    [SerializeField] private float jumpTakeoffNormalized = 0.3f; // empieza a avanzar
    [Range(0.1f, 0.95f)]
    [SerializeField] private float jumpLandNormalized = 0.65f;   // toca el piso y aplasta
    [SerializeField] private float jumpDamage = 60f;
    [SerializeField] private float slamRadius = 5f;
    [SerializeField] private float jumpLandOffset = 1.5f;   // aterriza un poco antes del blanco

    [Header("Lanzamiento (estilo Tank)")]
    [SerializeField] private float launchHorizontal = 10f;
    [SerializeField] private float launchVertical = 7f;

    private NavMeshAgent agent;
    private MiniBossAnimation anim;
    private EnemyHealth health;

    private Transform target;
    private float searchTimer;
    private float repathTimer;
    private bool busy;

    private bool running;
    private float runEndTime;
    private float nextRunTime;
    private float nextAttackTime;
    private float nextJumpTime;
    private float nextJumpRollTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<MiniBossAnimation>();
        health = GetComponent<EnemyHealth>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            if (agent != null)
                agent.enabled = false;
            return;
        }

        agent.speed = walkSpeed;
        nextRunTime = Time.time + Random.Range(runCooldown.x, runCooldown.y);
        nextJumpTime = Time.time + 3f;

        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);

        RefreshTarget();
    }

    private bool Dead => health != null && health.IsDead();

    private bool GameFinished =>
        (SafeZone.Current != null && SafeZone.Current.IsCompleted)
        || (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished);

    // ---------- Decision ----------

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;
        if (Dead) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (GameFinished)
        {
            agent.isStopped = true;
            anim.SetLocomotion(MiniBossAnimation.Idle);
            return;
        }

        if (busy) return;

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f || !IsValidTarget(target))
        {
            RefreshTarget();
            searchTimer = targetSearchInterval;
        }

        if (target == null)
        {
            agent.isStopped = true;
            anim.SetLocomotion(MiniBossAnimation.Idle);
            return;
        }

        Vector3 flat = target.position - transform.position;
        flat.y = 0f;
        float distance = flat.magnitude;

        if (Time.time >= nextAttackTime && distance <= meleeRange)
        {
            StartCoroutine(MeleeRoutine(target));
            return;
        }

        if (anim.HasJump && Time.time >= nextJumpTime && Time.time >= nextJumpRollTime
            && distance >= jumpMinRange && distance <= jumpMaxRange)
        {
            nextJumpRollTime = Time.time + 1f;
            if (Random.value < jumpChance)
            {
                StartCoroutine(JumpRoutine(target));
                return;
            }
        }

        Chase(distance);
    }

    private void Chase(float distance)
    {
        UpdateRunState(distance);

        agent.isStopped = false;
        agent.speed = running ? runSpeed : walkSpeed;

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            agent.SetDestination(target.position);
            repathTimer = repathInterval;
        }

        anim.SetLocomotion(running ? MiniBossAnimation.Run : MiniBossAnimation.Walk);
    }

    // Camina un rato, de golpe tiene un impulso y corre unos segundos, y vuelve a caminar.
    private void UpdateRunState(float distance)
    {
        if (running)
        {
            if (Time.time >= runEndTime)
            {
                running = false;
                nextRunTime = Time.time + Random.Range(runCooldown.x, runCooldown.y);
            }

            return;
        }

        if (Time.time >= nextRunTime && distance >= minDistanceToRun)
        {
            running = true;
            runEndTime = Time.time + Random.Range(runDuration.x, runDuration.y);
        }
    }

    // ---------- Acciones ----------

    private IEnumerator MeleeRoutine(Transform victim)
    {
        busy = true;
        Halt();

        float length = anim.BeginAttack(out _) + 0.05f;
        float hitTime = length * attackHitNormalized;
        bool hitDone = false;
        float t = 0f;

        while (t < length)
        {
            if (Dead) yield break;

            if (!hitDone && t < hitTime * 0.8f)
                FaceTarget(victim);

            if (!hitDone && t >= hitTime)
            {
                hitDone = true;
                if (DamageArea(transform.position, transform.forward, meleeHitRange, meleeHalfAngle, meleeDamage))
                    HitLandedClientRpc();
            }

            t += Time.deltaTime;
            yield return null;
        }

        nextAttackTime = Time.time + attackCooldown;
        yield return Recover();
    }

    private IEnumerator JumpRoutine(Transform victim)
    {
        busy = true;
        Halt();

        float length = anim.BeginJump(out _) + 0.05f;
        float takeoff = length * jumpTakeoffNormalized;
        float land = Mathf.Max(length * jumpLandNormalized, takeoff + 0.05f);

        bool launched = false;
        bool landed = false;
        Vector3 from = transform.position;
        Vector3 to = from;
        float t = 0f;

        while (t < length)
        {
            if (Dead) yield break;

            if (!launched)
            {
                if (t < takeoff)
                {
                    FaceTarget(victim);
                }
                else
                {
                    launched = true;
                    from = transform.position;
                    to = PickLanding(victim, from);
                }
            }

            if (launched && !landed)
            {
                float u = Mathf.Clamp01((t - takeoff) / (land - takeoff));
                Vector3 wanted = Vector3.Lerp(from, to, u);
                Vector3 delta = wanted - transform.position;
                delta.y = 0f;
                if (agent.enabled && agent.isOnNavMesh)
                    agent.Move(delta);

                if (u >= 1f)
                {
                    landed = true;
                    DamageArea(transform.position, transform.forward, slamRadius, 180f, jumpDamage);
                    SlamClientRpc();
                }
            }

            t += Time.deltaTime;
            yield return null;
        }

        nextJumpTime = Time.time + Random.Range(jumpCooldown.x, jumpCooldown.y);
        nextAttackTime = Time.time + attackCooldown * 0.5f;
        yield return Recover();
    }

    private IEnumerator Recover()
    {
        anim.SetLocomotion(MiniBossAnimation.Idle);
        yield return new WaitForSeconds(recoverTime);

        if (Dead) yield break;

        running = false;
        nextRunTime = Time.time + Random.Range(runCooldown.x, runCooldown.y);
        repathTimer = 0f;
        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
        busy = false;
    }

    private void Halt()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
    }

    private Vector3 PickLanding(Transform victim, Vector3 from)
    {
        Vector3 aim = victim != null ? victim.position : from + transform.forward * jumpMaxRange;
        Vector3 flat = aim - from;
        flat.y = 0f;
        float distance = Mathf.Clamp(flat.magnitude - jumpLandOffset, 0f, jumpMaxRange);
        Vector3 point = from + flat.normalized * distance;

        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            return hit.position;

        return from;
    }

    private void FaceTarget(Transform victim)
    {
        if (victim == null) return;
        Vector3 look = victim.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(look);
    }

    // ---------- Dano y lanzamiento ----------

    // Dana (y manda a volar a los jugadores) todo lo que este dentro del alcance y del cono. Devuelve true si pego a alguien.
    private bool DamageArea(Vector3 origin, Vector3 forward, float radius, float halfAngle, float damage)
    {
        bool anyHit = false;
        forward.y = 0f;

        foreach (PMovement player in FindObjectsByType<PMovement>())
        {
            if (player == null) continue;
            if (!player.TryGetComponent(out PlayerHealth playerHealth) || playerHealth.IsDead) continue;
            if (!InArea(player.transform.position, origin, forward, radius, halfAngle, out Vector3 away)) continue;

            playerHealth.TakeDamage(damage);
            anyHit = true;

            if (!playerHealth.IsDead && player.NetworkObject != null)
            {
                Vector3 velocity = away * launchHorizontal + Vector3.up * launchVertical;
                LaunchClientRpc(player.NetworkObject, velocity);
            }
        }

        foreach (RescuableNpc npc in new List<RescuableNpc>(RescuableNpc.All))
        {
            if (npc == null || !npc.IsTargetable) continue;
            if (!InArea(npc.transform.position, origin, forward, radius, halfAngle, out _)) continue;

            npc.TakeDamage(damage);
            anyHit = true;
        }

        return anyHit;
    }

    private static bool InArea(Vector3 position, Vector3 origin, Vector3 forward, float radius, float halfAngle, out Vector3 away)
    {
        away = position - origin;
        away.y = 0f;
        float distance = away.magnitude;
        if (distance > radius)
            return false;

        if (distance < 0.01f)
            away = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        else
            away /= distance;

        if (halfAngle < 180f && distance > 0.01f && forward.sqrMagnitude > 0.001f
            && Vector3.Angle(forward, away) > halfAngle)
            return false;

        return true;
    }

    // Solo el dueño del jugador aplica el empuje (el movimiento del jugador es de su cliente).
    [ClientRpc]
    private void LaunchClientRpc(NetworkObjectReference playerReference, Vector3 velocity)
    {
        if (!playerReference.TryGet(out NetworkObject playerObject) || !playerObject.IsOwner)
            return;

        PMovement movement = playerObject.GetComponentInChildren<PMovement>();
        if (movement != null)
            movement.Launch(velocity);
    }

    [ClientRpc]
    private void HitLandedClientRpc()
    {
        if (anim != null) anim.PlayHitSound();
    }

    [ClientRpc]
    private void SlamClientRpc()
    {
        if (anim != null) anim.PlaySlamSound();
    }

    // ---------- Blancos ----------

    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null) return false;
        if (candidate.TryGetComponent(out PlayerHealth playerHealth) && playerHealth.IsDead) return false;
        if (candidate.TryGetComponent(out RescuableNpc npc) && !npc.IsTargetable) return false;
        return true;
    }

    private void RefreshTarget()
    {
        Transform nearest = null;
        float nearestDistance = Mathf.Infinity;

        foreach (PMovement candidate in FindObjectsByType<PMovement>())
        {
            if (candidate == null) continue;
            if (candidate.TryGetComponent(out PlayerHealth playerHealth) && playerHealth.IsDead) continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate.transform;
            }
        }

        foreach (RescuableNpc npc in RescuableNpc.All)
        {
            if (npc == null || !npc.IsTargetable) continue;

            float distance = Vector3.Distance(transform.position, npc.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = npc.transform;
            }
        }

        target = nearest;
    }
}
