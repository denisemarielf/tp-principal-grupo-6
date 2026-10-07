using System.Collections;
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

    private Ai ai;
    private PlayableGraph deathGraph;
    private AnimationClipPlayable deathPlayable;
    private Animator deathAnimator;
    private GameObject deathModel;
    private float groundY;
    private bool deathStarted;
    private bool pinToGround;

    public float MaxHealth => maxHealth;
    public EnemyVariant Variant => variant;
    public float DeathDuration => deathClip != null ? deathClip.length : 0f;

    public override void OnNetworkSpawn()
    {
        ai = GetComponent<Ai>();
        isDead.OnValueChanged += OnDeadChanged;

        if (IsServer)
        {
            maxHealth = HealthFor(variant);
            currentHealth.Value = maxHealth;
        }

        if (isDead.Value)
            PlayDeath();
    }

    public override void OnNetworkDespawn()
    {
        isDead.OnValueChanged -= OnDeadChanged;
        StopDeathGraph();
    }

    public override void OnDestroy()
    {
        StopDeathGraph();
        base.OnDestroy();
    }

    public void Configure(EnemyVariant newVariant)
    {
        variant = newVariant;
        maxHealth = HealthFor(variant);
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

        if (attacker != null && ai != null)
            ai.SetAggroTarget(attacker);

        if (currentHealth.Value <= 0f)
            Die();
    }

    [ContextMenu("Debug: recibir 25 de dano")]
    private void DebugTakeDamage()
    {
        TakeDamage(25f);
    }

    private void Die()
    {
        if (!IsServer || isDead.Value) return;

        isDead.Value = true;

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

    private void OnDeadChanged(bool previous, bool current)
    {
        if (current)
            PlayDeath();
    }

    // El mismo clip humanoid se retargetea al modelo visible (fragil, normal o resistente).
    private void PlayDeath()
    {
        if (deathStarted || deathClip == null)
            return;

        GameObject model = ActiveModel();
        if (model == null)
            return;

        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();

        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        deathClip.wrapMode = WrapMode.ClampForever;

        // Los pies en la pose de pie marcan el piso. La caja del mesh no sigue los huesos al caer.
        deathAnimator = animator;
        deathModel = model;
        groundY = LowestBodyY();
        pinToGround = !float.IsPositiveInfinity(groundY);

        deathGraph = PlayableGraph.Create("EnemyDeath");
        deathPlayable = AnimationClipPlayable.Create(deathGraph, deathClip);
        deathPlayable.SetApplyFootIK(false);
        deathPlayable.SetDuration(deathClip.length);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(deathGraph, "Death", animator);
        output.SetSourcePlayable(deathPlayable);
        deathGraph.Play();
        deathStarted = true;
    }

    // Mantiene el último frame y apoya el hueso más bajo en el piso donde estaba parado.
    private void LateUpdate()
    {
        if (!pinToGround || deathModel == null)
            return;

        if (deathPlayable.IsValid() && deathClip != null && deathPlayable.GetTime() >= deathClip.length)
        {
            deathPlayable.SetSpeed(0);
            deathPlayable.SetTime(deathClip.length);
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
        if (deathClip != null)
            yield return new WaitForSeconds(deathClip.length);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
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
