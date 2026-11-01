using UnityEngine;

// Va en el modelo hijo (donde está el Animator). Reenvía el evento del clip al EnemyCombat del padre.
public class EnemyAttackEventRelay : MonoBehaviour
{
    private EnemyCombat combat;
    private EnemyAnimation enemyAnimation;

    public void OnAttackHit()
    {
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