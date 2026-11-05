using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

// Musica dinamica del juego.
//
//  - El SERVIDOR mira al AIDirector (AIDirector.Instance.CombatMusicActive) y decide si el
//    estado es COMBATE o CALMA. Lo replica a todos con una NetworkVariable.
//  - Cada CLIENTE reproduce la musica de forma local (nadie sincroniza el audio).
//    Cada uno elige sus propias canciones al azar de las listas.
//
//  Combate : suena mientras hay una horda en curso, un miniboss vivo o la intensidad del equipo en pico
//            (miniboss e intensidad se pueden apagar en el AIDirector).
//  Calma   : suena cuando no hay horda. Al terminar la horda, la musica de combate hace
//            fade out, pasa un silencio (calmStartDelay) y recien ahi entra la calma con fade in.
//
// Setup: GameObject vacio en la escena de juego con NetworkObject + este script
// (colocado en la escena, no hace falta registrarlo como prefab). Cargar las dos listas.
public class MusicDirector : NetworkBehaviour
{
    [Header("Listas de canciones")]
    [SerializeField] private AudioClip[] calmTracks;
    [SerializeField] private AudioClip[] combatTracks;

    [Header("Volumen")]
    [Range(0f, 1f)] [SerializeField] private float calmVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float combatVolume = 0.7f;
    [Tooltip("Opcional: grupo del AudioMixer para que respete el volumen de musica de tus opciones.")]
    [SerializeField] private AudioMixerGroup mixerGroup;

    [Header("Transiciones (segundos)")]
    [Tooltip("Cuanto tarda en entrar la musica de combate al empezar la horda.")]
    [SerializeField] private float combatFadeIn = 1.5f;
    [Tooltip("Cuanto tarda en desaparecer la musica de combate cuando termina la horda.")]
    [SerializeField] private float combatFadeOut = 5f;
    [Tooltip("Cuanto tarda en entrar la musica calma.")]
    [SerializeField] private float calmFadeIn = 4f;
    [Tooltip("Cuanto tarda en desaparecer la musica calma cuando arranca el combate.")]
    [SerializeField] private float calmFadeOut = 1.5f;
    [Tooltip("Silencio despues de que termina la horda, antes de que entre la musica calma.")]
    [SerializeField] private float calmStartDelay = 3f;

    [Header("Entre canciones")]
    [Tooltip("Fade al inicio y al final de cada cancion, para que no corte de golpe.")]
    [SerializeField] private float trackFade = 2f;
    [Tooltip("Silencio aleatorio (min, max) entre una cancion calma y la siguiente.")]
    [SerializeField] private Vector2 calmGap = new Vector2(2f, 8f);
    [Tooltip("Silencio aleatorio (min, max) entre canciones de combate.")]
    [SerializeField] private Vector2 combatGap = new Vector2(0f, 0f);

    [Header("Servidor")]
    [Tooltip("Tiempo minimo en combate antes de poder volver a calma (evita parpadeos).")]
    [SerializeField] private float minCombatTime = 8f;
    [SerializeField] private float pollInterval = 0.25f;

    private class Channel
    {
        public AudioSource source;
        public AudioClip[] list;
        public AudioClip last;
        public float level;       // 0..1, fade de modo (calma <-> combate)
        public float gapTimer;
        public bool started;      // true mientras una cancion de este canal esta sonando
    }

    private readonly NetworkVariable<bool> inCombat = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Channel calm;
    private Channel combat;
    private float calmDelayTimer;
    private float combatSince;
    private float pollTimer;
    private bool noAudio;

    private void Awake()
    {
        // Servidor dedicado / batchmode: no hay nada que reproducir.
        noAudio = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        if (noAudio)
            return;

        calm = new Channel { source = CreateSource("CalmMusic"), list = calmTracks };
        combat = new Channel { source = CreateSource("CombatMusic"), list = combatTracks };
    }

