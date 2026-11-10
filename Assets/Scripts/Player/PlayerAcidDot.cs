using UnityEngine;

// Dano por segundo del acido. Solo corre en el server (PlayerHealth.TakeDamage es solo server).
// Un vomito nuevo refresca la duracion, no acumula dano.
public class PlayerAcidDot : MonoBehaviour
{
    private const float TickInterval = 0.5f;   // evita cambiar la NetworkVariable de vida en cada frame

    private PlayerHealth health;
    private float damagePerTick;
    private int ticksLeft;
    private float tickTimer;

    public static void Apply(PlayerHealth target, float damagePerSecond, float duration)
    {
        if (target == null || !target.IsServer || target.IsDead)
            return;

        PlayerAcidDot dot = target.GetComponent<PlayerAcidDot>();
        if (dot == null)
            dot = target.gameObject.AddComponent<PlayerAcidDot>();

        dot.Begin(target, damagePerSecond, duration);
    }

    private void Begin(PlayerHealth target, float damagePerSecond, float duration)
    {
        health = target;
        damagePerTick = damagePerSecond * TickInterval;
        ticksLeft = Mathf.Max(1, Mathf.RoundToInt(duration / TickInterval));
        tickTimer = TickInterval;
        enabled = true;
    }

    private void Update()
    {
        if (health == null || health.IsDead)
        {
            enabled = false;
            return;
        }

        if ((SafeZone.Current != null && SafeZone.Current.IsCompleted)
            || (SurvivalModeController.Current != null && SurvivalModeController.Current.IsFinished))
        {
            enabled = false;
            return;
        }

        tickTimer -= Time.deltaTime;
        while (tickTimer <= 0f && ticksLeft > 0)
        {
            health.TakeDamage(damagePerTick);
            ticksLeft--;
            tickTimer += TickInterval;
        }

        if (ticksLeft <= 0 || health.IsDead)
            enabled = false;
    }
}