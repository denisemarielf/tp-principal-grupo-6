using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using TMPro;   // arriba del archivo
using UnityEngine.UI;




/// Controla el minijuego por niveles: asigna colores, cuenta conexiones, sonidos y efectos.
public class WireMinigame : MonoBehaviour
{
    [System.Serializable]
    public class Level
    {
        public int wires = 3;              // pares de cables activos (se limita a sources.Length)
        public float timeLimit = 0f;       // segundos; 0 = sin límite
        public int maxErrors = 0;          // 0 = sin límite; al llegar se reinicia el nivel
        public bool shuffleSources = false; // también mezcla la columna izquierda
        public float shuffleInterval = 0f;
    }

    [Header("Conectores (mismo largo en ambos arrays)")]
    public WireConnector[] sources;
    public WireConnector[] targets;
    public Color[] palette;            // al menos tantos colores como cables

    [Header("Niveles")]
    public Level[] levels =
    {
        new Level { wires = 3 },
        new Level { wires = 4, shuffleSources = true },
        new Level { wires = 5, shuffleSources = true, timeLimit = 30f, maxErrors = 3, shuffleInterval = 8f }
    };

    [Header("Sonido")]
    public AudioSource audioSource;
    public AudioClip connectClip;
    public AudioClip errorClip;
    public AudioClip completeClip;

    [Header("Efectos")]
    public ParticleSystem sparks;

    [Header("Al completar TODOS los niveles")]
    public UnityEvent onCompleted;

    int levelIndex, connectedCount, errors, activeCount;
    float timeLeft;
    float shuffleTimer;
    bool completed;
    
    


    TextMeshProUGUI timerText;

    /// Los conectores la consultan para ignorar arrastres durante transiciones.
    public bool Locked { get; private set; }

    int LevelCount => (levels != null && levels.Length > 0) ? levels.Length : 1;
    Level CurrentLevel => (levels != null && levels.Length > 0)
        ? levels[levelIndex]
        : new Level { wires = sources.Length };

    void Start() => Setup();

    /// Reinicia todo el minijuego desde el nivel 1 (lo llama ElectricPanel al abrir).
    public void Setup()
    {
        completed = false;
        StartLevel(0);
    }

    void StartLevel(int index)
    {
        CancelInvoke();

        if (sources.Length == 0 || targets.Length != sources.Length || palette.Length < sources.Length)
        {
            Debug.LogError($"WireMinigame mal configurado: sources={sources.Length}, targets={targets.Length}, palette={palette.Length}. " +
                           "sources y targets deben tener el mismo tamaño, y palette al menos ese tamaño.", this);
            return;
        }

        levelIndex = index;
        var lvl = CurrentLevel;
        activeCount = Mathf.Clamp(lvl.wires, 1, sources.Length);
        connectedCount = 0;
        errors = 0;
        timeLeft = lvl.timeLimit;
        shuffleTimer = lvl.shuffleInterval;
        Locked = false;

        // Colores distintos en cada ronda
        int[] colorIds = Enumerable.Range(0, palette.Length).OrderBy(_ => Random.value).Take(activeCount).ToArray();
        int[] targetOrder = Enumerable.Range(0, activeCount).OrderBy(_ => Random.value).ToArray();
        int[] sourceOrder = lvl.shuffleSources
            ? Enumerable.Range(0, activeCount).OrderBy(_ => Random.value).ToArray()
            : Enumerable.Range(0, activeCount).ToArray();

        for (int i = 0; i < sources.Length; i++)
        {
            if (i < activeCount)
            {
                int s = colorIds[sourceOrder[i]];
                int t = colorIds[targetOrder[i]];
                sources[i].Init(this, s, palette[s]);
                targets[i].Init(this, t, palette[t]);
            }
            else
            {
                sources[i].gameObject.SetActive(false);
                targets[i].gameObject.SetActive(false);
            }
        }
        EnsureTimerText();
        timerText.gameObject.SetActive(lvl.timeLimit > 0f);
        timerText.text = Mathf.CeilToInt(lvl.timeLimit).ToString();
        timerText.color = Color.white;
        
    }

