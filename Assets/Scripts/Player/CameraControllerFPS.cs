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
        if (IsSpawned && !IsOwner)
            return;

        // Muerto: SpectatorMode ubica la cámara. Si la reubicamos acá, vuelve al cuerpo.
        if (OwnerIsDead())
            return;

        // CameraBob corre en Update y, al caminar, devuelve la cámara a los ojos.
        if (thirdPerson)
            PlaceThirdPersonCamera();

        ApplyCameraVisibility();
    }

    // OwnBody queda fuera de la Main Camera y el arma del cuerpo solo tira sombra. En tercera persona se invierte eso.
    private void ApplyView()
    {
        if (thirdPerson)
            PlaceThirdPersonCamera();
        else
            transform.localPosition = firstPersonLocalPosition;

        if (weaponSwitcher != null && (!viewApplied || thirdPerson != lastThirdPerson))
        {
            weaponSwitcher.SetThirdPersonView(thirdPerson);
            lastThirdPerson = thirdPerson;
            viewApplied = true;
        }
    }

    private void PlaceThirdPersonCamera()
    {
        Vector3 shoulder = thirdPersonOffset + new Vector3(0f, 0f, -thirdPersonDistance);
        Vector3 desiredLocal = firstPersonLocalPosition + transform.localRotation * shoulder;
        Vector3 origin = transform.parent != null
            ? transform.parent.TransformPoint(firstPersonLocalPosition)
            : transform.position;
        Vector3 desiredWorld = transform.parent != null
            ? transform.parent.TransformPoint(desiredLocal)
            : desiredLocal;
        Vector3 pulled = PullInFrontOfWalls(origin, desiredWorld);
        transform.localPosition = transform.parent != null
            ? transform.parent.InverseTransformPoint(pulled)
            : pulled;
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

    private Vector3 PullInFrontOfWalls(Vector3 origin, Vector3 desired)
    {
        Vector3 delta = desired - origin;
        float distance = delta.magnitude;
        if (distance < 0.05f)
            return desired;

        Vector3 direction = delta / distance;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = distance;
        Transform root = transform.root;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == root || hit.transform.IsChildOf(root))
                continue;
            if (hit.distance < closest)
                closest = hit.distance;
        }

        if (closest >= distance)
            return desired;

        closest = Mathf.Max(0.2f, closest - 0.15f);
        return origin + direction * closest;
    }
}