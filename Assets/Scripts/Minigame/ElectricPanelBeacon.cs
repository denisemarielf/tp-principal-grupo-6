using UnityEngine;

/// Hace que el panel eléctrico del mundo resalte: luz pulsante, emisión, chispas y zumbido.
/// Mientras no esté resuelto parpadea en rojo; al resolverse queda en verde y se apagan chispas y zumbido.
public class ElectricPanelBeacon : MonoBehaviour
{
    [SerializeField] private ElectricPanel panel;

    [Header("Luz")]
    [SerializeField] private Light beaconLight;
    [SerializeField] private Color pendingColor = new Color(1f, 0.25f, 0.1f);
    [SerializeField] private Color solvedColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private float minIntensity = 0.5f;
    [SerializeField] private float maxIntensity = 3f;
    [SerializeField] private float pulseSpeed = 2.5f;

    [Header("Emisión (opcional)")]
    [Tooltip("Renderers del modelo que brillan (luces, perillas). Su material debe tener Emission activado.")]
    [SerializeField] private Renderer[] emissiveRenderers;
    [SerializeField] private float emissionBoost = 2f;

    [Header("Chispas y zumbido")]
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private Vector2 sparkInterval = new Vector2(1.5f, 4f);
    [Tooltip("AudioSource en loop con Spatial Blend = 1 (3D) y Max Distance de unos 8-10 m.")]
    [SerializeField] private AudioSource buzz;

    private bool solved;
    private float nextSpark;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (panel != null) panel.onSolved.AddListener(OnSolved);
    }

    private void OnDestroy()
    {
        if (panel != null) panel.onSolved.RemoveListener(OnSolved);
    }

    private void OnSolved()
    {
        solved = true;
        if (buzz != null) buzz.Stop();
        if (sparks != null) sparks.Stop();
        Apply(solvedColor, maxIntensity * 0.6f);
    }

    private void Update()
    {
        if (solved) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Apply(pendingColor, Mathf.Lerp(minIntensity, maxIntensity, t));

        if (sparks != null && Time.time >= nextSpark)
        {
            sparks.Play();
            nextSpark = Time.time + Random.Range(sparkInterval.x, sparkInterval.y);
        }
    }

    private void Apply(Color color, float intensity)
    {
        if (beaconLight != null)
        {
            beaconLight.color = color;
            beaconLight.intensity = intensity;
        }

        if (emissiveRenderers == null) return;

        Color emission = color * (intensity / Mathf.Max(0.01f, maxIntensity)) * emissionBoost;
        foreach (Renderer r in emissiveRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", emission);
            r.SetPropertyBlock(block);
        }
    }
}
