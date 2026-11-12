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

    [Header("Chispas")]
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private Vector2 sparkInterval = new Vector2(1.5f, 4f);

    [Header("Zumbido / sonidos de chispas")]
    [Tooltip("AudioSource con Spatial Blend = 1 (3D) y Max Distance de unos 8-10 m. Loop desactivado.")]
    [SerializeField] private AudioSource buzz;
    [Tooltip("Lista de sonidos; se reproduce uno al azar cada vez. Si está vacía, usa el clip del AudioSource en loop.")]
    [SerializeField] private AudioClip[] buzzClips;
    [Tooltip("Pausa aleatoria (segundos) entre un sonido y el siguiente.")]
    [SerializeField] private Vector2 buzzGap = new Vector2(0.1f, 1.2f);
    [Tooltip("Variación aleatoria de pitch para que no suene repetitivo.")]
    [SerializeField] private Vector2 buzzPitchRange = new Vector2(0.9f, 1.1f);

    private bool solved;
    private float nextSpark;
    private float nextBuzz;
    private int lastClipIndex = -1;
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

    private void Start()
    {
        if (solved || buzz == null) return;

        if (HasClips())
        {
            buzz.loop = false;
            nextBuzz = Time.time + Random.Range(0f, buzzGap.y);
        }
        else
        {
            // Sin lista: comportamiento anterior (un clip en loop)
            buzz.loop = true;
            if (!buzz.isPlaying) buzz.Play();
        }
    }

    private void Update()
    {
        if (solved) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Apply(pendingColor, Mathf.Lerp(minIntensity, maxIntensity, t));

        if (sparks != null && Time.time >= nextSpark)
        {
            sparks.Clear();
            sparks.Play();
            nextSpark = Time.time + Random.Range(sparkInterval.x, sparkInterval.y);
        }

        if (buzz != null && HasClips() && Time.time >= nextBuzz)
            PlayRandomBuzz();
    }

    private bool HasClips()
    {
        return buzzClips != null && buzzClips.Length > 0;
    }

    private void PlayRandomBuzz()
    {
        int index = Random.Range(0, buzzClips.Length);

        // Evita repetir el mismo clip dos veces seguidas
        if (buzzClips.Length > 1 && index == lastClipIndex)
            index = (index + 1 + Random.Range(0, buzzClips.Length - 1)) % buzzClips.Length;
        lastClipIndex = index;

        AudioClip clip = buzzClips[index];
        if (clip == null)
        {
            nextBuzz = Time.time + 0.5f;
            return;
        }

        float pitch = Random.Range(buzzPitchRange.x, buzzPitchRange.y);
        buzz.pitch = pitch;
        buzz.PlayOneShot(clip);

        nextBuzz = Time.time + (clip.length / pitch) + Random.Range(buzzGap.x, buzzGap.y);
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