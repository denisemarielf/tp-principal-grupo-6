using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TrainMenuController : NetworkBehaviour
{
    [Header("Tren")]
    [SerializeField] private Transform train;          // visual, SIN NetworkObject
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform stationPoint;
    [SerializeField] private float speed = 25f;
    [SerializeField] private bool alternateDirection = true;

    [Header("Intervalo entre trenes (segundos)")]
    [SerializeField] private float minInterval = 10f;
    [SerializeField] private float maxInterval = 25f;
    [SerializeField] private float firstTrainDelay = 3f;

    [Header("Bocina")]
    [SerializeField] private float hornDistance = 40f;

    [Header("Audio")]
    [SerializeField] private AudioSource movingSource;
    [SerializeField] private AudioSource hornSource;
    [SerializeField] private AudioClip[] hornClips;
    [SerializeField] private AudioClip[] movingClips;
    [SerializeField] private Vector2 hornPitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private float fadeTime = 1.5f;
    [SerializeField] private float movingMaxVolume = 1f;

    [Header("Rotación")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    // ---- Estado sincronizado (solo escribe el servidor) ----
    private readonly NetworkVariable<int> netRunId = new(0);
    private readonly NetworkVariable<bool> netForward = new(true);
    private readonly NetworkVariable<double> netStartTime = new(0);
    private readonly NetworkVariable<int> netHornIndex = new(-1);
    private readonly NetworkVariable<int> netMovingIndex = new(-1);

    // ---- Estado local para modo sin red (menú sin sesión) ----
    private int locRunId;
    private bool locForward = true;
    private double locStartTime;
    private int locHornIndex = -1;
    private int locMovingIndex = -1;

    // ---- Estado del servidor / loop ----
    private bool serverForward = true;
    private int lastHornIndex = -1;
    private int lastMovingIndex = -1;

    // ---- Estado de reproducción local ----
    private int playingRunId = -1;
    private bool trainActive;
    private bool hornPlayed;

    // Fuente de datos: red si hay sesión, local si no
    private double Now => IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private int RunId => IsSpawned ? netRunId.Value : locRunId;
    private bool RunForward => IsSpawned ? netForward.Value : locForward;
    private double RunStart => IsSpawned ? netStartTime.Value : locStartTime;
    private int RunHornIndex => IsSpawned ? netHornIndex.Value : locHornIndex;
    private int RunMovingIndex => IsSpawned ? netMovingIndex.Value : locMovingIndex;

    private void Start()
    {
        train.gameObject.SetActive(false);

        // Sin sesión de red (ej: menú sin conexión): el tren corre en modo local
        if (!IsSpawned && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
            StartCoroutine(ServerLoop());
    }

    public override void OnNetworkSpawn()
    {
        // Si había un loop local corriendo, lo cortamos: ahora manda la red
        StopAllCoroutines();
        EndLocalRun();
        playingRunId = -1;

        if (IsServer) StartCoroutine(ServerLoop());
    }

    // ================= SERVIDOR =================
    private IEnumerator ServerLoop()
    {
        yield return new WaitForSeconds(firstTrainDelay);

        while (true)
        {
            float duration = Vector3.Distance(startPoint.position, endPoint.position) / speed;

            PublishRun(serverForward,
                       PickIndex(hornClips, ref lastHornIndex),
                       PickIndex(movingClips, ref lastMovingIndex));

            yield return new WaitForSeconds(duration);

            if (alternateDirection) serverForward = !serverForward;
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    private void PublishRun(bool forward, int hornIdx, int movingIdx)
    {
        double start = Now;

        if (IsSpawned)
        {
            netForward.Value = forward;
            netStartTime.Value = start;
            netHornIndex.Value = hornIdx;
            netMovingIndex.Value = movingIdx;
            netRunId.Value++;               // siempre al final: dispara el nuevo recorrido
        }
        else
        {
            locForward = forward;
            locStartTime = start;
            locHornIndex = hornIdx;
            locMovingIndex = movingIdx;
            locRunId++;
        }
    }

    private int PickIndex(AudioClip[] clips, ref int last)
    {
        if (clips == null || clips.Length == 0) return -1;
        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == last) index = (index + 1) % clips.Length;
        last = index;
        return index;
    }

    // ================= TODOS (host y clientes) =================
    private void Update()
    {
        if (RunId == 0) return;                       // todavía no salió ningún tren

        if (RunId != playingRunId)                    // recorrido nuevo
        {
            playingRunId = RunId;
            EndLocalRun();
        }

        Transform from = RunForward ? startPoint : endPoint;
        Transform to = RunForward ? endPoint : startPoint;

        Vector3 vec = to.position - from.position;
        float length = vec.magnitude;
        if (length < 0.01f) return;

        float t = (float)((Now - RunStart) / (length / speed));

        if (t < 0f) return;                           // el reloj local va apenas atrasado
        if (t >= 1f) { EndLocalRun(); return; }       // ya terminó

        if (!trainActive) BeginLocalRun(from, to, vec, t);

        Vector3 pos = Vector3.Lerp(from.position, to.position, t);
        train.position = pos;
        movingSource.transform.position = pos;
        hornSource.transform.position = pos;

        // Fade in al salir, fade out al llegar
        float distFromStart = t * length;
        float distToEnd = (1f - t) * length;
        float fade = Mathf.Clamp01(Mathf.Min(distFromStart, distToEnd) / (speed * fadeTime));
        movingSource.volume = fade * movingMaxVolume;

        // Bocina una sola vez al acercarse a la estación
        if (!hornPlayed && Vector3.Distance(pos, stationPoint.position) < hornDistance)
        {
            PlayHorn(RunHornIndex);
            hornPlayed = true;
        }
    }

    private void BeginLocalRun(Transform from, Transform to, Vector3 vec, float t)
    {
        trainActive = true;
        train.position = from.position;
        train.rotation = Quaternion.LookRotation(vec.normalized) * Quaternion.Euler(rotationOffset);
        train.gameObject.SetActive(true);

        // Si un cliente entra con el tren ya cerca de la estación, no suena la bocina de golpe
        Vector3 pos = Vector3.Lerp(from.position, to.position, t);
        hornPlayed = t > 0.05f && Vector3.Distance(pos, stationPoint.position) < hornDistance;

        int mi = RunMovingIndex;
        if (movingClips != null && mi >= 0 && mi < movingClips.Length)
        {
            movingSource.clip = movingClips[mi];
            movingSource.loop = true;
            movingSource.volume = 0f;
            movingSource.Play();
        }
    }

    private void EndLocalRun()
    {
        if (!trainActive) return;
        trainActive = false;
        movingSource.Stop();
        train.gameObject.SetActive(false);
    }

    private void PlayHorn(int index)
    {
        if (hornClips == null || index < 0 || index >= hornClips.Length) return;
        hornSource.pitch = Random.Range(hornPitchRange.x, hornPitchRange.y);
        hornSource.PlayOneShot(hornClips[index]);
    }
}