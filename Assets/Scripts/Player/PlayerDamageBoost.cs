using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerDamageBoost : NetworkBehaviour
{
    private float multiplier = 1f;
    private Coroutine boostRoutine;

    public float Multiplier => multiplier;

    /// <param name="amount">Porcentaje extra: 25 = +25% de daño</param>
    public void ApplyDamageBoost(float amount, float duration)
    {
        if (!IsServer) return;

        // Si ya había un buff activo, se reemplaza y se reinicia el tiempo
        if (boostRoutine != null) StopCoroutine(boostRoutine);
        boostRoutine = StartCoroutine(BoostRoutine(amount, duration));
    }

    private IEnumerator BoostRoutine(float amount, float duration)
    {
        multiplier = 1f + amount / 100f;
        yield return new WaitForSeconds(duration);
        multiplier = 1f;
        boostRoutine = null;
    }

    public override void OnNetworkDespawn()
    {
        multiplier = 1f;
    }
}