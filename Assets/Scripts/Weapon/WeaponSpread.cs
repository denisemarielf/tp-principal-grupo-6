using UnityEngine;

/// <summary>
/// Calcula la dispersión actual del arma (en grados, semiángulo del cono).
/// Solo corre en el dueño; el valor se manda al servidor con cada disparo
/// y el servidor lo valida contra MaxSpread.
/// Poné este componente en el mismo GameObject que WeaponLogic.
/// </summary>
public class WeaponSpread : MonoBehaviour
{
    [Header("Base (grados, semiángulo del cono)")]
    [SerializeField] private float baseSpread = 0.4f;
    [SerializeField] private float maxSpread = 6f;

    [Header("Movimiento")]
    [Tooltip("Velocidad horizontal a la que se aplica el máximo de penalización.")]
    [SerializeField] private float speedReference = 6f;
    [SerializeField] private float moveSpreadAdd = 1.5f;
    [SerializeField] private float airSpreadAdd = 3f;

    [Header("Multiplicadores de postura")]
    [SerializeField] private float aimMultiplier = 0.4f;
    [SerializeField] private float crouchMultiplier = 0.7f;

    [Header("Bloom (acumulación por disparo)")]
    [SerializeField] private float bloomPerShot = 0.6f;
    [SerializeField] private float bloomMax = 4f;
    [SerializeField] private float bloomRecoverDelay = 0.15f;
    [SerializeField] private float bloomRecoverRate = 6f; // grados por segundo

    [Header("Suavizado")]
    [SerializeField] private float stateSmoothTime = 0.08f;

    public float MaxSpread => maxSpread;
    /// <summary>Dispersión actual en grados. Útil también para agrandar la mira.</summary>
    public float CurrentSpread { get; private set; }

    // Estado que setea el resto del juego (ADS, agachado)
    public bool IsAiming { get; set; }
    public bool IsCrouching { get; set; }

    private Transform root;
    private CharacterController controller;
    private Vector3 lastPos;
    private float speed;
    private float stateSpread, stateVel;
    private float bloom;
    private float lastShotTime = -999f;

    private void Awake()
    {
        root = transform.root;
        controller = GetComponentInParent<CharacterController>();
        lastPos = root.position;
        CurrentSpread = baseSpread;
        stateSpread = baseSpread;
    }

    private void OnEnable()
    {
        if (root != null) lastPos = root.position;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Velocidad horizontal por delta de posición (sirve con cualquier controller).
        Vector3 delta = root.position - lastPos;
        lastPos = root.position;
        delta.y = 0f;
        float instSpeed = delta.magnitude / dt;
        if (instSpeed > speedReference * 3f) instSpeed = 0f; // teletransporte/spawn: ignorar
        speed = Mathf.Lerp(speed, instSpeed, 1f - Mathf.Exp(-15f * dt));

        float moveFactor = Mathf.Clamp01(speed / Mathf.Max(0.01f, speedReference));
        bool airborne = controller != null && !controller.isGrounded;

        float target = baseSpread + moveFactor * moveSpreadAdd + (airborne ? airSpreadAdd : 0f);
        if (IsAiming) target *= aimMultiplier;
        if (IsCrouching) target *= crouchMultiplier;

        stateSpread = Mathf.SmoothDamp(stateSpread, target, ref stateVel, stateSmoothTime);

        // Bloom: se recupera después de una pausa sin disparar
        if (Time.time - lastShotTime > bloomRecoverDelay)
            bloom = Mathf.Max(0f, bloom - bloomRecoverRate * dt);

        float bloomScale = IsAiming ? aimMultiplier : 1f;
        CurrentSpread = Mathf.Min(stateSpread + bloom * bloomScale, maxSpread);
    }

    /// <summary>Llamar justo DESPUÉS de leer CurrentSpread para el disparo.</summary>
    public void RegisterShot()
    {
        bloom = Mathf.Min(bloom + bloomPerShot, bloomMax);
        lastShotTime = Time.time;
    }

    /// <summary>Dirección aleatoria dentro de un cono (distribución uniforme en el disco).</summary>
    public static Vector3 ApplySpread(Vector3 dir, float halfAngleDeg, System.Random rng)
    {
        if (halfAngleDeg <= 0.0001f) return dir;

        float a = (float)(rng.NextDouble() * 2.0 * Mathf.PI);
        float r = Mathf.Sqrt((float)rng.NextDouble()) * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);

        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(right, dir).normalized;

        return (dir + right * (Mathf.Cos(a) * r) + up * (Mathf.Sin(a) * r)).normalized;
    }
}
