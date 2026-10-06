using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PMovement))]
public class Character : NetworkBehaviour
{
    [Header("Spawn")]
    [SerializeField] private float spawnSpacing = 2f;

    [Header("HUD")]
    [Tooltip("Canvas hijo con la barra de vida propia y la de los compañeros. Solo se activa en el personaje del jugador local.")]
    [SerializeField] private GameObject localHud;

    private PMovement movement;
    private PlayerHealth health;
    private WeaponSwitcher weaponSwitcher;
    [SerializeField] private float interactRange = 2f;
    [SerializeField] private LayerMask pickupLayer;

    private readonly List<Behaviour> disabledSceneCameras = new List<Behaviour>();
    private bool placedByServer;


    private void Awake()
    {
        movement = GetComponent<PMovement>();
        health = GetComponent<PlayerHealth>();

        weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
    }

    public override void OnNetworkSpawn()
    {
        // Solo el dueño lee input y renderiza con la camara de su personaje.
        // Si los personajes remotos tuvieran PlayerInput y Camera activos, le robarian el teclado
        // y la pantalla al jugador local.
        SetLocalOnlyComponents(IsOwner);

        if (IsOwner)
        {
            DisableSceneCameras();
            StartCoroutine(MoveToSpawnPoint());
        }
    }

    public override void OnNetworkDespawn()
    {
        RestoreSceneCameras();
    }

    public override void OnDestroy()
    {
        RestoreSceneCameras();
        base.OnDestroy();
    }


    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsOwner || IsDead()) return;

        movement.SetMoveInput(context.ReadValue<Vector2>());
    }
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
        
            movement.SetSprint(true);
        }
        else if (context.canceled)
        {
            
            movement.SetSprint(false);
        }
        
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner || IsDead()) return;


        if (context.performed)
        {
            movement.TryJump();
        }
    }

    private bool IsDead()
    {
        return health != null && health.IsDead;
    }
    
    public void OnSelectWeapon1(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.SelectWeapon(-1);
    }

    public void OnSelectWeapon2(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.SelectWeapon(0);
    }

    public void OnSelectWeapon3(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.SelectWeapon(1);
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.Shoot(context);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (IsOwner && context.performed)
            weaponSwitcher.Reload(context);
    }

    public void OnInteraction(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (!context.performed) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, pickupLayer);
        WeaponPickup nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            WeaponPickup pickup = hit.GetComponent<WeaponPickup>();
            if (pickup == null) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = pickup;
            }
        }

        if (nearest != null)
        {
            weaponSwitcher?.RequestPickupWeaponServerRpc(
                (int)nearest.weaponType,
                nearest.GetComponent<NetworkObject>().NetworkObjectId
            );
        }
    }

    private void SetLocalOnlyComponents(bool isLocal)
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            // Apagar y volver a prender hace que el PlayerInput local vuelva a tomar el teclado y el mouse.
            playerInput.enabled = false;
            playerInput.enabled = isLocal;
        }

        foreach (Camera cam in GetComponentsInChildren<Camera>(true))
        {
            cam.enabled = isLocal;
        }

        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = isLocal;
        }

        // Cada jugador tiene su copia del HUD en el prefab; si no se apagaran las de los demas,
        // las barras quedarian superpuestas en pantalla.
        if (localHud != null)
        {
            localHud.SetActive(isLocal);
        }
    }

    // Camaras de la escena (por ejemplo la del menu): se apagan mientras el jugador local esta en partida.
    private void DisableSceneCameras()
    {
        foreach (Camera cam in FindObjectsByType<Camera>())
        {
            if (!cam.enabled || cam.GetComponentInParent<NetworkObject>() != null) continue;

            cam.enabled = false;
            disabledSceneCameras.Add(cam);

            AudioListener listener = cam.GetComponent<AudioListener>();
            if (listener != null && listener.enabled)
            {
                listener.enabled = false;
                disabledSceneCameras.Add(listener);
            }
        }
    }

    private void RestoreSceneCameras()
    {
        foreach (Behaviour component in disabledSceneCameras)
        {
            if (component != null)
            {
                component.enabled = true;
            }
        }
        disabledSceneCameras.Clear();
    }

    // Solo el server. Ubica al jugador numero 'playerIndex' al lado del punto de spawn, separado spawnSpacing del anterior.
    // La posicion la calcula el server y se la manda al dueño: con NetworkTransform de autoridad del dueño,
    // el cliente es quien tiene que teletransportarse, y su transform local puede no estar sincronizado todavia.
    public void PlaceAtSpawn(Vector3 spawnPosition, Quaternion spawnRotation, int playerIndex)
    {
        if (!IsServer) return;

        Vector3 position = spawnPosition + spawnRotation * Vector3.right * spawnSpacing * playerIndex;
        PlaceAtSpawnRpc(position, spawnRotation);
    }

    [Rpc(SendTo.Owner)]
    private void PlaceAtSpawnRpc(Vector3 position, Quaternion rotation)
    {
        placedByServer = true;
        StartCoroutine(TeleportNextFrame(position, rotation));
    }

    // Jugadores que no creo GameSessionManager (por ejemplo con AutoHost en escenas de prueba):
    // se separan por su id para que no aparezcan uno adentro del otro.
    // Se espera un frame para que el NetworkTransform ya este inicializado.
    private IEnumerator MoveToSpawnPoint()
    {
        yield return null;

        if (placedByServer) yield break;

        TeleportTo(transform.position + Vector3.right * spawnSpacing * OwnerClientId, transform.rotation);
    }

    private IEnumerator TeleportNextFrame(Vector3 position, Quaternion rotation)
    {
        yield return null;
        TeleportTo(position, rotation);
    }

    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null && networkTransform.CanCommitToTransform)
        {
            networkTransform.Teleport(position, rotation, transform.localScale);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        if (controller != null) controller.enabled = true;
    }
}
