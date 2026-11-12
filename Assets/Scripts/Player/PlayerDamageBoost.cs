using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerDamageBoost : NetworkBehaviour
{
    [Header("Efectos")]
    [SerializeField] private ParticleSystem boostParticles; // hijo del Player, con Simulation Space = Local
    [SerializeField] private AudioSource boostAudio;        // hijo del Player, Loop = true, Spatial Blend = 1
    [SerializeField] private AudioClip activateSound;       // opcional: sonido corto al activarse

    private float multiplier = 1f;
    private Coroutine boostRoutine;

    private readonly NetworkVariable<bool> isBoosted = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public float Multiplier => multiplier;

    public override void OnNetworkSpawn()
    {
        isBoosted.OnValueChanged += OnBoostChanged;
        // Para clientes que entran con el buff ya activo
        UpdateEffects(isBoosted.Value);
    }

    public override void OnNetworkDespawn()
    {
        isBoosted.OnValueChanged -= OnBoostChanged;
        multiplier = 1f;
        UpdateEffects(false);
    }

    /// <param name="amount">Porcentaje extra: 25 = +25% de daño</param>
    public void ApplyDamageBoost(float amount, float duration)
    {
        if (!IsServer) return;

        if (boostRoutine != null) StopCoroutine(boostRoutine);
        boostRoutine = StartCoroutine(BoostRoutine(amount, duration));
    }

    private IEnumerator BoostRoutine(float amount, float duration)
    {
        multiplier = 1f + amount / 100f;
        isBoosted.Value = true;

        yield return new WaitForSeconds(duration);

        multiplier = 1f;
        isBoosted.Value = false;
        boostRoutine = null;
    }

    [SerializeField] private AudioClip endSound;

    private void OnBoostChanged(bool previous, bool current)
    {
        UpdateEffects(current);

        if (boostAudio == null) return;

        if (current && !previous && activateSound != null)
            boostAudio.PlayOneShot(activateSound);
        else if (!current && previous && endSound != null)
            boostAudio.PlayOneShot(endSound);
    }

    private void UpdateEffects(bool active)
    {
        if (boostParticles != null)
        {
            if (active && !boostParticles.isPlaying) boostParticles.Play();
            else if (!active) boostParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (boostAudio != null)
        {
            if (active && !boostAudio.isPlaying) boostAudio.Play();
            else if (!active) boostAudio.Stop();
        }
    }
}