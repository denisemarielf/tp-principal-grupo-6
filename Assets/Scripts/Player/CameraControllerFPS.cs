using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CameraControllerFPS : NetworkBehaviour
{
    public float sensitivity = 2f;
    [SerializeField] private Transform weaponPivot; // el objeto que agrupa WeaponCamera + las armas
    [SerializeField] private WeaponSwitcher weaponSwitcher; // asignalo a mano: vive en un hermano de esta camara (Player), no en un ancestro
    [SerializeField] private float thirdPersonDistance = 3.2f;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.45f, 0.35f, 0f);

    [Header("Tercera persona - dinámica")]
    [SerializeField] private LayerMask cameraCollisionMask = ~0; // sacá Enemy/Player/OwnBody para que no empujen la cámara
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float thirdPersonNearClip = 0.1f;
    [SerializeField] private float aimDistance = 1.8f;
    [SerializeField] private Vector3 aimOffset = new Vector3(0.6f, 0.25f, 0f);
    [SerializeField] private float aimFovDelta = -12f;
    [SerializeField] private float movePullBack = 0.6f;
    [SerializeField] private float moveFovBoost = 7f;
    [SerializeField] private float referenceSpeed = 6f;   // velocidad "a tope" de tu jugador
    [SerializeField] private float strafeRoll = 2.5f;
    [SerializeField] private float releaseSmoothTime = 0.18f;

    [Header("Impacto al recibir daño")]
    [SerializeField] private float hitPitch = 3.5f;
    [SerializeField] private float hitYaw = 2f;
    [SerializeField] private float hitRoll = 3f;
    [SerializeField] private float hitSpring = 120f;
    [SerializeField] private float hitDamping = 14f;

    private float xRotation = 0f;
    private bool thirdPerson;

    public bool IsThirdPerson => thirdPerson;
    private bool lastThirdPerson;
    private bool viewApplied;
    private Vector3 firstPersonLocalPosition;
    private Camera viewCamera;
    private Camera[] weaponCameras;
    private int ownBodyLayer = -1;

    // Dinámica de tercera persona
    private float baseFov, baseNear;
    private int shoulderSide = 1;
    private float shoulderBlend = 1f, aimBlend, moveBlend, strafeSmoothed;
    private float currentDistance = -1f, distanceVelocity;
    private Vector3 lastBodyPos;
    private static readonly RaycastHit[] castHits = new RaycastHit[16];
    private static readonly Collider[] overlapBuf = new Collider[8];

    // Fix del mouse
    private int skipMouseFrames;
    private const float MouseScale = 0.0167f;      // equivale a lo que tenias a 60 FPS
    private const float MaxDeltaPerFrame = 150f;   // pixeles; descarta picos absurdos

    // Golpe de camara
    private Vector3 hitOffset;
    private Vector3 hitVelocity;
    private PlayerHealth ownerHealth;

    public NetworkVariable<float> networkPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private void OnEnable()
    {
        ApplySavedSensitivity();
        GameOptions.Changed += ApplySavedSensitivity;
    }

    private void OnDisable()
    {
        GameOptions.Changed -= ApplySavedSensitivity;
    }

    private void ApplySavedSensitivity()
    {
        sensitivity = GameOptions.Sensitivity;
    }

    private void Awake()
    {
        // Fallback por si no lo asignaste a mano: WeaponSwitcher es hermano de
        // esta camara (ambos hijos de Player), asi que GetComponentInParent NO
        // lo encuentra; buscamos desde la raiz en su lugar.
        if (weaponSwitcher == null)
        {
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>();
        }

        firstPersonLocalPosition = transform.localPosition;
        viewCamera = GetComponent<Camera>();
        if (viewCamera != null)
        {
            baseFov = viewCamera.fieldOfView;
            baseNear = viewCamera.nearClipPlane;
        }
        ownBodyLayer = LayerMask.NameToLayer("OwnBody");

        if (weaponPivot != null)
            weaponCameras = weaponPivot.GetComponentsInChildren<Camera>(true);
        else
        {
            Camera named = null;
            foreach (Camera cam in transform.root.GetComponentsInChildren<Camera>(true))
            {
                if (cam.gameObject.name == "WeaponCamera")
                {
                    named = cam;
                    break;
                }
            }
            weaponCameras = named != null ? new[] { named } : new Camera[0];
        }

        if (GetComponent<SpectatorMode>() == null)
            gameObject.AddComponent<SpectatorMode>();

        if (GetComponent<VideoFreeCamera>() == null)
            gameObject.AddComponent<VideoFreeCamera>();
    }

    private bool OwnerIsDead()
    {
        PlayerHealth health = GetComponentInParent<PlayerHealth>();
        return health != null && health.IsSpawned && health.IsDead && (!IsSpawned || IsOwner);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }

        if (IsOwner)
        {
            ownerHealth = GetComponentInParent<PlayerHealth>();
            if (ownerHealth != null)
                ownerHealth.DamageTaken += ApplyHitKick;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (ownerHealth != null)
            ownerHealth.DamageTaken -= ApplyHitKick;
        ownerHealth = null;
        base.OnNetworkDespawn();
    }

    void Start()
    {
        if ((!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening || IsOwner) && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && (!IsSpawned || IsOwner) && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }
    }

    // El cursor lo abre y lo cierra PauseMenuUI. Mientras esta libre, el mouse no mueve la camara.
    private void SetCursorLocked(bool locked)
    {
        // Con la pantalla de nivel completado o de partida terminada el cursor tiene que quedar libre para los botones.
        if (locked && EndScreenShown) return;

        // Al bloquear el cursor, ignoramos unos frames de mouse: ahi aparecen los saltos.
        if (locked && Cursor.lockState != CursorLockMode.Locked)
            skipMouseFrames = 3;

        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private static bool EndScreenShown =>
        GameOverUI.IsShown || (SafeZone.Current != null && SafeZone.Current.IsCompleted);

    public void ResetCamera()
    {
        xRotation = 0f;
        hitOffset = Vector3.zero;
        hitVelocity = Vector3.zero;
        currentDistance = -1f;
        distanceVelocity = 0f;
        transform.localRotation = Quaternion.identity;
        if (weaponPivot != null)
        {
            weaponPivot.localRotation = Quaternion.identity;
        }
        if ((!IsSpawned || IsOwner) && !PauseMenuUI.IsOpen)
        {
            SetCursorLocked(true);
        }
    }

    // Golpe de camara al recibir dano: la camara se sacude y vuelve sola.
    public void ApplyHitKick(float damage)
    {
        float strength = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(damage / 40f));
        hitOffset += new Vector3(
            -hitPitch * strength,                                           // cabecea hacia arriba
            UnityEngine.Random.Range(-hitYaw, hitYaw) * strength,
            UnityEngine.Random.Range(-hitRoll, hitRoll) * strength          // inclinacion lateral
        );
    }

    void Update()
    {
        if (VideoFreeCamera.IsActive)
            return;

        bool isLocal = !IsSpawned || IsOwner;
        Vector3 recoilOffset = Vector3.zero;

        if (isLocal && !OwnerIsDead())
        {
            if (!PauseMenuUI.IsOpen
                && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked)
            {
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    SetCursorLocked(true);
                }
            }

            if (!PauseMenuUI.IsOpen && Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                if (skipMouseFrames > 0)
                {
                    skipMouseFrames--;
                }
                else
                {
                    Vector2 mouseDelta = Vector2.ClampMagnitude(Mouse.current.delta.ReadValue(), MaxDeltaPerFrame);
                    float mouseX = mouseDelta.x * sensitivity * MouseScale;   // SIN Time.deltaTime
                    float mouseY = mouseDelta.y * sensitivity * MouseScale;

                    xRotation -= mouseY;
                    xRotation = Mathf.Clamp(xRotation, -90f, 90f);

                    if (transform.parent != null)
                    {
                        transform.parent.Rotate(Vector3.up * mouseX);
                    }

                    if (IsSpawned)
                    {
                        networkPitch.Value = xRotation;
                    }
                }
            }

            CameraRecoil currentRecoil = weaponSwitcher != null && weaponSwitcher.CurrentWeaponLogic != null
                ? weaponSwitcher.CurrentWeaponLogic.cameraRecoil
                : null;

            if (currentRecoil != null)
            {
                recoilOffset = currentRecoil.UpdateRecoil();
            }

            // Resorte amortiguado del golpe de camara.
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            hitVelocity += (-hitSpring * hitOffset - hitDamping * hitVelocity) * dt;
            hitOffset += hitVelocity * dt;

            // El recoil y el golpe se suman como offset LOCAL de la camara, encima del
            // pitch "limpio" del mouse (xRotation nunca se contamina, asi que
            // networkPitch sigue reflejando tu apuntado real).
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f)
                                    * Quaternion.Euler(recoilOffset)
                                    * Quaternion.Euler(hitOffset);

            if (!PauseMenuUI.IsOpen && !EndScreenShown && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
                thirdPerson = !thirdPerson;

            // Cambio de hombro (en tercera persona)
            if (thirdPerson && !PauseMenuUI.IsOpen && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                shoulderSide = -shoulderSide;

            ApplyView();
        }

        if (weaponPivot != null)
        {
            float pitchToApply = isLocal ? xRotation : (IsSpawned ? networkPitch.Value : xRotation);
            weaponPivot.localRotation = Quaternion.Euler(pitchToApply, 0f, 0f) * Quaternion.Euler(recoilOffset);
        }
    }

    private void LateUpdate()
    {
        if (VideoFreeCamera.IsActive)
            return;

        if (IsSpawned && !IsOwner)
            return;

        // Muerto: SpectatorMode ubica la cámara. Si la reubicamos acá, vuelve al cuerpo.
        if (OwnerIsDead())
            return;

        // CameraBob corre en Update y, al caminar, devuelve la cámara a los ojos.
        if (thirdPerson)
            UpdateThirdPersonCamera();

        ApplyCameraVisibility();
    }

    // OwnBody queda fuera de la Main Camera y el arma del cuerpo solo tira sombra. En tercera persona se invierte eso.
    private void ApplyView()
    {
        if (!thirdPerson)
            transform.localPosition = firstPersonLocalPosition;
        // En tercera persona la posición se calcula en LateUpdate (después de CameraBob).

        if (!viewApplied || thirdPerson != lastThirdPerson)
        {
            OnViewModeChanged();
            if (weaponSwitcher != null)
                weaponSwitcher.SetThirdPersonView(thirdPerson);
            lastThirdPerson = thirdPerson;
            viewApplied = true;
        }
    }

    private void OnViewModeChanged()
    {
        currentDistance = -1f;
        distanceVelocity = 0f;
        aimBlend = 0f;
        moveBlend = 0f;
        strafeSmoothed = 0f;
        lastBodyPos = transform.parent != null ? transform.parent.position : transform.position;

        if (viewCamera == null) return;
        viewCamera.nearClipPlane = thirdPerson ? thirdPersonNearClip : baseNear;
        if (!thirdPerson)
            viewCamera.fieldOfView = baseFov;
    }

    private void UpdateThirdPersonCamera()
    {
        float dt = Time.deltaTime;
        Transform body = transform.parent != null ? transform.parent : transform;

        // Velocidad horizontal del cuerpo, por diferencia de posición.
        Vector3 vel = dt > 0f ? (body.position - lastBodyPos) / dt : Vector3.zero;
        lastBodyPos = body.position;
        vel.y = 0f;
        if (vel.magnitude > referenceSpeed * 3f) vel = Vector3.zero; // respawn / teletransporte

        float k = 1f - Mathf.Exp(-8f * dt);
        moveBlend = Mathf.Lerp(moveBlend, Mathf.Clamp01(vel.magnitude / referenceSpeed), k);
        float strafe = Mathf.Clamp(Vector3.Dot(vel, body.right) / referenceSpeed, -1f, 1f);
        strafeSmoothed = Mathf.Lerp(strafeSmoothed, strafe, k);

        bool aiming = !PauseMenuUI.IsOpen && Mouse.current != null && Mouse.current.rightButton.isPressed;
        aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, dt * 6f);
        shoulderBlend = Mathf.MoveTowards(shoulderBlend, shoulderSide, dt * 6f);

        Vector3 offset = Vector3.Lerp(thirdPersonOffset, aimOffset, aimBlend);
        offset.x *= shoulderBlend;
        float distance = Mathf.Lerp(thirdPersonDistance + moveBlend * movePullBack, aimDistance, aimBlend);

        Vector3 desiredLocal = firstPersonLocalPosition
                             + transform.localRotation * (offset + new Vector3(0f, 0f, -distance));

        Vector3 origin = transform.parent != null
            ? transform.parent.TransformPoint(firstPersonLocalPosition)
            : transform.position;
        Vector3 desiredWorld = transform.parent != null
            ? transform.parent.TransformPoint(desiredLocal)
            : desiredLocal;

        Vector3 delta = desiredWorld - origin;
        float fullDist = delta.magnitude;
        Vector3 dir = fullDist > 0.001f ? delta / fullDist : transform.forward;
        float allowed = AllowedDistance(origin, dir, fullDist);

        // Pared: la cámara se acerca al instante. Sin pared: vuelve suavemente.
        if (currentDistance < 0f)
            currentDistance = allowed;
        else if (allowed < currentDistance)
        {
            currentDistance = allowed;
            distanceVelocity = 0f;
        }
        else
            currentDistance = Mathf.SmoothDamp(currentDistance, allowed, ref distanceVelocity, releaseSmoothTime);

        Vector3 pulled = origin + dir * currentDistance;
        transform.localPosition = transform.parent != null
            ? transform.parent.InverseTransformPoint(pulled)
            : pulled;

        // Inclinación al hacer strafe (Update reescribe localRotation cada frame, no se acumula).
        transform.localRotation *= Quaternion.Euler(0f, 0f, -strafeSmoothed * strafeRoll);

        if (viewCamera != null)
        {
            float targetFov = baseFov + moveBlend * moveFovBoost + aimBlend * aimFovDelta;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-10f * dt));
        }
    }

    private float AllowedDistance(Vector3 origin, Vector3 dir, float maxDist)
    {
        Transform root = transform.root;

        // SphereCast no detecta colliders que ya contienen el origen: lo chequeo aparte.
        int overlaps = Physics.OverlapSphereNonAlloc(origin, cameraRadius * 0.5f, overlapBuf,
            cameraCollisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps; i++)
        {
            if (!overlapBuf[i].transform.IsChildOf(root))
                return 0f;
        }

        int count = Physics.SphereCastNonAlloc(origin, cameraRadius, dir, castHits, maxDist,
            cameraCollisionMask, QueryTriggerInteraction.Ignore);
        float closest = maxDist;
        for (int i = 0; i < count; i++)
        {
            if (castHits[i].transform.IsChildOf(root))
                continue;
            if (castHits[i].distance < closest)
                closest = castHits[i].distance;
        }
        return closest;
    }

    // Después del refresh del modelo, que vuelve a prender todas las cámaras del dueño.
    private void ApplyCameraVisibility()
    {
        if (viewCamera != null && ownBodyLayer >= 0)
        {
            int bit = 1 << ownBodyLayer;
            if (thirdPerson)
                viewCamera.cullingMask |= bit;
            else
                viewCamera.cullingMask &= ~bit;
        }

        if (weaponCameras == null)
            return;

        foreach (Camera cam in weaponCameras)
        {
            if (cam != null && cam != viewCamera && cam.gameObject.name == "WeaponCamera")
                cam.enabled = !thirdPerson;
        }
    }
}
