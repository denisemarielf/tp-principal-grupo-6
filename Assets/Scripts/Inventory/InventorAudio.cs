using UnityEngine;

public enum InventorySound { Pickup, Use, Fail, Drop }

// Reproduce los sonidos del inventario del jugador local.
public class InventoryAudio : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    [Header("Sonidos por defecto")]
    [SerializeField] private AudioClip defaultPickup;
    [SerializeField] private AudioClip defaultUse;
    [SerializeField] private AudioClip failSound;
    [SerializeField] private AudioClip dropSound;

    public void Play(InventorySound kind, ItemDefinition def)
    {
        if (source == null) return;

        AudioClip clip = null;
        switch (kind)
        {
            case InventorySound.Pickup:
                clip = def != null && def.pickupSound != null ? def.pickupSound : defaultPickup;
                break;
            case InventorySound.Use:
                clip = def != null && def.useSound != null ? def.useSound : defaultUse;
                break;
            case InventorySound.Fail: clip = failSound; break;
            case InventorySound.Drop: clip = dropSound; break;
        }

        if (clip == null) return;
        source.pitch = 1f;
        source.PlayOneShot(clip, volume);
    }
}