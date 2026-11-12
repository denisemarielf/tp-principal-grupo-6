using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class CrosshairController : NetworkBehaviour
{
    [SerializeField] private CrossHair crosshair;

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

    [Header("Headshot")]
    [SerializeField] private Color headshotColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private float headshotPunchScale = 1.9f;
    [SerializeField] private AudioClip headshotSound;
    [SerializeField] private AudioClip headshotKillSound;

    private float markerTimer;
    private float markerDuration = 0.18f;
    private Color markerColor;
    private float markerPunch = 1.4f;

    // Dispersión del arma equipada (grados) y su equivalente en píxeles de pantalla
    private float spreadDeg;
    public float SpreadPixels { get; private set; }

    private void Awake()
    {
        if (hitMarker != null)
            hitMarker.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        // El crosshair solo tiene sentido para vos mismo, no para ver el de otros jugadores.
        enabled = IsOwner;
    }

    /// <summary>Llamado cada frame por el arma equipada (vía WeaponLogic.UpdateCrosshairSpread).</summary>
    public void SetSpread(float degrees)
    {
        spreadDeg = degrees;
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
            hitMarker.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, markerPunch, t);

            if (markerTimer <= 0f)
                hitMarker.enabled = false;
        }

        // Grados -> píxeles de pantalla usando el FOV actual (sigue bien al apuntar con ADS).
        Camera cam = Camera.main;
        if (cam != null)
        {
            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfScreen = Screen.height * 0.5f;
            SpreadPixels = Mathf.Tan(spreadDeg * Mathf.Deg2Rad) / Mathf.Tan(halfFov) * halfScreen;
        }

        bool isSprinting = false; // TODO: reemplazar cuando implementes sprint en PMovement

        if (crosshair != null)
            crosshair.UpdateCrossHairSize(SpreadPixels, isSprinting);
    }

    /// <summary>Se mantiene por compatibilidad con WeaponLogic; el "salto" de la mira ahora lo da el bloom del spread.</summary>
    public void NotifyShotFired() { }

    /// <summary>Llamado por WeaponSwitcher al cambiar de arma: oculta la mira cuando no hay ningún arma equipada.</summary>
    public void SetVisible(bool visible)
    {
        if (crosshair != null && crosshair.cross_Hair != null)
        {
            crosshair.cross_Hair.gameObject.SetActive(visible);
        }
    }

    public void NotifyHit(bool killed, bool headshot = false)
    {
        if (hitMarker != null)
        {
            markerColor = killed ? killColor : (headshot ? headshotColor : hitColor);
            markerDuration = killed ? killMarkerDuration : hitMarkerDuration;
            markerPunch = headshot ? headshotPunchScale : punchScale;
            markerTimer = markerDuration;
            hitMarker.enabled = true;
        }

        AudioClip clip;
        if (headshot && killed && headshotKillSound != null) clip = headshotKillSound;
        else if (headshot && headshotSound != null)          clip = headshotSound;
        else                                                 clip = killed ? killSound : hitSound;

        if (hitAudio != null && clip != null)
        {
            hitAudio.pitch = Random.Range(hitPitchRange.x, hitPitchRange.y);
            hitAudio.PlayOneShot(clip);
        }
    }
}