    private AudioSource CreateSource(string childName)
    {
        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;   // 2D: se escucha igual en cualquier lugar
        src.volume = 0f;
        if (mixerGroup != null)
            src.outputAudioMixerGroup = mixerGroup;

        return src;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
            ServerTick();

        if (!noAudio)
            ClientTick(Time.unscaledDeltaTime);
    }

    // ---------------------------------------------------------------- servidor

    private void ServerTick()
    {
        pollTimer -= Time.deltaTime;
        if (pollTimer > 0f)
            return;

        pollTimer = pollInterval;

        bool wantCombat = AIDirector.Instance != null && AIDirector.Instance.CombatMusicActive;

        if (wantCombat)
        {
            if (!inCombat.Value)
            {
                combatSince = Time.time;
                inCombat.Value = true;
            }
        }
        else if (inCombat.Value && Time.time - combatSince >= minCombatTime)
        {
            inCombat.Value = false;
        }
    }

    // ---------------------------------------------------------------- cliente

    private void ClientTick(float dt)
    {
        bool combatNow = inCombat.Value;

        // Al terminar la horda se espera un poco de silencio antes de la calma.
        if (combatNow)
            calmDelayTimer = calmStartDelay;
        else if (calmDelayTimer > 0f)
            calmDelayTimer -= dt;

        bool calmWanted = !combatNow && calmDelayTimer <= 0f;

        Drive(combat, combatNow, combatVolume, combatFadeIn, combatFadeOut, combatGap, dt);
        Drive(calm, calmWanted, calmVolume, calmFadeIn, calmFadeOut, calmGap, dt);
    }

    private void Drive(Channel ch, bool wanted, float maxVolume, float fadeIn, float fadeOut, Vector2 gap, float dt)
    {
        float duration = Mathf.Max(0.01f, wanted ? fadeIn : fadeOut);
        ch.level = Mathf.MoveTowards(ch.level, wanted ? 1f : 0f, dt / duration);

        AudioSource src = ch.source;

        // Totalmente apagado: se corta la cancion. La proxima vez arranca una nueva.
        if (!wanted && ch.level <= 0f)
        {
            if (src.isPlaying)
                src.Stop();

            src.volume = 0f;
            ch.started = false;
            ch.gapTimer = 0f;
            return;
        }

        if (wanted)
        {
            if (ch.started && !src.isPlaying)
            {
                // Termino la cancion: silencio corto y despues la siguiente.
                ch.started = false;
                ch.gapTimer = Random.Range(Mathf.Min(gap.x, gap.y), Mathf.Max(gap.x, gap.y));
            }

            if (!ch.started)
            {
                ch.gapTimer -= dt;
                if (ch.gapTimer > 0f)
                    return;

                AudioClip next = Pick(ch.list, ch.last);
                if (next == null)
                    return;

                ch.last = next;
                src.clip = next;
                src.volume = 0f;
                src.Play();
                ch.started = true;
            }
        }

        if (!src.isPlaying || src.clip == null)
            return;

        // Fade de la cancion misma (inicio y final) multiplicado por el fade de modo.
        float edge = 1f;
        if (trackFade > 0.01f)
        {
            float fromStart = src.time / trackFade;
            float toEnd = (src.clip.length - src.time) / trackFade;
            edge = Mathf.Clamp01(Mathf.Min(fromStart, toEnd));
        }

        src.volume = maxVolume * ch.level * edge;
    }

    private static AudioClip Pick(AudioClip[] list, AudioClip avoid)
    {
        if (list == null || list.Length == 0)
            return null;

        // Sin repetir la ultima si hay mas de una cancion valida.
        int valid = 0;
        foreach (AudioClip clip in list)
        {
            if (clip != null)
                valid++;
        }

        if (valid == 0)
            return null;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            AudioClip candidate = list[Random.Range(0, list.Length)];
            if (candidate == null)
                continue;
            if (candidate == avoid && valid > 1)
                continue;
            return candidate;
        }

        foreach (AudioClip clip in list)
        {
            if (clip != null)
                return clip;
        }

        return null;
    }
}
