using UnityEngine;
using Unity.Netcode;

public class CrosshairController : NetworkBehaviour
{
    [SerializeField] private CrossHair crosshair;
    [SerializeField] private float shootPulseDuration = 0.15f;

    private PMovement playerMovement;
    private float shootTimer;

    private void Awake()
    {
        playerMovement = GetComponentInParent<PMovement>();
    }

    public override void OnNetworkSpawn()
    {
        // El crosshair solo tiene sentido para vos mismo, no para ver el de otros jugadores.
        enabled = IsOwner;
    }

    private void Update()
    {
        if (shootTimer > 0f)
            shootTimer -= Time.deltaTime;

        bool isWalking = playerMovement != null && playerMovement.MoveInput.magnitude > 0.1f;
        bool isSprinting = false; // TODO: reemplazar cuando implementes sprint en PMovement
        bool isShooting = shootTimer > 0f;

        crosshair.UpdateCrossHairSize(isSprinting, isWalking, isShooting);
    }

    /// <summary>Llamado por WeaponLogic en cada disparo, para que la mira "salte" un instante.</summary>
    public void NotifyShotFired()
    {
        shootTimer = shootPulseDuration;
    }

    /// <summary>Llamado por WeaponSwitcher al cambiar de arma: oculta la mira cuando no hay ningún arma equipada.</summary>
    public void SetVisible(bool visible)
    {
        if (crosshair != null && crosshair.cross_Hair != null)
        {
            crosshair.cross_Hair.gameObject.SetActive(visible);
        }
    }
}