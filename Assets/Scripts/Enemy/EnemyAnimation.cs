using Unity.Netcode;
using UnityEngine;

// Idle en los tres. El frágil corre; el normal y el duro caminan. El ataque lo marca el server.
public class EnemyAnimation : NetworkBehaviour
{
    private static readonly int StateHash = Animator.StringToHash("state");
    private const int Idle = 0;
    private const int Walk = 1;
    private const int Run = 2;
    private const int Attack = 3;
    private const float MoveThreshold = 0.2f;

    [SerializeField] private RuntimeAnimatorController locomotion;

    private readonly NetworkVariable<bool> isAttacking = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private EnemyHealth health;
    private Vector3 lastPosition;

    public void SetAttacking(bool attacking)
    {
        if (!IsSpawned || !IsServer || isAttacking.Value == attacking)
            return;

        isAttacking.Value = attacking;
    }

    public override void OnNetworkSpawn()
    {
        health = GetComponent<EnemyHealth>();
        lastPosition = transform.position;
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
        if (isAttacking.Value)
            state = Attack;
        else if (HorizontalSpeed() > MoveThreshold)
            state = ActiveModelIsFragil() ? Run : Walk;

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
            return animator;
        }

        return null;
    }
}
