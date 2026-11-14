using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// IA del mini jefe (solo corre en el server). Camina hacia el blanco mas cercano, de a ratos le da un
// impulso y corre, pega con golpes que mandan a volar (estilo Tank) y salta para aplastar.
// Ataques especiales: burla (taunt), vomito acido a distancia (lento + dano por segundo),
// agarrar y estrangular (jugador inmovil, mucho dano) y agarrar y lanzar hacia atras.
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

    [Header("Lanzamiento por golpe (estilo Tank)")]
    [SerializeField] private float launchHorizontal = 5f;
    [SerializeField] private float launchVertical = 3.5f;

    [Header("Burla (Taunt)")]
    [SerializeField] private float tauntMinRange = 10f;
    [SerializeField] private float tauntMaxRange = 30f;
    [Range(0f, 1f)]
    [SerializeField] private float tauntChance = 0.25f;     // se tira una vez por segundo cuando esta en rango
    [SerializeField] private Vector2 tauntCooldown = new Vector2(25f, 50f); // casi nunca

    [Header("Vomito acido (a distancia)")]
    [SerializeField] private float vomitMinRange = 5f;
    [SerializeField] private float vomitRange = 14f;        // alcance del chorro (hitbox y particulas)
    [SerializeField] private float vomitHalfAngle = 20f;    // apertura del cono
    [Range(0f, 1f)]
    [SerializeField] private float vomitChance = 0.5f;
    [SerializeField] private Vector2 vomitCooldown = new Vector2(10f, 18f);
    [Range(0.05f, 0.9f)]
    [SerializeField] private float vomitSprayStart = 0.35f; // empieza a salir el acido (dentro del clip)
    [Range(0.1f, 0.98f)]
    [SerializeField] private float vomitSprayEnd = 0.8f;    // termina el chorro
    [SerializeField] private float vomitTurnSpeed = 45f;    // grados/seg: va siguiendo al blanco mientras vomita
    [SerializeField] private float mouthHeight = 2.6f;      // de donde sale el vomito (local al jefe)
    [SerializeField] private float mouthForward = 0.8f;
    [SerializeField] private float acidDamagePerSecond = 5f;
    [SerializeField] private float acidDuration = 5f;
    [Range(0.1f, 1f)]
    [SerializeField] private float acidSpeedMultiplier = 0.5f; // 0.5 = el jugador va a la mitad de velocidad
    [SerializeField] private float npcAcidDamage = 25f;     // los NPC reciben el dano de una sola vez
    [SerializeField] private LayerMask acidBlockMask = ~0;  // capas que frenan el acido (paredes/suelo/props). Quita la capa del jefe y de los jugadores

    [Header("Agarrar y estrangular (raro)")]
    [Range(0f, 1f)]
    [SerializeField] private float grabChance = 0.15f;      // se evalua cuando va a pegar de cerca
    [SerializeField] private Vector2 grabCooldown = new Vector2(20f, 35f);
    [SerializeField] private float grabReach = 4.5f;
    [SerializeField] private float grabHalfAngle = 60f;
    [Range(0.05f, 0.9f)]
    [SerializeField] private float grabLatchNormalized = 0.3f;   // momento en que lo agarra
    [Range(0.1f, 0.95f)]
    [SerializeField] private float grabStrangleStart = 0.4f;    // empieza el dano
    [Range(0.2f, 0.98f)]
    [SerializeField] private float grabReleaseNormalized = 0.9f; // lo suelta
    [SerializeField] private int grabTicks = 4;
    [SerializeField] private float grabTotalDamage = 90f;

    [Header("Agarrar y lanzar")]
    [Range(0f, 1f)]
    [SerializeField] private float throwChance = 0.3f;      // se evalua cuando va a pegar de cerca
    [SerializeField] private Vector2 throwCooldown = new Vector2(10f, 16f);
    [SerializeField] private float throwReach = 4.5f;
    [SerializeField] private float throwHalfAngle = 60f;
    [Range(0.05f, 0.9f)]
    [SerializeField] private float throwLatchNormalized = 0.3f;   // momento en que lo agarra y lo golpea
    [Range(0.1f, 0.95f)]
    [SerializeField] private float throwReleaseNormalized = 0.6f; // momento en que lo suelta y sale volando
    [SerializeField] private float throwDamage = 35f;
    [SerializeField] private float throwHorizontal = 8f;   // hacia ATRAS del jefe
    [SerializeField] private float throwVertical = 4f;
    [SerializeField] private float throwDropBehind = 1.8f;  // donde queda el jugador justo antes de salir volando
    [SerializeField] private float throwDropHeight = 2.2f;

    [Header("Punto de agarre (relativo al jefe)")]
    [SerializeField] private float holdDistance = 1.4f;
    [SerializeField] private float holdHeight = 1.0f;
    [Header("Linea de vision (golpes cuerpo a cuerpo)")]
    [SerializeField] private LayerMask attackBlockMask = ~0;   // capas que frenan los golpes (paredes/suelo/props). Quita la capa del jefe, de los jugadores y de otros enemigos
    [SerializeField] private float attackRayHeight = 1.5f;     // altura (desde los pies del jefe) desde donde sale el chequeo

    private NavMeshAgent agent;
    private MiniBossAnimation anim;
    private EnemyHealth health;
    private Transform holdAnchor;

    private Transform target;
    private float searchTimer;
    private float repathTimer;
    private bool busy;

    private bool running;
    private float runEndTime;
    private float nextRunTime;
    private float nextAttackTime;
    private float nextJumpTime;
    private float nextRollTime;
    private float nextTauntTime;
    private float nextVomitTime;
    private float nextGrabTime;
    private float nextThrowTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<MiniBossAnimation>();
        health = GetComponent<EnemyHealth>();

        // Ancla donde queda el jugador agarrado. Existe en todos los clientes (el dueño del jugador la sigue).
        GameObject anchor = new GameObject("HoldAnchor");
        holdAnchor = anchor.transform;
        holdAnchor.SetParent(transform, false);
        holdAnchor.localPosition = new Vector3(0f, holdHeight, holdDistance);
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
        nextVomitTime = Time.time + 8f;
        nextThrowTime = Time.time + 5f;
        nextGrabTime = Time.time + 15f;
        nextTauntTime = Time.time + Random.Range(10f, 25f);

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
            StartCloseAction(target);
            return;
        }

        // Una tirada por segundo para las acciones a distancia (vomito, salto, burla).
        if (Time.time >= nextRollTime)
        {
            nextRollTime = Time.time + 1f;
            if (TryStartRangedAction(target, distance))
                return;
        }

        Chase(distance);
    }

    // De cerca: agarrar y estrangular (raro), agarrar y lanzar, o el golpe normal.
    // Agarrar solo funciona con jugadores (los NPC reciben siempre el golpe normal).
    private void StartCloseAction(Transform victim)
    {
        bool isPlayer = victim.TryGetComponent(out PMovement _) && victim.TryGetComponent(out PlayerHealth _);

        if (isPlayer)
        {
            if (anim.HasGrab && Time.time >= nextGrabTime && Random.value < grabChance)
            {
                StartCoroutine(GrabRoutine(victim));
                return;
            }

            if (anim.HasThrow && Time.time >= nextThrowTime && Random.value < throwChance)
            {
                StartCoroutine(ThrowRoutine(victim));
                return;
            }
        }

        StartCoroutine(MeleeRoutine(victim));
    }

    private bool TryStartRangedAction(Transform victim, float distance)
    {
        if (anim.HasVomit && Time.time >= nextVomitTime
            && distance >= vomitMinRange && distance <= vomitRange - 1f
            && Random.value < vomitChance)
        {
            StartCoroutine(VomitRoutine(victim));
            return true;
        }

        if (anim.HasJump && Time.time >= nextJumpTime
            && distance >= jumpMinRange && distance <= jumpMaxRange
            && Random.value < jumpChance)
        {
            StartCoroutine(JumpRoutine(victim));
            return true;
        }

        if (anim.HasTaunt && Time.time >= nextTauntTime
            && distance >= tauntMinRange && distance <= tauntMaxRange
            && Random.value < tauntChance)
        {
            StartCoroutine(TauntRoutine(victim));
            return true;
        }

        return false;
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

    // Burla: se queda mirando al blanco un rato. No hace dano; el cooldown es largo para que sea poco frecuente.
    private IEnumerator TauntRoutine(Transform victim)
    {
        busy = true;
        Halt();

        float length = anim.BeginTaunt() + 0.05f;
        float t = 0f;

        while (t < length)
        {
            if (Dead) yield break;

            if (t < length * 0.4f)
                FaceTarget(victim);

            t += Time.deltaTime;
            yield return null;
        }

        nextTauntTime = Time.time + Random.Range(tauntCooldown.x, tauntCooldown.y);
        yield return Recover();
    }

    // Vomito: chorro de acido en cono. Quien lo recibe queda lento y con dano por segundo (PlayerAcidDot).
    // Cada jugador se afecta una sola vez por vomito; otro vomito le refresca el efecto (no se acumula).
    private IEnumerator VomitRoutine(Transform victim)
    {
        busy = true;
        Halt();

        float length = anim.BeginVomit() + 0.05f;
        float sprayStart = length * vomitSprayStart;
        float sprayEnd = Mathf.Max(length * vomitSprayEnd, sprayStart + 0.2f);

        HashSet<PlayerHealth> affected = new HashSet<PlayerHealth>();
        HashSet<RescuableNpc> affectedNpcs = new HashSet<RescuableNpc>();
        bool spraying = false;
        float nextCheck = 0f;
        float t = 0f;

        while (t < length)
        {
            if (Dead) yield break;

            if (t < sprayEnd)
                TurnToward(victim, vomitTurnSpeed);

            if (!spraying && t >= sprayStart)
            {
                spraying = true;
                AcidSprayClientRpc(sprayEnd - sprayStart);
            }

            if (spraying && t <= sprayEnd && t >= nextCheck)
            {
                nextCheck = t + 0.12f;
                Vector3 mouth = transform.TransformPoint(new Vector3(0f, mouthHeight, mouthForward));
                if (SprayHit(mouth, affected, affectedNpcs))
                    HitLandedClientRpc();
            }

            t += Time.deltaTime;
            yield return null;
        }

        nextVomitTime = Time.time + Random.Range(vomitCooldown.x, vomitCooldown.y);
        nextAttackTime = Time.time + attackCooldown * 0.5f;
        yield return Recover();
    }

    // Agarra al jugador, lo deja inmovil y lo estrangula (dano en varios ticks). Despues lo suelta.
    private IEnumerator GrabRoutine(Transform victim)
    {
        busy = true;
        Halt();

        victim.TryGetComponent(out PMovement movement);
        victim.TryGetComponent(out PlayerHealth victimHealth);

        float length = anim.BeginGrab() + 0.05f;
        float latchTime = length * grabLatchNormalized;
        float strangleStart = length * grabStrangleStart;
        float releaseTime = Mathf.Max(length * grabReleaseNormalized, strangleStart + 0.2f);
        float tickEvery = (releaseTime - strangleStart) / Mathf.Max(1, grabTicks);

        bool latchTried = false;
        bool held = false;
        int ticksDone = 0;
        float t = 0f;

        while (t < length)
        {
            if (Dead)
            {
                if (held) Free(movement, DropPointInFront());
                yield break;
            }

            if (!latchTried && t < latchTime)
                FaceTarget(victim);

            if (!latchTried && t >= latchTime)
            {
                latchTried = true;
                if (movement != null && victimHealth != null && !victimHealth.IsDead
                    && CanReach(victim, grabReach, grabHalfAngle))
                {
                    held = true;
                    HoldClientRpc(movement.NetworkObject, length - t + 0.5f);
                    HitLandedClientRpc();
                }
            }

            if (held && (movement == null || victimHealth == null))
                held = false; // el jugador se desconecto

            if (held && GameFinished)
            {
                held = false;
                Free(movement, DropPointInFront());
                yield break;
            }

            if (held && !victimHealth.IsDead && ticksDone < grabTicks && t >= strangleStart + ticksDone * tickEvery)
            {
                victimHealth.TakeDamage(grabTotalDamage / Mathf.Max(1, grabTicks));
                ticksDone++;
                HitLandedClientRpc();
            }

            if (held && t >= releaseTime)
            {
                held = false;
                Free(movement, DropPointInFront());
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (held)
            Free(movement, DropPointInFront());

        nextGrabTime = Time.time + Random.Range(grabCooldown.x, grabCooldown.y);
        nextAttackTime = Time.time + attackCooldown;
        yield return Recover();
    }

    // Golpea, agarra al jugador y lo lanza hacia ATRAS del jefe (por encima del hombro).
    private IEnumerator ThrowRoutine(Transform victim)
    {
        busy = true;
        Halt();

        victim.TryGetComponent(out PMovement movement);
        victim.TryGetComponent(out PlayerHealth victimHealth);

        float length = anim.BeginThrow() + 0.05f;
        float latchTime = length * throwLatchNormalized;
        float releaseTime = Mathf.Max(length * throwReleaseNormalized, latchTime + 0.1f);

        bool latchTried = false;
        bool held = false;
        float t = 0f;

        while (t < length)
        {
            if (Dead)
            {
                if (held) Free(movement, DropPointInFront());
                yield break;
            }

            if (!latchTried && t < latchTime)
                FaceTarget(victim);

            if (!latchTried && t >= latchTime)
            {
                latchTried = true;
                if (movement != null && victimHealth != null && !victimHealth.IsDead
                    && CanReach(victim, throwReach, throwHalfAngle))
                {
                    victimHealth.TakeDamage(throwDamage);
                    HitLandedClientRpc();

                    if (!victimHealth.IsDead)
                    {
                        held = true;
                        HoldClientRpc(movement.NetworkObject, length - t + 0.5f);
                    }
                }
            }

            if (held && (movement == null || victimHealth == null))
                held = false;

            if (held && t >= releaseTime)
            {
                held = false;

                Vector3 back = -FlatForward();
                Vector3 drop = transform.position + back * throwDropBehind + Vector3.up * throwDropHeight;
                Free(movement, drop);

                if (!victimHealth.IsDead)
                {
                    Vector3 velocity = back * throwHorizontal + Vector3.up * throwVertical;
                    LaunchClientRpc(movement.NetworkObject, velocity);
                }

                SlamClientRpc();
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (held)
            Free(movement, DropPointInFront());

        nextThrowTime = Time.time + Random.Range(throwCooldown.x, throwCooldown.y);
        nextAttackTime = Time.time + attackCooldown;
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

    // Gira de a poco (para seguir al blanco mientras vomita).
    private void TurnToward(Transform victim, float degreesPerSecond)
    {
        if (victim == null) return;
        Vector3 look = victim.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(look), degreesPerSecond * Time.deltaTime);
    }

    private Vector3 FlatForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    private Vector3 DropPointInFront()
    {
        return transform.position + FlatForward() * holdDistance;
    }

   private bool CanReach(Transform victim, float range, float halfAngle)
{
    if (victim == null) return false;
    if (!InArea(victim.position, transform.position, FlatForward(), range, halfAngle, out _))
        return false;
    return HasClearShot(transform.position + Vector3.up * attackRayHeight, victim, attackBlockMask);
}

    // ---------- Dano y lanzamiento ----------

    // Dana (y manda a volar a los jugadores) todo lo que este dentro del alcance y del cono. Devuelve true si pego a alguien.
   private bool DamageArea(Vector3 origin, Vector3 forward, float radius, float halfAngle, float damage)
{
    bool anyHit = false;
    forward.y = 0f;
    Vector3 rayOrigin = origin + Vector3.up * attackRayHeight;

    foreach (PMovement player in FindObjectsByType<PMovement>())
    {
        if (player == null) continue;
        if (!player.TryGetComponent(out PlayerHealth playerHealth) || playerHealth.IsDead) continue;
        if (!InArea(player.transform.position, origin, forward, radius, halfAngle, out Vector3 away)) continue;
        if (!HasClearShot(rayOrigin, player.transform, attackBlockMask)) continue;

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
        if (!HasClearShot(rayOrigin, npc.transform, attackBlockMask)) continue;

        npc.TakeDamage(damage);
        anyHit = true;
    }

    return anyHit;
}

    // Chorro de acido: a cada jugador/NPC dentro del cono lo afecta una sola vez por vomito.
    // Devuelve true si alguien nuevo fue alcanzado en esta pasada.
    private bool SprayHit(Vector3 origin, HashSet<PlayerHealth> affected, HashSet<RescuableNpc> affectedNpcs)
    {
        bool any = false;
        Vector3 forward = FlatForward();

        foreach (PMovement player in FindObjectsByType<PMovement>())
        {
            if (player == null) continue;
            if (!player.TryGetComponent(out PlayerHealth playerHealth) || playerHealth.IsDead) continue;
            if (affected.Contains(playerHealth)) continue;
            if (!InArea(player.transform.position, origin, forward, vomitRange, vomitHalfAngle, out _)) continue;
            if (!HasClearShot(origin, player.transform)) continue;   // hay una pared en medio

            affected.Add(playerHealth);
            any = true;

            PlayerAcidDot.Apply(playerHealth, acidDamagePerSecond, acidDuration);   // dano por segundo (server)
            if (player.NetworkObject != null)
                AcidSlowClientRpc(player.NetworkObject, acidSpeedMultiplier, acidDuration); // lentitud (dueño)
        }

        foreach (RescuableNpc npc in new List<RescuableNpc>(RescuableNpc.All))
        {
            if (npc == null || !npc.IsTargetable || affectedNpcs.Contains(npc)) continue;
            if (!InArea(npc.transform.position, origin, forward, vomitRange, vomitHalfAngle, out _)) continue;
            if (!HasClearShot(origin, npc.transform)) continue;      // hay una pared en medio

            affectedNpcs.Add(npc);
            npc.TakeDamage(npcAcidDamage);
            any = true;
        }

        return any;
    }

    private bool HasClearShot(Vector3 origin, Transform victim)
    {
        return HasClearShot(origin, victim, acidBlockMask);
    }
    // Linea de vision boca -> victima: si hay geometria solida en medio, el acido no llega.
    private bool HasClearShot(Vector3 origin, Transform victim, LayerMask mask)
{
    Vector3 target = AimPoint(victim);
    Vector3 dir = target - origin;
    float dist = dir.magnitude;
    if (dist < 0.05f)
        return true;

    RaycastHit[] hits = Physics.RaycastAll(origin, dir / dist, dist, mask, QueryTriggerInteraction.Ignore);
    foreach (RaycastHit hit in hits)
    {
        Transform h = hit.collider.transform;
        if (h.IsChildOf(transform) || h.IsChildOf(victim)) continue;
        if (hit.collider.GetComponentInParent<PlayerHealth>() != null) continue;
        if (hit.collider.GetComponentInParent<RescuableNpc>() != null) continue;
        return false;
    }

    return true;
}

    private static Vector3 AimPoint(Transform victim)
    {
        Collider col = victim.GetComponentInChildren<Collider>();
        return col != null ? col.bounds.center : victim.position + Vector3.up;
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

    // ---------- Efectos sobre el jugador (el movimiento es del cliente dueño) ----------

    private static bool TryGetOwnedMovement(NetworkObjectReference playerReference, out PMovement movement)
    {
        movement = null;
        if (!playerReference.TryGet(out NetworkObject playerObject) || !playerObject.IsOwner)
            return false;

        movement = playerObject.GetComponentInChildren<PMovement>();
        return movement != null;
    }

    // Solo el dueño del jugador aplica el empuje.
    [ClientRpc]
    private void LaunchClientRpc(NetworkObjectReference playerReference, Vector3 velocity)
    {
        if (TryGetOwnedMovement(playerReference, out PMovement movement))
            movement.Launch(velocity);
    }

    [ClientRpc]
    private void AcidSlowClientRpc(NetworkObjectReference playerReference, float multiplier, float duration)
    {
        if (TryGetOwnedMovement(playerReference, out PMovement movement))
            PlayerStatusEffects.For(movement).ApplySlow(multiplier, duration);
    }

    // El jugador queda inmovil y pegado al punto de agarre del jefe. Se suelta solo al vencer 'duration' (seguro anti-trabado).
    [ClientRpc]
    private void HoldClientRpc(NetworkObjectReference playerReference, float duration)
    {
        if (TryGetOwnedMovement(playerReference, out PMovement movement))
            PlayerStatusEffects.For(movement).Hold(holdAnchor, duration);
    }

    [ClientRpc]
    private void ReleaseClientRpc(NetworkObjectReference playerReference, Vector3 dropPoint, bool teleport)
    {
        if (TryGetOwnedMovement(playerReference, out PMovement movement))
            PlayerStatusEffects.For(movement).Release(teleport, dropPoint);
    }

    private void Free(PMovement movement, Vector3 dropPoint)
    {
        if (movement == null || movement.NetworkObject == null || !movement.NetworkObject.IsSpawned)
            return;

        ReleaseClientRpc(movement.NetworkObject, dropPoint, true);
    }

    // ---------- Sonido y particulas (todos los clientes) ----------

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

    [ClientRpc]
    private void AcidSprayClientRpc(float duration)
    {
        MiniBossAcidFx.Spray(transform, new Vector3(0f, mouthHeight, mouthForward), duration, vomitRange, vomitHalfAngle, acidBlockMask);
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
