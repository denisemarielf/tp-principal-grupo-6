using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// Modo cámara libre para grabar el mapa (tecla L).
// Congela solo zombies/IA (el tiempo del juego sigue: helicóptero, etc.).
public class VideoFreeCamera : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float boostMultiplier = 3f;
    [Tooltip("Multiplicador del fog al entrar en modo video (0.15 = mucho más claro).")]
    [SerializeField] [Range(0f, 1f)] private float videoFogDensityScale = 0.15f;

    private const float MouseScale = 0.0167f;
    private const float MaxDeltaPerFrame = 150f;

    private CameraControllerFPS fps;
    private CameraBob bob;
    private SpectatorMode spectator;
    private PlayerInput playerInput;
    private Camera viewCamera;
    private Camera[] weaponCameras;

    private Transform savedParent;
    private Vector3 savedLocalPos;
    private Quaternion savedLocalRot;
    private float savedFov;
    private int savedCullingMask;
    private bool savedFog;
    private float savedFogDensity;
    private float savedFogStart;
    private float savedFogEnd;
    private bool savedFpsEnabled;
    private bool savedBobEnabled;
    private bool savedSpectatorEnabled;
    private bool savedPlayerInputEnabled;
    private readonly List<bool> savedWeaponCamEnabled = new List<bool>();

    private float yaw;
    private float pitch;
    private float sensitivity = 2f;
    private int skipMouseFrames;

    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private readonly List<bool> canvasWasEnabled = new List<bool>();

    private readonly List<Behaviour> frozenBehaviours = new List<Behaviour>();
    private readonly List<NavMeshAgent> frozenAgents = new List<NavMeshAgent>();
    private readonly List<bool> agentWasEnabled = new List<bool>();
    private readonly List<Animator> frozenAnimators = new List<Animator>();
    private readonly List<float> animatorSpeeds = new List<float>();

    private void Awake()
    {
        fps = GetComponent<CameraControllerFPS>();
        bob = GetComponent<CameraBob>();
        spectator = GetComponent<SpectatorMode>();
        viewCamera = GetComponent<Camera>();
        playerInput = GetComponentInParent<PlayerInput>();

        weaponCameras = transform.root.GetComponentsInChildren<Camera>(true);
    }

    private void OnEnable()
    {
        ApplySavedSensitivity();
        GameOptions.Changed += ApplySavedSensitivity;
    }

    private void OnDisable()
    {
        GameOptions.Changed -= ApplySavedSensitivity;
        if (IsActive)
            ExitMode();
    }

    private void OnDestroy()
    {
        if (IsActive)
            ExitMode();
    }

    private void ApplySavedSensitivity()
    {
        sensitivity = GameOptions.Sensitivity;
    }

    private bool IsLocalOwner()
    {
        if (fps != null && fps.IsSpawned)
            return fps.IsOwner;

        NetworkBehaviour net = GetComponentInParent<NetworkBehaviour>();
        if (net != null && net.IsSpawned)
            return net.IsOwner;

        return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
    }

    private void Update()
    {
        if (!IsLocalOwner() || Keyboard.current == null)
            return;

        if (!PauseMenuUI.IsOpen && Keyboard.current.lKey.wasPressedThisFrame)
        {
            if (IsActive)
                ExitMode();
            else
                EnterMode();
            return;
        }

        if (!IsActive || PauseMenuUI.IsOpen)
            return;

        HandleLook();
        HandleMove();
    }

    private void EnterMode()
    {
        if (IsActive)
            return;

        IsActive = true;

        savedParent = transform.parent;
        savedLocalPos = transform.localPosition;
        savedLocalRot = transform.localRotation;
        savedFov = viewCamera != null ? viewCamera.fieldOfView : 60f;
        savedCullingMask = viewCamera != null ? viewCamera.cullingMask : ~0;

        savedFog = RenderSettings.fog;
        savedFogDensity = RenderSettings.fogDensity;
        savedFogStart = RenderSettings.fogStartDistance;
        savedFogEnd = RenderSettings.fogEndDistance;

        savedFpsEnabled = fps != null && fps.enabled;
        savedBobEnabled = bob != null && bob.enabled;
        savedSpectatorEnabled = spectator != null && spectator.enabled;
        savedPlayerInputEnabled = playerInput != null && playerInput.enabled;

        savedWeaponCamEnabled.Clear();
        if (weaponCameras != null)
        {
            for (int i = 0; i < weaponCameras.Length; i++)
            {
                Camera cam = weaponCameras[i];
                savedWeaponCamEnabled.Add(cam != null && cam.enabled);
                if (cam != null && cam != viewCamera)
                    cam.enabled = false;
            }
        }

        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);

        FreezeZombies();
        ApplyVideoFog();
        ShowOwnBodyOnCamera();

        if (fps != null)
            fps.enabled = false;
        if (bob != null)
            bob.enabled = false;
        if (spectator != null)
            spectator.enabled = false;
        if (playerInput != null)
            playerInput.enabled = false;

        // Solo se desparenta la cámara: el cuerpo queda quieto en el mapa.
        transform.SetParent(null, true);

        HideGameplayHud();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        skipMouseFrames = 2;
    }

    private void ExitMode()
    {
        if (!IsActive)
            return;

        IsActive = false;

        UnfreezeZombies();
        RestoreVideoFog();

        if (savedParent != null)
            transform.SetParent(savedParent, false);
        transform.localPosition = savedLocalPos;
        transform.localRotation = savedLocalRot;

        if (viewCamera != null)
        {
            viewCamera.fieldOfView = savedFov;
            viewCamera.cullingMask = savedCullingMask;
        }

        if (weaponCameras != null)
        {
            for (int i = 0; i < weaponCameras.Length; i++)
            {
                Camera cam = weaponCameras[i];
                if (cam == null || cam == viewCamera)
                    continue;
                bool wasEnabled = i < savedWeaponCamEnabled.Count && savedWeaponCamEnabled[i];
                cam.enabled = wasEnabled;
            }
        }

        if (fps != null)
        {
            fps.enabled = savedFpsEnabled;
            fps.ResetCamera();
        }
        if (bob != null)
            bob.enabled = savedBobEnabled;
        if (spectator != null)
            spectator.enabled = savedSpectatorEnabled;
        if (playerInput != null)
            playerInput.enabled = savedPlayerInputEnabled;

        RestoreGameplayHud();

        if (!PauseMenuUI.IsOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ApplyVideoFog()
    {
        if (!RenderSettings.fog)
            return;

        RenderSettings.fogDensity = savedFogDensity * videoFogDensityScale;
        // Por si alguna escena usa fog lineal: estira el alcance.
        float end = savedFogEnd > savedFogStart ? savedFogEnd : 300f;
        RenderSettings.fogEndDistance = Mathf.Lerp(end, end * 6f, 1f - videoFogDensityScale);
    }

    private void RestoreVideoFog()
    {
        RenderSettings.fog = savedFog;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.fogStartDistance = savedFogStart;
        RenderSettings.fogEndDistance = savedFogEnd;
    }

    private void ShowOwnBodyOnCamera()
    {
        if (viewCamera == null)
            return;

        int ownBodyLayer = LayerMask.NameToLayer("OwnBody");
        if (ownBodyLayer < 0)
            return;

        viewCamera.cullingMask |= 1 << ownBodyLayer;
    }

    private void FreezeZombies()
    {
        UnfreezeZombies();

        FreezeBehaviourList(FindObjectsByType<Ai>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<EnemyCombat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<MiniBossBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<EnemyAnimation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<MiniBossAnimation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<AIDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<SurvivalWaveDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<MiniBossSpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<EnemySceneSetup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeBehaviourList(FindObjectsByType<EnemySpawnerWaves>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

        foreach (EnemyHealth health in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (health == null)
                continue;

            NavMeshAgent agent = health.GetComponent<NavMeshAgent>();
            if (agent != null && !frozenAgents.Contains(agent))
            {
                frozenAgents.Add(agent);
                agentWasEnabled.Add(agent.enabled);
                if (agent.enabled)
                    agent.isStopped = true;
                agent.enabled = false;
            }

            foreach (Animator animator in health.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || frozenAnimators.Contains(animator))
                    continue;
                frozenAnimators.Add(animator);
                animatorSpeeds.Add(animator.speed);
                animator.speed = 0f;
            }
        }
    }

    private void FreezeBehaviourList<T>(T[] behaviours) where T : Behaviour
    {
        if (behaviours == null)
            return;

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled || frozenBehaviours.Contains(behaviour))
                continue;
            frozenBehaviours.Add(behaviour);
            behaviour.enabled = false;
        }
    }

    private void UnfreezeZombies()
    {
        for (int i = 0; i < frozenBehaviours.Count; i++)
        {
            Behaviour behaviour = frozenBehaviours[i];
            if (behaviour != null)
                behaviour.enabled = true;
        }
        frozenBehaviours.Clear();

        for (int i = 0; i < frozenAgents.Count; i++)
        {
            NavMeshAgent agent = frozenAgents[i];
            if (agent == null)
                continue;
            bool wasEnabled = i < agentWasEnabled.Count && agentWasEnabled[i];
            agent.enabled = wasEnabled;
            if (wasEnabled)
                agent.isStopped = false;
        }
        frozenAgents.Clear();
        agentWasEnabled.Clear();

        for (int i = 0; i < frozenAnimators.Count; i++)
        {
            Animator animator = frozenAnimators[i];
            if (animator != null)
                animator.speed = i < animatorSpeeds.Count ? animatorSpeeds[i] : 1f;
        }
        frozenAnimators.Clear();
        animatorSpeeds.Clear();
    }

    private void HandleLook()
    {
        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
            return;

        if (skipMouseFrames > 0)
        {
            skipMouseFrames--;
            return;
        }

        Vector2 mouseDelta = Vector2.ClampMagnitude(Mouse.current.delta.ReadValue(), MaxDeltaPerFrame);
        yaw += mouseDelta.x * sensitivity * MouseScale;
        pitch -= mouseDelta.y * sensitivity * MouseScale;
        pitch = Mathf.Clamp(pitch, -89.5f, 89.5f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMove()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
            return;

        Vector3 input = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) input += Vector3.forward;
        if (Keyboard.current.sKey.isPressed) input += Vector3.back;
        if (Keyboard.current.aKey.isPressed) input += Vector3.left;
        if (Keyboard.current.dKey.isPressed) input += Vector3.right;
        if (Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed) input += Vector3.up;
        if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.qKey.isPressed) input += Vector3.down;

        if (input.sqrMagnitude < 0.0001f)
            return;

        float speed = moveSpeed;
        if (Keyboard.current.leftShiftKey.isPressed)
            speed *= boostMultiplier;

        Vector3 world = transform.TransformDirection(input.normalized);
        transform.position += world * (speed * dt);
    }

    private void HideGameplayHud()
    {
        RestoreGameplayHud();

        foreach (ObjectiveHUD hud in FindObjectsByType<ObjectiveHUD>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            HideCanvas(hud != null ? hud.GetComponentInParent<Canvas>() : null);

        foreach (CrosshairController crosshair in FindObjectsByType<CrosshairController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            HideCanvas(crosshair != null ? crosshair.GetComponentInParent<Canvas>() : null);
    }

    private void HideCanvas(Canvas canvas)
    {
        if (canvas == null || !canvas.enabled)
            return;
        if (canvas.GetComponentInParent<PauseMenuUI>() != null)
            return;
        if (hiddenCanvases.Contains(canvas))
            return;

        hiddenCanvases.Add(canvas);
        canvasWasEnabled.Add(true);
        canvas.enabled = false;
    }

    private void RestoreGameplayHud()
    {
        for (int i = 0; i < hiddenCanvases.Count; i++)
        {
            Canvas canvas = hiddenCanvases[i];
            if (canvas != null)
                canvas.enabled = i < canvasWasEnabled.Count && canvasWasEnabled[i];
        }
        hiddenCanvases.Clear();
        canvasWasEnabled.Clear();
    }

    private static float NormalizePitch(float x)
    {
        if (x > 180f)
            x -= 360f;
        return Mathf.Clamp(x, -89.5f, 89.5f);
    }
}
