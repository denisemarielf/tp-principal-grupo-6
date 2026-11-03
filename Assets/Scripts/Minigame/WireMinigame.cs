using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// Controla el minijuego: asigna colores, cuenta conexiones, reproduce sonidos y efectos.
public class WireMinigame : MonoBehaviour
{
    [Header("Conectores (mismo largo en ambos arrays)")]
    public WireConnector[] sources;
    public WireConnector[] targets;
    public Color[] palette;            // al menos tantos colores como cables

    [Header("Sonido")]
    public AudioSource audioSource;
    public AudioClip connectClip;
    public AudioClip errorClip;
    public AudioClip completeClip;

    [Header("Efectos")]
    public ParticleSystem sparks;      // chispas al conectar (opcional)

    [Header("Al completar")]
    public UnityEvent onCompleted;     // aquí se habilita el botón del helicóptero

    int connectedCount;
    bool completed;

    void Start() => Setup();

    public void Setup()
    {
        connectedCount = 0;
        completed = false;

        if (sources.Length == 0 || targets.Length != sources.Length || palette.Length < sources.Length)
        {
            Debug.LogError($"WireMinigame mal configurado: sources={sources.Length}, targets={targets.Length}, palette={palette.Length}. " +
                           "sources y targets deben tener el mismo tamaño, y palette al menos ese tamaño.", this);
            return;
        }

        // Mezcla los colores de la columna derecha
        int[] ids = Enumerable.Range(0, sources.Length).OrderBy(_ => Random.value).ToArray();

        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].Init(this, i, palette[i]);
            targets[i].Init(this, ids[i], palette[ids[i]]);
        }
    }

    public void OnWireConnected(Vector3 worldPos)
    {
        connectedCount++;

        // El pitch sube con cada cable: da sensación de progreso
        Play(connectClip, 1f + 0.12f * connectedCount);

        if (sparks)
        {
            sparks.transform.position = worldPos;
            sparks.Play();
        }

        if (!completed && connectedCount >= sources.Length)
        {
            completed = true;
            Invoke(nameof(Finish), 0.4f);
        }
    }

    public void PlayError() => Play(errorClip, Random.Range(0.9f, 1.05f));

    void Finish()
    {
        Play(completeClip, 1f);
        onCompleted?.Invoke();
    }

    void Play(AudioClip clip, float pitch)
    {
        if (!clip || !audioSource) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip);
    }
}