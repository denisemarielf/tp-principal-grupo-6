using UnityEngine;
using QuickOutline;

public class PickupGlow : MonoBehaviour
{
    [Header("Pulso de la luz")]
    public Light glowLight;
    public float pulseSpeed = 2f;
    public float minIntensity = 1f;
    public float maxIntensity = 2.5f;

    [Header("Resaltado")]
    [Tooltip("Contorno del pickup. Si falta, se agrega solo.")]
    [SerializeField] private Outline outline;
    [SerializeField] private Color outlineColor = new Color32(78, 255, 5, 255);
    [SerializeField] private float outlineWidth = 4f;

    private static readonly Color WeaponYellow = new Color(1f, 0.86f, 0.05f, 1f);
    private static readonly Color AmmoRed = new Color(1f, 0.08f, 0.08f, 1f);
    private static readonly Color MedkitGreen = new Color(0.2f, 1f, 0.12f, 1f);

    private const float HighlightRadius = 15f;
    private Transform player;

    private void Awake()
    {
        outlineColor = ResolveGlowColor();

        if (glowLight == null)
            glowLight = GetComponentInChildren<Light>(true);
        if (glowLight != null)
            glowLight.color = outlineColor;

        if (outline == null)
            outline = GetComponent<Outline>();
        if (outline == null)
            outline = gameObject.AddComponent<Outline>();

        // Visible: el depth test lo tapa si hay una pared adelante.
        outline.OutlineMode = Outline.Mode.OutlineVisible;
        outline.OutlineColor = outlineColor;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = false;
    }

    // Armas amarillo, munición rojo, botiquines verde. El resto conserva el color del prefab.
    private Color ResolveGlowColor()
    {
        if (GetComponent<AmmoCase>() != null)
            return AmmoRed;

        ItemPickup item = GetComponent<ItemPickup>();
        if (item != null)
        {
            if (item.ItemId == 2)
                return AmmoRed;
            if (item.ItemId == 1)
                return MedkitGreen;
        }

        if (GetComponent<MedkitPickup>() != null)
            return MedkitGreen;

        if (GetComponent<WeaponPickup>() != null)
            return WeaponYellow;

        return outlineColor;
    }

    private void Update()
    {
        if (glowLight != null)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            glowLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        }

        if (outline == null)
            return;

        if (player == null)
        {
            if (Camera.main != null)
                player = Camera.main.transform;
            else
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged.transform;
            }
        }

        bool near = player != null &&
            (player.position - transform.position).sqrMagnitude <= HighlightRadius * HighlightRadius;

        if (outline.enabled != near)
            outline.enabled = near;
    }
}