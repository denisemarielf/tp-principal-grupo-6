using System.Collections;
using UnityEngine;

// Luz de lampara que parpadea como si la instalacion electrica estuviera por morir.
// Casi todo el tiempo queda estable (algo tenue) y de golpe tiene rafagas de parpadeo, a veces con apagon corto.
// Si hay un renderer asignado, tambien apaga y prende su emision para que el foco acompane a la luz.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("Opcional: el foco/lampara cuya emision acompana a la luz.")]
    [SerializeField] private Renderer emissiveRenderer;
    [SerializeField] private Color emissionColor = new Color(1f, 0.85f, 0.6f);
    [Tooltip("Intensidad de la luz cuando esta estable, como fraccion de la que tenia en la escena.")]
    [Range(0f, 1f)] [SerializeField] private float steadyFactor = 0.8f;
    [Tooltip("Segundos entre rafagas de parpadeo (min, max).")]
    [SerializeField] private Vector2 timeBetweenBursts = new Vector2(2f, 9f);
    [Tooltip("Cantidad de cambios de estado por rafaga (min, max).")]
    [SerializeField] private Vector2Int flickersPerBurst = new Vector2Int(3, 9);
    [Range(0f, 1f)] [SerializeField] private float blackoutChance = 0.25f;
    [Tooltip("Si es true, esta lampara nunca se recupera del todo: queda en penumbra permanente.")]
    [SerializeField] private bool dying;

    private Light lamp;
    private float baseIntensity;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        StartCoroutine(Loop());
    }

    private void OnDisable()
    {
        SetLevel(1f);
    }

    private IEnumerator Loop()
    {
        // Desfasa las lamparas para que no parpadeen todas juntas.
        yield return new WaitForSeconds(Random.Range(0f, timeBetweenBursts.y));

        while (true)
        {
            SetLevel(steadyFactor);
            yield return new WaitForSeconds(Random.Range(timeBetweenBursts.x, timeBetweenBursts.y));

            int flickers = Random.Range(flickersPerBurst.x, flickersPerBurst.y + 1);
            for (int i = 0; i < flickers; i++)
            {
                SetLevel(Random.value < 0.5f ? Random.Range(0f, 0.2f) : Random.Range(0.5f, 1f));
                yield return new WaitForSeconds(Random.Range(0.02f, 0.12f));
            }

            if (Random.value < blackoutChance)
            {
                SetLevel(0f);
                yield return new WaitForSeconds(Random.Range(0.4f, 1.5f));
            }

            if (dying)
            {
                SetLevel(0.15f);
                yield return new WaitForSeconds(Random.Range(1f, 3f));
            }
        }
    }

    private void SetLevel(float level)
    {
        lamp.intensity = baseIntensity * level;
        if (emissiveRenderer == null) return;

        emissiveRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, emissionColor * level);
        emissiveRenderer.SetPropertyBlock(block);
    }
}
