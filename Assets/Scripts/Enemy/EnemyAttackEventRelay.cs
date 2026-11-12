using UnityEngine;

// Va en el modelo hijo (donde está el Animator). Reenvía el evento del clip al EnemyCombat del padre.
public class EnemyAttackEventRelay : MonoBehaviour
{
    private EnemyCombat combat;
    private EnemyAnimation enemyAnimation;
    private EnemyHealth health;

    public void OnAttackHit()
    {
        if (health == null)
            health = GetComponentInParent<EnemyHealth>();

        // Un zombie muerto no suena ni hace dano, aunque el clip siga disparando el evento.
        if (health != null && health.IsDead())
            return;

        if (combat == null)
            combat = GetComponentInParent<EnemyCombat>();
        if (enemyAnimation == null)
            enemyAnimation = GetComponentInParent<EnemyAnimation>();

        if (enemyAnimation != null)
            enemyAnimation.PlayAttackHitSound();
        if (combat != null)
            combat.DealDamage();
    }
}