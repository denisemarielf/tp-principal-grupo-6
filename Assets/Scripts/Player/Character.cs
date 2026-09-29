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

    private PMovement movement;
    private readonly List<Behaviour> disabledSceneCameras = new List<Behaviour>();
    private bool placedByServer;


    private void Awake()
    {
        movement = GetComponent<PMovement>();

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
        if (!IsOwner) return;

        movement.SetMoveInput(context.ReadValue<Vector2>());
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;


        if (context.performed)
        {
            movement.TryJump();
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
