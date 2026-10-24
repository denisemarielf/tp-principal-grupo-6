using System;
using UnityEngine;

/// <summary>
/// Simula animaciones de ataque para armas melee SIN Animator.
/// Anima el viewmodel y expone PositionOffset / RotationOffset
/// para que WorldWeaponSway los copie al modelo en tercera persona.
/// </summary>
public class ProceduralMeleeAnimation : MonoBehaviour
{
    [Serializable]
    public class SwingPose
    {
        public string name = "Tajo";
        [Header("Preparación (windup)")]
        public Vector3 windupPosition;
        public Vector3 windupEuler;
        [Header("Golpe (strike)")]
        public Vector3 strikePosition;
        public Vector3 strikeEuler;
    }

    [Header("Referencias")]
    [SerializeField] private MeleeLogic melee;
    [Tooltip("Transform(s) del VIEWMODEL a animar. Vacío = este mismo objeto. " +
             "NO pongas acá el weapon_world: de ese se encarga WorldWeaponSway.")]
    [SerializeField] private Transform[] targets;

    [Header("Patrones de ataque")]
    [SerializeField] private SwingPose[] swings;
    [SerializeField] private float comboResetTime = 1.2f;

    [Header("Tiempos")]
    [Range(0.1f, 0.9f)][SerializeField] private float windupPortion = 0.6f;
    [SerializeField] private float holdTime = 0.05f;
    [SerializeField] private float fallbackHitDelay = 0.2f;
    [SerializeField] private float fallbackCooldown = 0.7f;

    [Header("Equipar")]
    [SerializeField] private bool playEquipAnimation = true;
    [SerializeField] private Vector3 equipPosition = new Vector3(0f, -0.25f, -0.1f);
    [SerializeField] private Vector3 equipEuler = new Vector3(40f, 0f, 0f);
    [SerializeField] private float equipDuration = 0.35f;

    [Header("Reposo (respiración)")]
    [SerializeField] private Vector3 idleAmplitude = new Vector3(0f, 0.004f, 0f);
    [SerializeField] private Vector3 idleEulerAmplitude = new Vector3(0.4f, 0f, 0.3f);
    [SerializeField] private float idleSpeed = 1.2f;

    // ---- Offsets actuales (los lee WorldWeaponSway) ----
    public Vector3 PositionOffset { get { Evaluate(); return posOffset; } }
    public Quaternion RotationOffset { get { Evaluate(); return rotOffset; } }

    private Vector3 posOffset;
    private Quaternion rotOffset = Quaternion.identity;
    private int evalFrame = -1;
    private bool initialized;

    private Vector3[] basePos;
    private Quaternion[] baseRot;

    private bool swinging;
    private float swingStart;
    private float lastSwingTime = -999f;
    private int comboIndex;
    private SwingPose current;
    private float hitTime, windupTime, recoverTime, totalTime;

    private float equipStart = -999f;
    private float idleWeight = 1f;

    private void Reset() { swings = DefaultSwings(); }

    private void Awake() { EnsureInit(); }

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        if (melee == null) melee = GetComponentInParent<MeleeLogic>();
        if (targets == null || targets.Length == 0) targets = new[] { transform };
        if (swings == null || swings.Length == 0) swings = DefaultSwings();