    void Update()
    {
        if (completed || Locked || sources.Length == 0 || !sources[0].isActiveAndEnabled) return;
        var lvl = CurrentLevel;

        // Mezcla a mitad de nivel
        if (lvl.shuffleInterval > 0f)
        {
            shuffleTimer -= Time.deltaTime;
            if (shuffleTimer <= 0f)
            {
                ShuffleTargets();
                shuffleTimer = lvl.shuffleInterval;
            }
        }

        // Límite de tiempo
        if (lvl.timeLimit <= 0f) return;

        timeLeft -= Time.deltaTime;

        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, timeLeft)).ToString();
            timerText.color = timeLeft <= 5f ? Color.red : Color.white;
        }

        if (timeLeft <= 5f)
        {
            bool on = Mathf.PingPong(Time.time * 4f, 1f) > 0.5f;
            for (int i = 0; i < activeCount; i++)
            {
                sources[i].FlashLed(on);
                targets[i].FlashLed(on);
            }
        }

        if (timeLeft <= 0f) FailLevel();
    }

    public void OnWireConnected(Vector3 worldPos)
    {
        connectedCount++;
        Play(connectClip, 1f + 0.12f * connectedCount);

        if (sparks)
        {
            sparks.transform.position = worldPos;
            sparks.Play();
        }

        if (!completed && connectedCount >= activeCount)
        {
            Locked = true;
            if (levelIndex >= LevelCount - 1)
            {
                completed = true;
                Invoke(nameof(Finish), 0.4f);
            }
            else
            {
                Invoke(nameof(NextLevel), 0.6f);
            }
        }
    }

    public void PlayError()
    {
        errors++;
        Play(errorClip, Random.Range(0.9f, 1.05f));
        int max = CurrentLevel.maxErrors;
        if (max > 0 && errors >= max) FailLevel();
    }

    void FailLevel()
    {
        Play(errorClip, 0.7f);
        StartLevel(levelIndex);   // reinicia el nivel actual
    }

    void NextLevel()
    {
        Play(completeClip, 1f);
        StartLevel(levelIndex + 1);
    }

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
    void EnsureTimerText()
    {
        if (timerText != null) return;

        var parent = sources[0].transform.parent;
        var go = new GameObject("TimerText",
            typeof(RectTransform), typeof(Canvas), typeof(LayoutElement), typeof(TextMeshProUGUI));

        go.GetComponent<LayoutElement>().ignoreLayout = true;   // el Layout Group no lo mueve
        var cv = go.GetComponent<Canvas>();
        cv.overrideSorting = true;                              // siempre por encima
        cv.sortingOrder = 20;

        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        rt.sizeDelta = new Vector2(0f, 60f);

        timerText = go.GetComponent<TextMeshProUGUI>();
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.fontSize = 40;
        timerText.fontStyle = FontStyles.Bold;
        timerText.raycastTarget = false;
    }
    void ShuffleTargets()
    {
        var free = targets.Take(activeCount).Where(t => !t.connected).ToList();
        if (free.Count < 2) return;   // con menos de 2 libres no hay nada que intercambiar

        int a = Random.Range(0, free.Count);
        int b = Random.Range(0, free.Count - 1);
        if (b >= a) b++;              // garantiza que sea distinto de a

        free[a].SwapColorWith(free[b]);
        Play(errorClip, 1.3f);        // sonido de aviso (podés usar otro clip)
    }
      public WireConnector FindTargetNear(Vector2 screenPos, Camera cam)
    {
        float tolerance = Mathf.Min(Screen.width, Screen.height) * 0.07f; // ~7% del lado corto
        WireConnector best = null;
        float bestDist = tolerance;

        for (int i = 0; i < activeCount; i++)
        {
            var t = targets[i];
            if (t.connected) continue;
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, t.WorldCenter);
            float d = Vector2.Distance(sp, screenPos);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }
}
