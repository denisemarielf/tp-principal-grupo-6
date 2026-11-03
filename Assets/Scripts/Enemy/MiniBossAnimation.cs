using Unity.Netcode;
using UnityEngine;

// Animacion del mini jefe. El server decide el estado (MiniBossBrain) y todos los clientes lo reproducen.
// Idle / Walk / Run son bucles. Attack y Jump son acciones completas que el server no corta:
// la duracion real de cada clip se guarda en attackLengths / jumpLengths (las asigna MiniBossBuilder).
public class MiniBossAnimation : NetworkBehaviour
{
    public const int Idle = 0;
    public const int Walk = 1;
    public const int Run = 2;
    public const int Attack = 3;
    public const int Jump = 4;

    private static readonly int StateHash = Animator.StringToHash("state");
    private static readonly int IdleIndexHash = Animator.StringToHash("idleIndex");
    private static readonly int AttackIndexHash = Animator.StringToHash("attackIndex");
    private static readonly int JumpIndexHash = Animator.StringToHash("jumpIndex");

    [SerializeField] private RuntimeAnimatorController locomotion;

    [Header("Variantes (las asigna MiniBossBuilder)")]
    [SerializeField] private int idleVariants = 1;
    [SerializeField] private int attackVariants = 1;
    [SerializeField] private int jumpVariants = 0; // 0 = no hay clip de salto: el jefe nunca salta
    [SerializeField] private float[] attackLengths;
    [SerializeField] private float[] jumpLengths;
    [SerializeField] private float defaultAttackLength = 1.2f;
    [SerializeField] private float defaultJumpLength = 1.6f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] stepClips;
    [SerializeField] private AudioClip[] attackClips;   // grito / esfuerzo al empezar un golpe o un salto
    [SerializeField] private AudioClip[] hitClips;      // el golpe conecta
    [SerializeField] private AudioClip[] slamClips;     // aterrizaje del salto
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private float stepInterval = 0.75f;
    [SerializeField] private float runStepInterval = 0.4f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.05f);

    // Las variantes se declaran ANTES de state para que lleguen juntas y en ese orden.
    private readonly NetworkVariable<int> idleVariant = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> attackVariant = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> jumpVariant = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> state = new NetworkVariable<int>(
        Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private EnemyHealth health;
    private EnemyVoice voice;
    private Animator cachedAnimator;
    private int lastState = -1;
    private float nextStepTime;

    public bool HasJump => jumpVariants > 0;

    public override void OnNetworkSpawn()
    {
        health = GetComponent<EnemyHealth>();
        voice = GetComponent<EnemyVoice>();
        if (IsServer)
            idleVariant.Value = Random.Range(0, Mathf.Max(1, idleVariants));
    }

    // ---------- Server ----------

    // Idle, Walk o Run.
    public void SetLocomotion(int value)
    {
        if (!IsSpawned || !IsServer)
            return;

        if (value == Idle && state.Value != Idle)
            idleVariant.Value = Random.Range(0, Mathf.Max(1, idleVariants));

        state.Value = value;
    }

    // Sortea un golpe, lo arranca y devuelve cuanto dura el clip.
    public float BeginAttack(out int variant)
    {
        variant = Random.Range(0, Mathf.Max(1, attackVariants));
        if (!IsSpawned || !IsServer)
            return defaultAttackLength;

        attackVariant.Value = variant;
        state.Value = Attack;
        return LengthOf(attackLengths, variant, defaultAttackLength);
    }

    // Sortea un salto, lo arranca y devuelve cuanto dura el clip.
    public float BeginJump(out int variant)
    {
        variant = Random.Range(0, Mathf.Max(1, jumpVariants));
        if (!IsSpawned || !IsServer)
            return defaultJumpLength;

        jumpVariant.Value = variant;
        state.Value = Jump;
        return LengthOf(jumpLengths, variant, defaultJumpLength);
    }

    private static float LengthOf(float[] lengths, int index, float fallback)
    {
        if (lengths == null || index < 0 || index >= lengths.Length || lengths[index] <= 0f)
            return fallback;

        return lengths[index];
    }

    // ---------- Todos ----------

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

        int current = state.Value;
        if (current != lastState)
        {
            if (current == Attack || current == Jump)
            {
                // Si el jefe tiene EnemyVoice, la voz de ataque sale por ahi (mismo presupuesto de voces que los zombis).
                if (voice != null)
                    voice.TryPlay(EnemyVoice.Kind.Attack);
                else
                    PlayRandom(attackClips, 1f);
            }
            lastState = current;
        }

        if ((current == Walk || current == Run) && Time.time >= nextStepTime)
        {
            PlayRandom(stepClips, 1f);
            nextStepTime = Time.time + (current == Run ? runStepInterval : stepInterval);
        }

        animator.SetInteger(IdleIndexHash, Mathf.Clamp(idleVariant.Value, 0, Mathf.Max(0, idleVariants - 1)));
        animator.SetInteger(AttackIndexHash, Mathf.Clamp(attackVariant.Value, 0, Mathf.Max(0, attackVariants - 1)));
        animator.SetInteger(JumpIndexHash, Mathf.Clamp(jumpVariant.Value, 0, Mathf.Max(0, jumpVariants - 1)));
        animator.SetInteger(StateHash, current);
    }

    private Animator ActiveAnimator()
    {
        if (cachedAnimator != null && cachedAnimator.gameObject.activeInHierarchy)
            return cachedAnimator;

        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeInHierarchy)
                continue;
            if (child.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                continue;

            Animator animator = child.GetComponent<Animator>();
            if (animator == null)
                animator = child.gameObject.AddComponent<Animator>();

            cachedAnimator = animator;
            return animator;
        }

        return null;
    }

    // Los llama MiniBossBrain desde sus ClientRpc.
    public void PlayHitSound() { PlayRandom(hitClips, volume); }
    public void PlaySlamSound() { PlayRandom(slamClips, volume); }

    private void PlayRandom(AudioClip[] clips, float clipVolume)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
            return;

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, clipVolume * volume);
    }
}
