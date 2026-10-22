using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

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
    private PlayerInventory inventory;
    

    [Header("Nombre del jugador")] //nuevo
    [SerializeField] private TextMeshPro playerNameText;

    // Último nombre recibido (el servidor lo usa para responder a los demás)
    private string nombreJugador = ""; //nuevo

    // Nombre con el que se identifica al jugador (ej. en el guardado de partida, US 3.6).
    public string PlayerName => string.IsNullOrEmpty(nombreJugador) ? "Jugador " + (OwnerClientId + 1) : nombreJugador;


    private void Awake()
    {
        movement = GetComponent<PMovement>();
        health = GetComponent<PlayerHealth>();
        inventory = GetComponent<PlayerInventory>();
        weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
    }

    //Para que el texto se vea bien desde todos los angulos
    private void LateUpdate()
    {
        if (playerNameText == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        playerNameText.transform.rotation = cam.transform.rotation;
    }

    public override void OnNetworkSpawn()
    {
        SetLocalOnlyComponents(IsOwner);

        if (IsOwner)
        {
            string nombre = NombrePlayer.NombreJugador;

            if (string.IsNullOrWhiteSpace(nombre))
                nombre = "Jugador " + (OwnerClientId + 1);

            EnviarNombreAlServidorRpc(nombre);

            DisableSceneCameras();
            StartCoroutine(MoveToSpawnPoint());
            StartCoroutine(ApplyCarryOverWhenReady());
        }
        else if (!IsServer)
        {
            // Personaje de otro jugador visto desde un cliente:
            // pedimos su nombre por si el servidor ya lo tenía.
            PedirNombreRpc();
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

    //metodos nuevos 
    // El dueño manda su nombre al servidor.
    [Rpc(SendTo.Server, RequireOwnership = true)]
    private void EnviarNombreAlServidorRpc(string nombre)
    {
        string limpio = (nombre ?? "").Trim();

        if (limpio.Length > 16)
            limpio = limpio.Substring(0, 16);

        // Nunca queda vacío
        if (string.IsNullOrWhiteSpace(limpio))
            limpio = "Jugador " + (OwnerClientId + 1);

        nombreJugador = limpio;
        AplicarNombreRpc(nombreJugador);
    }

    // Un cliente que ve este personaje por primera vez pide el nombre.
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void PedirNombreRpc()
    {
        if (!string.IsNullOrEmpty(nombreJugador))
            AplicarNombreRpc(nombreJugador);
    }

    // El servidor le avisa a todos el nombre de este personaje.
    [Rpc(SendTo.Everyone)]
    private void AplicarNombreRpc(string nombre)
    {
        nombreJugador = nombre;

        if (playerNameText != null)
            playerNameText.text = nombre;
    }
    //aca termina


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
   
    public void OnUseItem(InputAction.CallbackContext context)
    {
        if (!IsOwner || IsDead() || !context.performed || inventory == null) return;

        inventory.RequestUseSlotServerRpc(inventory.SelectedSlot);
    }

    public void OnInventoryNext(InputAction.CallbackContext context)
    {
        if (!IsOwner || IsDead() || !context.performed || inventory == null) return;
        inventory.CycleSelection(1);
    }

    public void OnInventoryPrev(InputAction.CallbackContext context)
    {
        if (!IsOwner || IsDead() || !context.performed || inventory == null) return;
        inventory.CycleSelection(-1);
    }
    public void OnDropItem(InputAction.CallbackContext context)
    {
        if (!IsOwner || IsDead() || !context.performed || inventory == null) return;

        // El servidor valida el slot; si está vacío suena el sonido de error.
        inventory.RequestDropSlotServerRpc(inventory.SelectedSlot);
    }


    private bool IsDead()
    {
        return health != null && health.IsDead;
    }


    // Tecla E (accion Player/Interact). Atiende botiquines (US 3.1, sin
    // inventario: se consumen en el momento), pickups de armas tiradas en
    // el mapa y civiles que esperan ser rescatados. Si hay varios cerca a la
    // vez, se usa el que esté más cerca.
    public void OnInteraction(InputAction.CallbackContext context)
    {
        if (!IsOwner || IsDead() || !context.performed) return;

        ItemPickup item = ItemPickup.FindNearest(transform.position);
        float itemDist = item != null
            ? Vector3.Distance(transform.position, item.transform.position)
            : float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange, pickupLayer);
        WeaponPickup nearestWeapon = null;
        float nearestWeaponDist = float.MaxValue;

        foreach (var hit in hits)
        {
            WeaponPickup pickup = hit.GetComponent<WeaponPickup>();
            if (pickup == null) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < nearestWeaponDist)
            {
                nearestWeaponDist = dist;
                nearestWeapon = pickup;
            }
        }

        RescuableNpc npc = RescuableNpc.FindNearestWaiting(transform.position);
        float npcDist = npc != null
            ? Vector3.Distance(transform.position, npc.transform.position)
            : float.MaxValue;

        if (npc != null && npcDist <= itemDist && npcDist <= nearestWeaponDist)
        {
            npc.RequestRescue();
        }
        else if (item != null && itemDist <= nearestWeaponDist)
        {
            inventory?.RequestPickupItemServerRpc(item.NetworkObjectId);
        }
        else if (nearestWeapon != null)
        {
            weaponSwitcher?.RequestPickupWeaponServerRpc(
                (int)nearestWeapon.weaponType,
                nearestWeapon.GetComponent<NetworkObject>().NetworkObjectId
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

    // Espera a que el loadout de red se reconstruya tras ApplyServerState (US 4.10).
    private IEnumerator ApplyCarryOverWhenReady()
    {
        if (!PlayerCarryOver.HasLocalOwnerPending)
            yield break;

        float timeout = Time.realtimeSinceStartup + 3f;
        while (!PlayerCarryOver.IsLocalLoadoutReady(gameObject)
               && Time.realtimeSinceStartup < timeout)
            yield return null;

        // Un frame extra para que RebuildSlot termine tras el cambio de NetworkVariable.
        yield return null;
        PlayerCarryOver.TryApplyLocalOwner(gameObject);
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