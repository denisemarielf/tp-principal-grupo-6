using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Helicóptero de huida sincronizado con Netcode for GameObjects.
///
/// Idea: el servidor solo guarda en una NetworkVariable el momento (tiempo de servidor)
/// en que empezó el vuelo. Cada cliente calcula la pose del helicóptero a partir de ese
/// tiempo, así todos lo ven igual (vuelo + balanceo) y quien entra tarde también lo ve bien.
///
/// Requisitos:
///  - El helicóptero es un NetworkObject colocado en la escena (con startPoint y hoverPoint en la escena).
///  - NO agregar NetworkTransform: la posición se calcula localmente de forma determinista.
///  - Llamar a StartArrival() desde el servidor/host.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class HelicopterEscapeNetworked : NetworkBehaviour
{
    [Header("Puntos")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform hoverPoint;

    [Header("Vuelo")]
    [SerializeField] private bool playOnSpawn = false;       // solo lo respeta el servidor
    [Tooltip("Velocidad media del vuelo en metros por segundo (10 = 36 km/h, 20 = 72 km/h)")]
    [SerializeField] private float flightSpeed = 12f;
    [Tooltip("Evita duraciones absurdamente cortas si los puntos están muy cerca")]
    [SerializeField] private float minFlightDuration = 2f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float forwardTiltAngle = 12f;
    [SerializeField] private bool faceMovement = true;
    [SerializeField] private bool alignToHoverPointOnArrival = true;

    [Header("Hover (balanceo)")]
    [SerializeField] private float bobAmplitude = 0.25f;
    [SerializeField] private float bobSpeed = 1.2f;
    [SerializeField] private float driftAmplitude = 0.15f;
    [SerializeField] private float driftSpeed = 0.7f;
    [SerializeField] private float swayAngle = 2.5f;
    [SerializeField] private float swaySpeed = 0.9f;
    [SerializeField] private float hoverBlendTime = 1.5f;
    [Tooltip("Debe ser igual en todos los clientes (valor del prefab/escena), no aleatorio")]
    [SerializeField] private float noiseSeed = 17.3f;

    [Header("Rotor (opcional)")]
    [SerializeField] private Transform mainRotor;
    [SerializeField] private Transform tailRotor;
    [SerializeField] private Vector3 mainRotorAxis = Vector3.up;
    [SerializeField] private Vector3 tailRotorAxis = Vector3.right;
    [SerializeField] private float rotorSpeed = 1200f;

    [Header("Audio")]
    [SerializeField] private AudioClip rotorLoopClip;
    [SerializeField] private AudioClip arrivalClip;
    [SerializeField, Range(0f, 1f)] private float rotorVolume = 0.8f;
    [SerializeField] private float audioFadeInTime = 2f;
    [SerializeField] private float flightPitch = 1.1f;
    [SerializeField] private float hoverPitch = 1.0f;
    [SerializeField] private float pitchChangeSpeed = 1.5f;
    [SerializeField] private float audioMinDistance = 15f;
    [SerializeField] private float audioMaxDistance = 150f;
    [SerializeField] private bool keepSoundWhileHovering = true;

    [Header("Eventos (se disparan en cada cliente)")]
    public UnityEvent onArrived;

    // -1 = todavía no empezó. Solo el servidor escribe; todos leen.
    private readonly NetworkVariable<double> flightStartTime = new NetworkVariable<double>(-1.0);

    private enum State { Idle, Flying, Hovering }
    private State state = State.Idle;

    private bool arrivedEventFired;
    private AudioSource rotorSource;
    private float audioTimer;

    // ---------------------------------------------------------------- Netcode

    public override void OnNetworkSpawn()
    {
        SetupAudio();
        ApplyPose(-1.0); // colocarlo en el punto de inicio

        flightStartTime.OnValueChanged += OnFlightStartChanged;

        // Cliente que entra tarde: si el vuelo ya empezó, arrancar el audio
        if (flightStartTime.Value >= 0.0) PlayRotorSound();

        if (IsServer && playOnSpawn) StartArrival();
    }

    public override void OnNetworkDespawn()
    {
        flightStartTime.OnValueChanged -= OnFlightStartChanged;
    }

    private void OnFlightStartChanged(double previous, double current)
    {
        arrivedEventFired = false;
        if (current >= 0.0) PlayRotorSound();
    }

    /// <summary>Inicia el vuelo para todos los clientes. Solo funciona en servidor/host.</summary>
    public void StartArrival()
    {
        if (!IsServer)
        {
            Debug.LogWarning("HelicopterEscapeNetworked.StartArrival() solo puede llamarse desde el servidor.");
            return;
        }
        if (flightStartTime.Value >= 0.0) return; // ya empezó

        flightStartTime.Value = NetworkManager.ServerTime.Time;
    }

    // ----------------------------------------------------------------- Update

    private void Update()
    {
        if (!IsSpawned || startPoint == null || hoverPoint == null) return;

        double elapsed = flightStartTime.Value < 0.0
            ? -1.0
            : NetworkManager.ServerTime.Time - flightStartTime.Value;

        ApplyPose(elapsed);
        SpinRotors();
        UpdateAudio();
    }

    /// <summary>Calcula posición y rotación solo a partir del tiempo transcurrido (determinista).</summary>
    private void ApplyPose(double elapsed)
    {
        if (startPoint == null || hoverPoint == null) return;

        Vector3 from = startPoint.position;
        Vector3 to = hoverPoint.position;

        Vector3 flatDir = new Vector3(to.x - from.x, 0f, to.z - from.z);
        Quaternion baseRot = (faceMovement && flatDir.sqrMagnitude > 0.001f)
            ? Quaternion.LookRotation(flatDir.normalized, Vector3.up)
            : startPoint.rotation;

        // Antes de empezar: quieto en el punto de inicio
        if (elapsed < 0.0)
        {
            state = State.Idle;
            transform.SetPositionAndRotation(from, baseRot);
            return;
        }

        // Duración = distancia / velocidad media (igual en todos los clientes: los puntos son de escena)
        float distance = Vector3.Distance(from, to);
        float duration = Mathf.Max(minFlightDuration, distance / Mathf.Max(0.1f, flightSpeed));

        // ---- Vuelo
        if (elapsed < duration)
        {
            state = State.Flying;
            float t = (float)(elapsed / duration);
            float k = speedCurve.Evaluate(t);
            float tilt = Mathf.Sin(t * Mathf.PI) * forwardTiltAngle;

            transform.SetPositionAndRotation(
                Vector3.Lerp(from, to, k),
                baseRot * Quaternion.Euler(tilt, 0f, 0f));
            return;
        }

        // ---- Hover
        if (state != State.Hovering)
        {
            state = State.Hovering;
            if (!arrivedEventFired)
            {
                arrivedEventFired = true;
                if (arrivalClip != null && rotorSource != null) rotorSource.PlayOneShot(arrivalClip);
                onArrived?.Invoke();
            }
        }

        float hoverTime = (float)(elapsed - duration);
        float tn = hoverTime + noiseSeed;
        float blend = Mathf.SmoothStep(0f, 1f, hoverTime / Mathf.Max(0.01f, hoverBlendTime));

        float bob = (Mathf.PerlinNoise(tn * bobSpeed, 0f) - 0.5f) * 2f * bobAmplitude;
        float driftX = (Mathf.PerlinNoise(tn * driftSpeed, 10f) - 0.5f) * 2f * driftAmplitude;
        float driftZ = (Mathf.PerlinNoise(tn * driftSpeed, 20f) - 0.5f) * 2f * driftAmplitude;
        Vector3 pos = to + new Vector3(driftX, bob, driftZ) * blend;

        float pitch = (Mathf.PerlinNoise(tn * swaySpeed, 30f) - 0.5f) * 2f * swayAngle;
        float roll = (Mathf.PerlinNoise(tn * swaySpeed, 40f) - 0.5f) * 2f * swayAngle;
        float yaw = (Mathf.PerlinNoise(tn * swaySpeed * 0.5f, 50f) - 0.5f) * 2f * (swayAngle * 0.5f);

        Quaternion hoverBaseRot = alignToHoverPointOnArrival ? hoverPoint.rotation : baseRot;
        Quaternion rot = Quaternion.Slerp(baseRot, hoverBaseRot, blend)
                         * Quaternion.Euler(pitch * blend, yaw * blend, roll * blend);

        transform.SetPositionAndRotation(pos, rot);
    }

    // ------------------------------------------------------------------ Rotor

    private void SpinRotors()
    {
        if (state == State.Idle) return;
        if (mainRotor != null)
            mainRotor.Rotate(mainRotorAxis, rotorSpeed * Time.deltaTime, Space.Self);
        if (tailRotor != null)
            tailRotor.Rotate(tailRotorAxis, rotorSpeed * 1.5f * Time.deltaTime, Space.Self);
    }

    // ------------------------------------------------------------------ Audio (local en cada cliente)

    private void SetupAudio()
    {
        rotorSource = GetComponent<AudioSource>();
        if (rotorSource == null) rotorSource = gameObject.AddComponent<AudioSource>();

        rotorSource.clip = rotorLoopClip;
        rotorSource.loop = true;
        rotorSource.playOnAwake = false;
        rotorSource.spatialBlend = 1f;
        rotorSource.rolloffMode = AudioRolloffMode.Linear;
        rotorSource.minDistance = audioMinDistance;
        rotorSource.maxDistance = audioMaxDistance;
        rotorSource.dopplerLevel = 0.5f;
        rotorSource.volume = 0f;
        rotorSource.pitch = flightPitch;
    }

    private void PlayRotorSound()
    {
        if (rotorSource == null || rotorLoopClip == null) return;
        audioTimer = 0f;
        if (!rotorSource.isPlaying) rotorSource.Play();
    }

    private void UpdateAudio()
    {
        if (rotorSource == null || !rotorSource.isPlaying) return;

        audioTimer += Time.deltaTime;
        float fade = Mathf.Clamp01(audioTimer / Mathf.Max(0.01f, audioFadeInTime));
        float targetVolume = (state == State.Hovering && !keepSoundWhileHovering) ? 0f : rotorVolume;
        rotorSource.volume = Mathf.MoveTowards(rotorSource.volume, targetVolume * fade, Time.deltaTime);

        float targetPitch = state == State.Flying ? flightPitch : hoverPitch;
        rotorSource.pitch = Mathf.Lerp(rotorSource.pitch, targetPitch, Time.deltaTime * pitchChangeSpeed);
    }

    private void OnDrawGizmos()
    {
        if (startPoint == null || hoverPoint == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startPoint.position, 0.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(hoverPoint.position, 0.5f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPoint.position, hoverPoint.position);
    }
}