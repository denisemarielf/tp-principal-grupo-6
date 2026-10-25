using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// Contrato común de toda arma equipable (fuego o melee).
public abstract class EquippableWeapon : NetworkBehaviour
{
    public abstract void SetEquipped(bool equipped);
    public abstract void OnShoot(InputAction.CallbackContext context);
    public virtual void OnReload(InputAction.CallbackContext context) { }
    public abstract Sprite HudIcon { get; }
    public virtual bool UsesAmmo => false;
}
