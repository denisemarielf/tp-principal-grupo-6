using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class EnemyHealth : NetworkBehaviour
{
    [Header("Vida")]
    [SerializeField] private EnemyVariant variant = EnemyVariant.Normal;
    [SerializeField] private float maxHealth = 100f;

    [Header("Muerte")]
    [SerializeField] private AnimationClip deathClip;
    [SerializeField] private AnimationClip[] extraDeathClips;
    [SerializeField] private AudioClip[] deathSounds;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;
    [SerializeField] private float healthOverride = 0f;

    [System.Serializable]
    public class LootEntry
    {
        [Tooltip("Prefab con NetworkObject (ItemPickup de medikit, municion, etc.). Debe estar registrado en NetworkPrefabs.")]
        public GameObject prefab;
        [Tooltip("Peso relativo: mas alto = sale mas seguido entre los items del array.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Cantidad que da el drop (solo aplica si el prefab tiene ItemPickup).")]
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;
    }

    [Header("Loot")]
    [SerializeField, Range(0f, 1f)] private float lootDropChance = 0.25f;
    [SerializeField] private LootEntry[] lootTable;
    [SerializeField] private float lootHeightOffset = 0f;
    [SerializeField] private float lootScatterRadius = 0.4f;
    [SerializeField] private LayerMask lootGroundMask = ~0;
    [Tooltip("Segundos hasta que el drop desaparece si nadie lo agarra. 0 = no desaparece.")]
    [SerializeField] private float lootLifetime = 30f;

    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Tiene que declararse ANTES de isDead: asi el cliente ya conoce la variante cuando le llega isDead = true.
    private NetworkVariable<int> deathVariant = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Enemigos spawneados en este equipo (los civiles la usan para detectar peligro cerca).
    public static readonly List<EnemyHealth> All = new List<EnemyHealth>();

    private Ai ai;
    private EnemyCombat enemyCombat;
    private PlayableGraph deathGraph;
    private AnimationClipPlayable deathPlayable;
    private AnimationClip activeDeathClip;
    private Animator deathAnimator;
    private GameObject deathModel;
    private float groundY;
    private bool deathStarted;
    private bool pinToGround;

    public float MaxHealth => maxHealth;
    public EnemyVariant Variant => variant;
    public float DeathDuration
    {
        get
        {
            AnimationClip clip = CurrentDeathClip();
            return clip != null ? clip.length : 0f;
        }
    }

    // Variantes de muerte validas: deathClip primero y despues las extra que no esten vacias.
    private int DeathClipCount()
    {
        int count = deathClip != null ? 1 : 0;
        if (extraDeathClips != null)
        {
            foreach (AnimationClip clip in extraDeathClips)
            {
                if (clip != null)
                    count++;
            }
        }

        return count;
    }

    private AnimationClip DeathClipAt(int index)
    {
        int i = 0;
        if (deathClip != null)
        {
            if (index == 0)
                return deathClip;
            i = 1;
        }

        if (extraDeathClips != null)
        {
            foreach (AnimationClip clip in extraDeathClips)
            {
                if (clip == null)
                    continue;
                if (i == index)
                    return clip;
                i++;
            }
        }

        return deathClip;
    }

    private AnimationClip CurrentDeathClip()
    {
        return DeathClipAt(deathVariant.Value);
    }

    private void PlayDeathSound()
    {
        if (deathSounds == null || deathSounds.Length == 0)
            return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, deathVolume);
    }

    public override void OnNetworkSpawn()
    {
        ai = GetComponent<Ai>();
        enemyCombat = GetComponent<EnemyCombat>();
        All.Add(this);
        isDead.OnValueChanged += OnDeadChanged;

        // Tambien en clientes: la barra necesita el maximo (override o variante), ya escalado por dificultad.
        float baseHealth = healthOverride > 0f ? healthOverride : HealthFor(variant);
        maxHealth = baseHealth * DifficultyTuning.HealthMul;

        if (IsServer)
            currentHealth.Value = maxHealth;

        if (isDead.Value)
            PlayDeath();
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
        isDead.OnValueChanged -= OnDeadChanged;
        StopDeathGraph();
    }

    public override void OnDestroy()
    {
        All.Remove(this);
        StopDeathGraph();
        base.OnDestroy();
    }

    public void Configure(EnemyVariant newVariant)
    {
        variant = newVariant;
        maxHealth = HealthFor(variant) * DifficultyTuning.HealthMul;
    }

    public static float HealthFor(EnemyVariant value)
    {
        switch (value)
        {
            case EnemyVariant.Fragil:
                return 50f;
            case EnemyVariant.Resistente:
                return 250f;
            default:
                return 100f;
        }
    }

    public void TakeDamage(float amount, Transform attacker = null)
    {
        if (!IsServer) return;
        if (isDead.Value) return;

        currentHealth.Value -= amount;
        ImpactClientRpc();
        MatchStatsService.NotifyDamage(attacker, amount);

        if (attacker != null && ai != null)
            ai.SetAggroTarget(attacker);

        if (currentHealth.Value <= 0f)
        {
            AchievementService.NotifyEnemyKilled(attacker, variant);
            MatchStatsService.NotifyKill(attacker);
            Die();
        }
        else if (enemyCombat != null)
        {
            // Reaccion al golpe: el zombie se queda quieto mientras dura el clip (no corta un ataque en curso).
            if (attacker != null)
                enemyCombat.ApplyKnockback(attacker.position);
            enemyCombat.TryStagger();
        }
    }

    // El server avisa y cada cliente reproduce el sonido en su copia del zombie (incluido el golpe que lo mata).
    [ClientRpc]
    private void ImpactClientRpc()
    {
        EnemyAnimation animation = GetComponent<EnemyAnimation>();
        if (animation != null)
            animation.PlayImpactSound();

        EnemyVoice voice = GetComponent<EnemyVoice>();
        if (voice != null)
            voice.TryPlay(EnemyVoice.Kind.Hurt);
    }

    [ContextMenu("Debug: recibir 25 de dano")]
    private void DebugTakeDamage()
    {
        TakeDamage(25f);
    }

    private void Die()
    {
        if (!IsServer || isDead.Value) return;

        // Primero la variante y despues isDead, para que lleguen juntas y en ese orden.
        int count = DeathClipCount();
        deathVariant.Value = count > 1 ? Random.Range(0, count) : 0;
        isDead.Value = true;

        TryDropLoot();

        // Si estaba mordiendo a un jugador, lo suelta.
        if (enemyCombat != null)
            enemyCombat.ReleaseBiteTarget();

        if (ai != null)
            ai.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
            agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        PlayDeath();
        StartCoroutine(RemoveAfterDeath());
    }

    // Solo el servidor decide y spawnea; los clientes reciben el objeto por Netcode.
    private void TryDropLoot()
    {
        if (!IsServer) return;
        if (lootTable == null || lootTable.Length == 0) return;
        if (Random.value > lootDropChance) return;

        LootEntry entry = PickLootEntry();
        if (entry == null) return;

        Vector2 circle = Random.insideUnitCircle * lootScatterRadius;
        Vector3 pos = transform.position + new Vector3(circle.x, 0f, circle.y);

        pos = FindLootGround(pos) + Vector3.up * lootHeightOffset;

        GameObject instance = Instantiate(entry.prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogWarning($"[EnemyHealth] El prefab de loot '{entry.prefab.name}' no tiene NetworkObject.");
            Destroy(instance);
            return;
        }

        netObj.Spawn(true);

        // Cantidad aleatoria (ItemPickup.ServerSetQuantity ya existe y solo corre en servidor).
        if (entry.maxQuantity > 1)
        {
            ItemPickup pickup = instance.GetComponent<ItemPickup>();
            if (pickup != null)
            {
                int min = Mathf.Min(entry.minQuantity, entry.maxQuantity);
                pickup.ServerSetQuantity(Random.Range(min, entry.maxQuantity + 1));
            }
        }

        if (lootLifetime > 0f)
            instance.AddComponent<LootLifetime>().Begin(netObj, lootLifetime);
    }

    // Busca el piso bajo 'pos' ignorando al propio enemigo, a jugadores, a otros enemigos y a otros pickups.
    // Si no encuentra nada, usa la altura de los pies del enemigo (nunca queda en el aire).
    private Vector3 FindLootGround(Vector3 pos)
    {
        Vector3 fallback = new Vector3(pos.x, transform.position.y, pos.z);
        Vector3 origin = new Vector3(pos.x, transform.position.y + 1f, pos.z);

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 6f, lootGroundMask, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        Vector3 point = fallback;

        foreach (RaycastHit hit in hits)
        {
            Collider c = hit.collider;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            if (c.GetComponentInParent<PlayerHealth>() != null) continue;
            if (c.GetComponentInParent<ItemPickup>() != null) continue;

            if (hit.distance < best)
            {
                best = hit.distance;
                point = hit.point;
            }
        }

        return point;
    }

    // Seleccion por peso entre las entradas validas del array.
    private LootEntry PickLootEntry()
    {
        float total = 0f;
        foreach (LootEntry entry in lootTable)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0f)
                total += entry.weight;
        }

        if (total <= 0f)
            return null;

        float roll = Random.value * total;
        foreach (LootEntry entry in lootTable)
        {
            if (entry == null || entry.prefab == null || entry.weight <= 0f)
                continue;

            roll -= entry.weight;
            if (roll <= 0f)
                return entry;
        }

        return null;
    }

    private void OnDeadChanged(bool previous, bool current)
    {
        if (current)
            PlayDeath();
    }

    // El mismo clip humanoid se retargetea al modelo visible (fragil, normal o resistente).
    private void PlayDeath()
    {
        AnimationClip clip = CurrentDeathClip();
        if (deathStarted || clip == null)
            return;

        GameObject model = ActiveModel();
        if (model == null)
            return;

        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();

        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        clip.wrapMode = WrapMode.ClampForever;
        activeDeathClip = clip;

        // Los pies en la pose de pie marcan el piso. La caja del mesh no sigue los huesos al caer.
        deathAnimator = animator;
        deathModel = model;
        groundY = LowestBodyY();
        pinToGround = !float.IsPositiveInfinity(groundY);

        deathGraph = PlayableGraph.Create("EnemyDeath");
        deathPlayable = AnimationClipPlayable.Create(deathGraph, clip);
        deathPlayable.SetApplyFootIK(false);
        deathPlayable.SetDuration(clip.length);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(deathGraph, "Death", animator);
        output.SetSourcePlayable(deathPlayable);
        deathGraph.Play();
        deathStarted = true;
        PlayDeathSound();

        EnemyVoice voice = GetComponent<EnemyVoice>();
        if (voice != null)
            voice.TryPlay(EnemyVoice.Kind.Death);
    }

    // Mantiene el último frame y apoya el hueso más bajo en el piso donde estaba parado.
    private void LateUpdate()
    {
        if (!pinToGround || deathModel == null)
            return;

        if (deathPlayable.IsValid() && activeDeathClip != null && deathPlayable.GetTime() >= activeDeathClip.length)
        {
            deathPlayable.SetSpeed(0);
            deathPlayable.SetTime(activeDeathClip.length);
            if (deathGraph.IsValid())
                deathGraph.Evaluate(0);
        }

        float lowest = LowestBodyY();
        if (float.IsPositiveInfinity(lowest))
            return;

        float gap = lowest - groundY;
        if (Mathf.Abs(gap) > 0.001f)
            deathModel.transform.position -= new Vector3(0f, gap, 0f);
    }

    private float LowestBodyY()
    {
        float lowest = float.PositiveInfinity;
        if (deathAnimator != null && deathAnimator.avatar != null && deathAnimator.avatar.isHuman)
        {
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = deathAnimator.GetBoneTransform((HumanBodyBones)i);
                if (bone == null)
                    continue;
                if (bone.position.y < lowest)
                    lowest = bone.position.y;
            }
        }

        if (!float.IsPositiveInfinity(lowest) || deathModel == null)
            return lowest;

        foreach (Transform bone in deathModel.GetComponentsInChildren<Transform>())
        {
            if (bone == deathModel.transform)
                continue;
            if (bone.position.y < lowest)
                lowest = bone.position.y;
        }

        return lowest;
    }

    private GameObject ActiveModel()
    {
        EnemyAnimation anim = GetComponent<EnemyAnimation>();
        if (anim != null && anim.UsesRootAnimator && GetComponent<Animator>() != null)
            return gameObject;
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeInHierarchy)
                continue;
            if (child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                return child.gameObject;
        }

        return null;
    }

    private IEnumerator RemoveAfterDeath()
    {
        // La duracion depende de la variante elegida (Death y Death2 pueden durar distinto).
        float duration = DeathDuration;
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            // Solo el servidor puede despawnear
            if (!IsServer) yield break;

            if (NetworkObject.InScenePlaced)
            {
                // Colocado a mano en la escena: despawnear sin destruir
                NetworkObject.Despawn(false);
                gameObject.SetActive(false);
            }
            else
            {
                // Instanciado en runtime: destruir
                NetworkObject.Despawn(true);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void StopDeathGraph()
    {
        pinToGround = false;
        deathModel = null;
        deathAnimator = null;
        if (deathGraph.IsValid())
            deathGraph.Destroy();
    }

    public bool IsDead()
    {
        return isDead.Value;
    }

    public float GetCurrentHealth()
    {
        return currentHealth.Value;
    }
}