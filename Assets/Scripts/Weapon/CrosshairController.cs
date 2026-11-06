using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
public class CrosshairController : NetworkBehaviour
{
    [SerializeField] private CrossHair crosshair;
    [SerializeField] private float shootPulseDuration = 0.15f;
    [Header("Hitmarker")]
[SerializeField] private Graphic hitMarker;              // Image con la X, hermana del crosshair (no hija)
[SerializeField] private Color hitColor = Color.white;
[SerializeField] private Color killColor = new Color(1f, 0.15f, 0.1f);
[SerializeField] private float hitMarkerDuration = 0.18f;
[SerializeField] private float killMarkerDuration = 0.35f;
[SerializeField] private float punchScale = 1.4f;

[Header("Sonido de impacto (AudioSource 2D, Spatial Blend = 0)")]
[SerializeField] private AudioSource hitAudio;
[SerializeField] private AudioClip hitSound;
[SerializeField] private AudioClip killSound;
[SerializeField] private Vector2 hitPitchRange = new Vector2(0.95f, 1.08f);
private float markerTimer;
private float markerDuration = 0.18f;
private Color markerColor;

    private PMovement playerMovement;
    private float shootTimer;

    private void Awake()
    {
        playerMovement = GetComponentInParent<PMovement>();
        if (hitMarker != null)
    hitMarker.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        // El crosshair solo tiene sentido para vos mismo, no para ver el de otros jugadores.
        enabled = IsOwner;
    }

    private void Update()
    {
        if (hitMarker != null && markerTimer > 0f)
{
    markerTimer -= Time.deltaTime;
    float t = Mathf.Clamp01(markerTimer / markerDuration); // 1 -> 0

    Color c = markerColor;
    c.a = t;
    hitMarker.color = c;
    hitMarker.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, t);

    if (markerTimer <= 0f)
        hitMarker.enabled = false;
}
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

    /// <summary>Llamado por WeaponSwitcher al cambiar de arma: oculta la mira cuando no hay ning�n arma equipada.</summary>
    public void SetVisible(bool visible)
    {
        if (crosshair != null && crosshair.cross_Hair != null)
        {
            crosshair.cross_Hair.gameObject.SetActive(visible);
        }
    }
    public void NotifyHit(bool killed)
{
    if (hitMarker != null)
    {
        markerColor = killed ? killColor : hitColor;
        markerDuration = killed ? killMarkerDuration : hitMarkerDuration;
        markerTimer = markerDuration;
        hitMarker.enabled = true;
    }

    AudioClip clip = killed ? killSound : hitSound;
    if (hitAudio != null && clip != null)
    {
        hitAudio.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
        hitAudio.PlayOneShot(clip);
    }
}
}