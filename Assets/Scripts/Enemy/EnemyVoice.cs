using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

// Voces de los zombies (gruñidos, ataque, dolor, muerte).
// Es solo ambientacion: cada cliente decide por su cuenta que voces se oyen, no hay trafico de red.
// Para que no se vuelva un caos con muchos zombies, todos comparten un presupuesto de voces:
//  - solo suenan los que estan dentro del rango de escucha,
//  - hay un maximo de voces simultaneas (total y por tipo),
//  - los gruñidos ambiente tienen un tiempo minimo entre uno y otro, sin importar cuantos zombies haya,
//  - una voz mas importante (muerte > ataque > dolor > ambiente) corta a una menos importante.
[DisallowMultipleComponent]
public class EnemyVoice : MonoBehaviour
{
    // El orden es la prioridad.
    public enum Kind
    {
        Ambient = 0,
        Hurt = 1,
        Attack = 2,
        Death = 3
    }

    [Header("Audio")]
    [Tooltip("Si queda vacio se crea uno 3D automaticamente.")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    [Header("Voces")]
    [SerializeField] private AudioClip[] ambientClips;   // gruñidos mientras camina o espera
    [SerializeField] private AudioClip[] hurtClips;      // al recibir un disparo
    [SerializeField] private AudioClip[] attackClips;    // al empezar a atacar o morder
    [SerializeField] private AudioClip[] deathClips;     // al morir (si ya usas "Death Sounds" en EnemyHealth, dejalo vacio)

    [Header("Frecuencia")]
    [SerializeField] private Vector2 ambientInterval = new Vector2(6f, 14f);
    [SerializeField] private float hurtCooldown = 1.5f;

    [Header("Limite de ruido")]
    [Tooltip("Fuera de esta distancia a la camara el zombie no emite voces.")]
    [SerializeField] private float hearingRange = 30f;

    // Presupuesto global, compartido por todos los zombies del cliente.
    private const int MaxTotalVoices = 5;
    private const int MaxAmbientVoices = 2;
    private const int MaxHurtVoices = 2;
    private const int MaxAttackVoices = 3;
    private const int MaxDeathVoices = 3;
    private const float GlobalAmbientGap = 1.2f;   // minimo entre dos gruñidos ambiente de cualquier zombie

    private static readonly List<EnemyVoice> All = new List<EnemyVoice>();
    private static float nextGlobalAmbientTime;

    private readonly int[] lastClipIndex = { -1, -1, -1, -1 };
    private EnemyHealth health;
    private NetworkObject networkObject;
    private Kind currentKind;
    private float nextAmbientAttempt;
    private float nextHurtTime;

    private bool IsSpeaking => source != null && source.isPlaying;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        networkObject = GetComponent<NetworkObject>();

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = hearingRange;
        }

        if (mixerGroup != null)
            source.outputAudioMixerGroup = mixerGroup;
    }

    private void OnEnable()
    {
        All.Add(this);
        // Cada zombie arranca en un momento distinto para que no gruñan todos a la vez al aparecer.
        nextAmbientAttempt = Time.time + Random.Range(0f, ambientInterval.y);
    }

    private void OnDisable()
    {
        All.Remove(this);
        if (source != null)
            source.Stop();
    }

    private void Update()
    {
        if (Time.time < nextAmbientAttempt)
            return;

        // Se reprograma siempre, aunque este turno no suene: asi no se acumulan intentos.
        nextAmbientAttempt = Time.time + Random.Range(ambientInterval.x, ambientInterval.y);

        if (networkObject != null && !networkObject.IsSpawned)
            return;

        TryPlay(Kind.Ambient);
    }

    // Devuelve true si la voz empezo a sonar. Si no hay "lugar" en el presupuesto, simplemente se omite.
    public bool TryPlay(Kind kind)
    {
        if (source == null)
            return false;

        AudioClip[] clips = ClipsFor(kind);
        if (clips == null || clips.Length == 0)
            return false;

        if (kind != Kind.Death && health != null && health.IsDead())
            return false;
        if (!InHearingRange())
            return false;
        if (kind == Kind.Hurt && Time.time < nextHurtTime)
            return false;

        if (kind == Kind.Death)
        {
            if (CountPlaying(Kind.Death, this) >= MaxDeathVoices)
                return false;
        }
        else
        {
            // Ya esta hablando algo igual o mas importante: no se pisa.
            if (IsSpeaking && currentKind >= kind)
                return false;
            if (CountPlaying(null, this) >= MaxTotalVoices)
                return false;

            switch (kind)
            {
                case Kind.Ambient:
                    if (Time.time < nextGlobalAmbientTime || CountPlaying(Kind.Ambient, this) >= MaxAmbientVoices)
                        return false;
                    break;
                case Kind.Hurt:
                    if (CountPlaying(Kind.Hurt, this) >= MaxHurtVoices)
                        return false;
                    break;
                case Kind.Attack:
                    if (CountPlaying(Kind.Attack, this) >= MaxAttackVoices)
                        return false;
                    break;
            }
        }

        AudioClip clip = PickClip(kind, clips);
        if (clip == null)
            return false;

        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.Play();
        currentKind = kind;

        if (kind == Kind.Ambient)
            nextGlobalAmbientTime = Time.time + GlobalAmbientGap;
        else if (kind == Kind.Hurt)
            nextHurtTime = Time.time + hurtCooldown;

        return true;
    }

    private AudioClip[] ClipsFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.Ambient: return ambientClips;
            case Kind.Hurt: return hurtClips;
            case Kind.Attack: return attackClips;
            default: return deathClips;
        }
    }

    // No repite el mismo clip dos veces seguidas en el mismo zombie.
    private AudioClip PickClip(Kind kind, AudioClip[] clips)
    {
        int slot = (int)kind;
        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == lastClipIndex[slot])
            index = (index + 1) % clips.Length;

        lastClipIndex[slot] = index;
        return clips[index];
    }

    private bool InHearingRange()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return true;

        return (cam.transform.position - transform.position).sqrMagnitude <= hearingRange * hearingRange;
    }

    // Cuantas voces suenan ahora (de un tipo, o todas si kind es null), sin contar a "exclude".
    private static int CountPlaying(Kind? kind, EnemyVoice exclude)
    {
        int count = 0;
        foreach (EnemyVoice voice in All)
        {
            if (voice == exclude || !voice.IsSpeaking)
                continue;
            if (kind.HasValue && voice.currentKind != kind.Value)
                continue;

            count++;
        }

        return count;
    }
}