using Unity.Netcode;
using UnityEngine;

// Idle en los tres. El frágil corre; el normal y el duro caminan. El ataque lo marca el server.
// Variantes: la caminata se elige una vez por zombie, el ataque y la reaccion al golpe las sortea el server.
// Asi el server conoce el clip elegido y sabe cuanto dura (attackLengths / hitLengths).
public class EnemyAnimation : NetworkBehaviour
{
    private static readonly int StateHash = Animator.StringToHash("state");
    private static readonly int WalkIndexHash = Animator.StringToHash("walkIndex");
    private static readonly int AttackIndexHash = Animator.StringToHash("attackIndex");
    private static readonly int HitIndexHash = Animator.StringToHash("hitIndex");
    private const int Idle = 0;
    private const int Walk = 1;
    private const int Run = 2;
    private const int Attack = 3;
    private const int Hit = 4;
    private const int Bite = 5;
    private const float MoveThreshold = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] stepClips;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip[] landedClips;      // el golpe del zombie conecta con un jugador o civil
    [SerializeField] private AudioClip[] biteLandedClips;  // idem para la mordida (si esta vacio usa landedClips)
    [SerializeField, Range(0f, 1f)] private float landedVolume = 1f;
    [SerializeField] private AudioClip[] impactClips;   // sonidos de impacto cuando un jugador le pega
    [SerializeField, Range(0f, 1f)] private float impactVolume = 1f;
    [SerializeField] private float stepInterval = 0.5f;
    [SerializeField] private float runStepInterval = 0.3f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    private float nextStepTime;

    [SerializeField] private RuntimeAnimatorController locomotion;

    [Header("Variantes (las asigna EnemyLocomotionImporter)")]
    [SerializeField] private int walkVariants = 1;
    [SerializeField] private int attackVariants = 1;
    [SerializeField] private int hitVariants = 1;
    [SerializeField] private float[] attackLengths;
    [SerializeField] private float[] hitLengths;
    [SerializeField] private float biteLength; // 0 = no hay clip de mordida

    [Header("Ataque")]
    [Tooltip("Colchon extra por cada ciclo para que el cliente alcance a terminar el clip aunque llegue con retraso.")]
    [SerializeField] private float attackCycleMargin = 0.05f;
    [SerializeField] private float defaultAttackLength = 1.2f;
    [Tooltip("Escribe en la consola que ataque/mordida se sortea. Apagalo cuando termines de probar.")]
    [SerializeField] private bool debugLogs;

    [Header("Reaccion al golpe")]
    [SerializeField] private float defaultHitLength = 0.7f;
    [Tooltip("Pausa entre el final de una reaccion y la siguiente.")]
    [SerializeField] private float hitCooldown = 0.5f;

    // Las variantes se declaran ANTES de isAttacking para que lleguen juntas y en ese orden.
    private readonly NetworkVariable<int> walkVariant = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> attackVariant = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isBiting = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isAttacking = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private EnemyHealth health;
    private EnemyVoice voice;
    private int lastState = -1;
    private Vector3 lastPosition;
    private int hitIndex;
    private float hitUntil;
    private float nextHitTime;

    private void PlayRandom(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip);
    }

    public void SetAttacking(bool attacking)
    {
        if (!IsSpawned || !IsServer)
            return;

        if (!attacking && isBiting.Value)
            isBiting.Value = false;
        if (isAttacking.Value == attacking)
            return;

        isAttacking.Value = attacking;
    }

    // Server: sortea un ataque y devuelve cuanto dura. EnemyCombat no se mueve ni corta hasta que termine.
    public float StartAttackCycle()
    {
        if (!IsSpawned || !IsServer)
            return defaultAttackLength;

        int variant = Random.Range(0, Mathf.Max(1, attackVariants));
        attackVariant.Value = variant;
        if (debugLogs)
            Debug.Log("[EnemyAnimation] " + name + ": ataque variante " + variant + " de " + attackVariants, this);
        if (isBiting.Value)
            isBiting.Value = false;
        SetAttacking(true);
        return LengthOf(attackLengths, variant, defaultAttackLength) + attackCycleMargin;
    }

    public bool HasBite => biteLength > 0f;

    // Server: arranca la mordida y devuelve cuanto dura. Mismo trato que un ataque: no se corta.
    public float StartBite()
    {
        if (!IsSpawned || !IsServer)
            return biteLength;

        isBiting.Value = true;
        if (debugLogs)
            Debug.Log("[EnemyAnimation] " + name + ": MORDIDA (" + biteLength + " s)", this);
        SetAttacking(true);
        return biteLength + attackCycleMargin;
    }

    // Server: dispara la reaccion al golpe y devuelve cuanto dura (0 si no se reproduce).
    public float PlayHit()
    {
        if (!IsSpawned || !IsServer)
            return 0f;
        if (Time.time < nextHitTime)
            return 0f;

        int variant = Random.Range(0, Mathf.Max(1, hitVariants));
        float duration = LengthOf(hitLengths, variant, defaultHitLength);
        nextHitTime = Time.time + duration + hitCooldown;
        HitClientRpc(variant);
        return duration;
    }

    [ClientRpc]
    private void HitClientRpc(int variant)
    {
        hitIndex = variant;
        hitUntil = Time.time + LengthOf(hitLengths, variant, defaultHitLength);
    }

    private static float LengthOf(float[] lengths, int index, float fallback)
    {
        if (lengths == null || index < 0 || index >= lengths.Length || lengths[index] <= 0f)
            return fallback;

        return lengths[index];
    }

    public override void OnNetworkSpawn()
    {
        health = GetComponent<EnemyHealth>();
        voice = GetComponent<EnemyVoice>();
        lastPosition = transform.position;

        if (IsServer && debugLogs)
            Debug.Log("[EnemyAnimation] " + name + ": attackVariants=" + attackVariants + ", attackLengths=" + (attackLengths != null ? attackLengths.Length : 0) + ", biteLength=" + biteLength + ", hitVariants=" + hitVariants, this);

        if (IsServer)
            walkVariant.Value = Random.Range(0, Mathf.Max(1, walkVariants));
    }

    private void Update()
    {
        if (!IsSpawned || locomotion == null)
            return;
        if (health != null && health.IsDead())
            return;

        Animator animator = ActiveAnimator();
        if (animator == null)
            return;

        if (animator.runtimeAnimatorController != locomotion)
        {
            animator.runtimeAnimatorController = locomotion;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        int state = Idle;
        if (isBiting.Value)
            state = Bite;
        else if (isAttacking.Value)
            state = Attack;
        else if (Time.time < hitUntil)
            state = Hit;
        else if (HorizontalSpeed() > MoveThreshold)
            state = ActiveModelIsFragil() ? Run : Walk;

        // Voz al empezar a atacar o morder (EnemyVoice decide si hay "lugar" para que suene).
        if (state != lastState)
        {
            if (voice != null && (state == Attack || state == Bite))
                voice.TryPlay(EnemyVoice.Kind.Attack);
            lastState = state;
        }

        if (state == Walk || state == Run)
        {
            if (Time.time >= nextStepTime)
            {
                PlayRandom(stepClips);
                nextStepTime = Time.time + (state == Run ? runStepInterval : stepInterval);
            }
        }

        animator.SetInteger(WalkIndexHash, Mathf.Clamp(walkVariant.Value, 0, Mathf.Max(0, walkVariants - 1)));
        animator.SetInteger(AttackIndexHash, Mathf.Clamp(attackVariant.Value, 0, Mathf.Max(0, attackVariants - 1)));
        animator.SetInteger(HitIndexHash, Mathf.Clamp(hitIndex, 0, Mathf.Max(0, hitVariants - 1)));
        animator.SetInteger(StateHash, state);
        lastPosition = transform.position;
    }

    private float HorizontalSpeed()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        return delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
    }

    private bool ActiveModelIsFragil()
    {
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy && child.name == "Fragil")
                return true;
        }

        return false;
    }

    private Animator ActiveAnimator()
    {
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeInHierarchy)
                continue;
            if (child.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                continue;

            Animator animator = child.GetComponent<Animator>();
            if (animator == null)
                animator = child.gameObject.AddComponent<Animator>();
            if (child.GetComponent<EnemyAttackEventRelay>() == null)
                child.gameObject.AddComponent<EnemyAttackEventRelay>();
            return animator;
        }

        return null;
    }

    public void PlayAttackHitSound()
    {
        PlayRandom(attackClips);
    }

    // Se reproduce en todos los clientes cuando el golpe del zombie conecta (ver EnemyCombat.DealDamage).
    public void PlayLandedSound(bool bite)
    {
        AudioClip[] clips = bite && biteLandedClips != null && biteLandedClips.Length > 0
            ? biteLandedClips
            : landedClips;

        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, landedVolume);
    }

    // Se reproduce en todos los clientes cuando el enemigo recibe un impacto (ver EnemyHealth).
    public void PlayImpactSound()
    {
        if (audioSource == null || impactClips == null || impactClips.Length == 0)
            return;

        AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
        if (clip == null)
            return;

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, impactVolume);
    }
}