        basePos = new Vector3[targets.Length];
        baseRot = new Quaternion[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            basePos[i] = targets[i].localPosition;
            baseRot[i] = targets[i].localRotation;
        }
    }

    private void OnEnable()
    {
        EnsureInit();
        swinging = false;
        equipStart = playEquipAnimation ? Time.time : -999f;
    }

    private void OnDisable()
    {
        if (targets == null || basePos == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            targets[i].localPosition = basePos[i];
            targets[i].localRotation = baseRot[i];
        }
    }

    /// <summary>Lo llama MeleeLogic cuando arranca un ataque.</summary>
    [ContextMenu("Probar swing (Play Mode)")]
    public void PlaySwing()
    {
        EnsureInit();
        if (swings == null || swings.Length == 0) return;

        if (Time.time - lastSwingTime > comboResetTime) comboIndex = 0;
        current = swings[comboIndex % swings.Length];
        comboIndex++;
        lastSwingTime = Time.time;

        float hitDelay = melee != null ? melee.hitDelay : fallbackHitDelay;
        float cooldown = melee != null ? melee.attackCooldown : fallbackCooldown;

        hitTime = Mathf.Max(hitDelay, 0.06f);
        windupTime = hitTime * windupPortion;
        recoverTime = Mathf.Max(cooldown - hitTime - holdTime, 0.1f);
        totalTime = hitTime + holdTime + recoverTime;

        swingStart = Time.time;
        swinging = true;
    }

    private void LateUpdate()
    {
        Evaluate();

        if (targets == null || basePos == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            targets[i].localPosition = basePos[i] + posOffset;
            // Delta expresado en los ejes del padre (igual que lo interpreta WorldWeaponSway)
            targets[i].localRotation = rotOffset * baseRot[i];
        }
    }

    /// <summary>Calcula los offsets una sola vez por frame (sin depender del orden de ejecución).</summary>
    private void Evaluate()
    {
        if (evalFrame == Time.frameCount) return;
        evalFrame = Time.frameCount;
        EnsureInit();

        Vector3 pos = Vector3.zero;
        Vector3 euler = Vector3.zero;

        if (swinging)
        {
            EvaluateSwing(out pos, out euler);
            idleWeight = 0f;
        }
        else
        {
            idleWeight = Mathf.MoveTowards(idleWeight, 1f, Time.deltaTime * 4f);
        }

        if (playEquipAnimation && equipDuration > 0f)
        {
            float e = Mathf.Clamp01((Time.time - equipStart) / equipDuration);
            if (e < 1f)
            {
                float k = 1f - Smooth(e);
                pos += equipPosition * k;
                euler += equipEuler * k;
            }
        }

        if (idleWeight > 0f)
        {
            float s = Mathf.Sin(Time.time * idleSpeed * Mathf.PI * 2f);
            pos += idleAmplitude * (s * idleWeight);
            euler += idleEulerAmplitude * (s * idleWeight);
        }

        posOffset = pos;
        rotOffset = Quaternion.Euler(euler);
    }

    private void EvaluateSwing(out Vector3 pos, out Vector3 euler)
    {
        float t = Time.time - swingStart;
        SwingPose s = current;

        if (t < windupTime)
        {
            float k = EaseOut(t / Mathf.Max(windupTime, 0.0001f));
            pos = s.windupPosition * k;
            euler = s.windupEuler * k;
        }
        else if (t < hitTime)
        {
            float k = EaseIn((t - windupTime) / Mathf.Max(hitTime - windupTime, 0.0001f));
            pos = Vector3.Lerp(s.windupPosition, s.strikePosition, k);
            euler = Vector3.Lerp(s.windupEuler, s.strikeEuler, k);
        }
        else if (t < hitTime + holdTime)
        {
            pos = s.strikePosition;
            euler = s.strikeEuler;
        }
        else if (t < totalTime)
        {
            float k = Smooth((t - hitTime - holdTime) / Mathf.Max(recoverTime, 0.0001f));
            pos = Vector3.Lerp(s.strikePosition, Vector3.zero, k);
            euler = Vector3.Lerp(s.strikeEuler, Vector3.zero, k);
        }
        else
        {
            swinging = false;
            pos = Vector3.zero;
            euler = Vector3.zero;
        }
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    private static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t * t; }

    private static SwingPose[] DefaultSwings()
    {
        return new[]
        {
            new SwingPose
            {
                name = "Tajo derecha-izquierda",
                windupPosition = new Vector3(0.12f, 0.04f, -0.06f),
                windupEuler    = new Vector3(10f, 25f, -40f),
                strikePosition = new Vector3(-0.15f, -0.05f, 0.15f),
                strikeEuler    = new Vector3(-10f, -45f, 35f)
            },
            new SwingPose
            {
                name = "Tajo izquierda-derecha",
                windupPosition = new Vector3(-0.12f, 0.04f, -0.06f),
                windupEuler    = new Vector3(10f, -25f, 40f),
                strikePosition = new Vector3(0.15f, -0.05f, 0.15f),
                strikeEuler    = new Vector3(-10f, 45f, -35f)
            },
            new SwingPose
            {
                name = "Estocada",
                windupPosition = new Vector3(0f, 0f, -0.15f),
                windupEuler    = new Vector3(5f, 0f, 0f),
                strikePosition = new Vector3(0f, 0f, 0.28f),
                strikeEuler    = new Vector3(-5f, 0f, 0f)
            }
        };
    }
}
