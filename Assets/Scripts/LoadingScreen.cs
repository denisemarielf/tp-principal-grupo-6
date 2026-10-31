using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de carga de transición entre el lobby y el mapa.
///
/// USO: ponelo en un GameObject RAÍZ vacío de la escena del menú (ej. "_PantallaCarga").
/// Sobrevive al cambio de escena (DontDestroyOnLoad) y se activa sola cuando
/// Netcode empieza a cargar una escena (en el host y en todos los clientes).
/// No hace falta tocar el código del lobby.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [System.Serializable]
    public class MapaInfo
    {
        public string nombreEscena;      // nombre exacto de la escena (como en Build Settings)
        public string nombreMostrado;    // lo que se ve arriba a la izquierda
    }

    public static LoadingScreen Instance { get; private set; }

    [Header("Fondos (se elige uno al azar en cada carga)")]
    public Sprite[] fondos;

    [Header("Consejos (se elige uno al azar y rotan mientras carga)")]
    [TextArea(2, 4)]
    public string[] consejos =
    {
        "Mantén al grupo unido: un sobreviviente aislado dura poco.",
        "Los disparos atraen a los infectados; piensa antes de disparar.",
        "Guarda munición para los momentos críticos."
    };
    public float segundosPorConsejo = 7f;

    [Header("Nombres de mapa (escena -> nombre mostrado)")]
    public MapaInfo[] mapas =
    {
        new MapaInfo { nombreEscena = "SampleScene",     nombreMostrado = "SUBURBIO" },
        new MapaInfo { nombreEscena = "MapaOficina",     nombreMostrado = "COMPLEJO DE OFICINAS" },
        new MapaInfo { nombreEscena = "ExtraccionScene", nombreMostrado = "ESTACION DE TREN" }
    };

    [Header("Tipografía (arrastrá tus font assets TMP)")]
    public TMP_FontAsset fuenteTitulo;   // Bangers SDF
    public TMP_FontAsset fuenteTexto;    // Anton SDF

    [Header("Tiempos")]
    [Tooltip("La pantalla se queda al menos este tiempo, aunque la escena cargue más rápido.")]
    public float tiempoMinimo = 2f;
    [Tooltip("Seguro anti-cuelgue: si pasa este tiempo, se cierra igual.")]
    public float tiempoMaximo = 90f;
    [Tooltip("Velocidad máxima de llenado de la barra (1 = llenarse en 1 segundo).")]
    public float velocidadBarra = 0.5f;
    public float fundidoEntrada = 0.25f;
    public float fundidoSalida = 0.5f;

    // Estilo (mismo que el menú)
    static readonly Color Rojo = new Color(0.80f, 0.12f, 0.12f, 1f);
    static readonly Color Vidrio = new Color(0.03f, 0.035f, 0.04f, 0.58f);
    static readonly Color Borde = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color Gris = new Color(0.55f, 0.58f, 0.56f, 1f);
    static readonly Color ColorTexto = new Color(0.93f, 0.94f, 0.92f, 1f);

    // UI
    GameObject raiz;
    CanvasGroup grupo;
    Image imgFondo;
    TextMeshProUGUI txtMapa;
    TextMeshProUGUI txtConsejo;
    TextMeshProUGUI txtEstado;
    GameObject panelConsejo;
    RectTransform rellenoRect;

    // Estado
    bool visible;
    bool saliendo;
    bool esPrueba;
    bool todosTerminaron;
    float tiempo;
    float progresoMostrado;
    float temporizadorConsejo;
    int ultimoConsejo = -1;
    int ultimoPorcentaje = -1;
    bool ultimoLlena;
    AsyncOperation operacion;
    NetworkSceneManager suscrito;

    static Sprite ultimoFondo;
    static Sprite degradadoArriba;
    static Sprite degradadoAbajo;

    // ============================================================
    // CICLO DE VIDA
    // ============================================================

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Al volver al menú después de una partida, la copia nueva se descarta.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        ConstruirUI();
        raiz.SetActive(false);
    }

    void OnDestroy()
    {
        if (suscrito != null)
            suscrito.OnSceneEvent -= AlEventoDeEscena;

        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        VigilarNetcode();

        if (!visible)
            return;

        float dt = Time.unscaledDeltaTime;
        tiempo += dt;

        // Progreso real (0..1)
        float real;
        if (todosTerminaron)
            real = 1f;
        else if (operacion != null)
            real = operacion.isDone ? 1f : Mathf.Clamp01(operacion.progress / 0.9f);
        else
            real = Mathf.Min(0.9f, tiempo * 0.3f);

        progresoMostrado = Mathf.MoveTowards(progresoMostrado, real, velocidadBarra * dt);
        bool llena = progresoMostrado >= 0.999f;
        ActualizarBarra(llena);

        // Consejos rotativos
        temporizadorConsejo += dt;
        if (temporizadorConsejo >= segundosPorConsejo)
        {
            temporizadorConsejo = 0f;
            ElegirConsejo();
        }

        // Cierre
        if (!saliendo)
        {
            bool listo = todosTerminaron && llena && tiempo >= tiempoMinimo;

            NetworkManager nm = NetworkManager.Singleton;
            bool redCaida = !esPrueba && (nm == null || !nm.IsListening);

            if (listo || redCaida || tiempo >= tiempoMaximo)
                saliendo = true;
        }

        float duracion = Mathf.Max(0.01f, saliendo ? fundidoSalida : fundidoEntrada);
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, saliendo ? 0f : 1f, dt / duracion);

        if (saliendo && grupo.alpha <= 0f)
            Ocultar();
    }

    // ============================================================
    // NETCODE
    // ============================================================

    // El NetworkSceneManager se crea cada vez que arranca la red,
    // así que nos (re)suscribimos cuando cambia.
    void VigilarNetcode()
    {
        NetworkManager nm = NetworkManager.Singleton;
        NetworkSceneManager actual = nm != null ? nm.SceneManager : null;

        if (actual == suscrito)
            return;

        if (suscrito != null)
            suscrito.OnSceneEvent -= AlEventoDeEscena;

        suscrito = actual;

        if (suscrito != null)
            suscrito.OnSceneEvent += AlEventoDeEscena;
    }

    void AlEventoDeEscena(SceneEvent e)
    {
        if (e.LoadSceneMode != LoadSceneMode.Single)
            return;

        switch (e.SceneEventType)
        {
            case SceneEventType.Load:
                if (!visible || saliendo)
                    Mostrar(e.SceneName, false);

                if (e.AsyncOperation != null)
                    operacion = e.AsyncOperation;
                break;

            case SceneEventType.LoadEventCompleted:
                // Todos los clientes terminaron de cargar.
                todosTerminaron = true;
                break;
        }
    }

    // ============================================================
    // MOSTRAR / OCULTAR
    // ============================================================

    void Mostrar(string escena, bool prueba)
    {
        esPrueba = prueba;
        operacion = null;
        todosTerminaron = false;
        saliendo = false;
        tiempo = 0f;
        progresoMostrado = 0f;
        temporizadorConsejo = 0f;
        ultimoPorcentaje = -1;

        txtMapa.text = ResolverNombre(escena);
        ElegirFondo();
        ElegirConsejo();
        ActualizarBarra(false);

        if (!visible)
            grupo.alpha = 0f;

        raiz.SetActive(true);
        visible = true;
    }

    void Ocultar()
    {
        visible = false;
        saliendo = false;
        operacion = null;
        raiz.SetActive(false);
    }

    [ContextMenu("Probar pantalla de carga (en Play)")]
    void Probar()
    {
        if (!Application.isPlaying)
            return;

        string escena = (mapas != null && mapas.Length > 0) ? mapas[0].nombreEscena : "Mapa";
        Mostrar(escena, true);
        todosTerminaron = true;
    }

    // ============================================================
    // CONTENIDO
    // ============================================================

    string ResolverNombre(string escena)
    {
        if (mapas != null)
        {
            foreach (MapaInfo m in mapas)
            {
                if (m != null &&
                    string.Equals(m.nombreEscena, escena, System.StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(m.nombreMostrado))
                {
                    return m.nombreMostrado.ToUpperInvariant();
                }
            }
        }

        return string.IsNullOrEmpty(escena) ? "" : escena.Replace('_', ' ').ToUpperInvariant();
    }

    void ElegirFondo()
    {
        var validos = new List<Sprite>();
        if (fondos != null)
        {
            foreach (Sprite s in fondos)
                if (s != null) validos.Add(s);
        }

        imgFondo.gameObject.SetActive(validos.Count > 0);
        if (validos.Count == 0)
            return;

        Sprite elegido = validos[UnityEngine.Random.Range(0, validos.Count)];

        // Evitar repetir la misma imagen dos cargas seguidas.
        if (validos.Count > 1 && elegido == ultimoFondo)
        {
            int i = validos.IndexOf(elegido);
            elegido = validos[(i + 1) % validos.Count];
        }

        ultimoFondo = elegido;
        imgFondo.sprite = elegido;

        var fit = imgFondo.GetComponent<AspectRatioFitter>();
        fit.aspectRatio = elegido.rect.width / Mathf.Max(1f, elegido.rect.height);
    }

    void ElegirConsejo()
    {
        bool hay = consejos != null && consejos.Length > 0;
        panelConsejo.SetActive(hay);
        if (!hay)
            return;

        int i = UnityEngine.Random.Range(0, consejos.Length);
        if (consejos.Length > 1 && i == ultimoConsejo)
            i = (i + 1) % consejos.Length;

        ultimoConsejo = i;
        txtConsejo.text = consejos[i];
    }

    void ActualizarBarra(bool llena)
    {
        rellenoRect.anchorMax = new Vector2(Mathf.Clamp01(progresoMostrado), 1f);

        int pct = Mathf.RoundToInt(progresoMostrado * 100f);
        bool espera = llena && !todosTerminaron;

        if (pct == ultimoPorcentaje && espera == ultimoLlena)
            return;

        ultimoPorcentaje = pct;
        ultimoLlena = espera;

        txtEstado.text = espera
            ? "ESPERANDO A LOS DEMÁS JUGADORES"
            : "CARGANDO  " + pct + "%";
    }

    // ============================================================
    // CONSTRUCCIÓN DE LA UI
    // ============================================================

    void ConstruirUI()
    {
        raiz = new GameObject(
            "Canvas_PantallaCarga",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        raiz.transform.SetParent(transform, false);

        Canvas canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = raiz.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        grupo = raiz.GetComponent<CanvasGroup>();

        // Fondo negro (también bloquea clics hacia lo que haya detrás)
        Image negro = CrearImagen("Fondo_Negro", raiz.transform, Color.black);
        Estirar(negro.rectTransform);
        negro.raycastTarget = true;

        // Imagen de fondo: cubre toda la pantalla sin deformarse
        imgFondo = CrearImagen("Imagen_Fondo", raiz.transform, Color.white);
        RectTransform rtFondo = imgFondo.rectTransform;
        Anclar(rtFondo, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AspectRatioFitter fit = imgFondo.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 16f / 9f;

        // Degradados para que el texto se lea sobre cualquier imagen
        Image sup = CrearImagen("Degradado_Superior", raiz.transform, new Color(0f, 0f, 0f, 0.85f));
        sup.sprite = Degradado(true);
        Anclar(sup.rectTransform, V(0, 1), V(1, 1), V(0.5f, 1), Vector2.zero, V(0, 360));

        Image inf = CrearImagen("Degradado_Inferior", raiz.transform, new Color(0f, 0f, 0f, 0.90f));
        inf.sprite = Degradado(false);
        Anclar(inf.rectTransform, V(0, 0), V(1, 0), V(0.5f, 0), Vector2.zero, V(0, 420));

        // ---------- Arriba a la izquierda: nombre del mapa ----------
        TextMeshProUGUI etiqueta = CrearTexto("Etiqueta", raiz.transform, "CARGANDO MAPA",
            22f, Gris, TextAlignmentOptions.TopLeft, fuenteTexto, 4f);
        Anclar(etiqueta.rectTransform, V(0, 1), V(0, 1), V(0, 1), V(120, -70), V(900, 34));

        txtMapa = CrearTexto("Nombre_Mapa", raiz.transform, "",
            88f, Color.white, TextAlignmentOptions.TopLeft, fuenteTitulo, 1f);
        Anclar(txtMapa.rectTransform, V(0, 1), V(0, 1), V(0, 1), V(120, -104), V(1400, 120));

        Image lineaRoja = CrearImagen("Linea_Roja", raiz.transform, Rojo);
        Anclar(lineaRoja.rectTransform, V(0, 1), V(0, 1), V(0, 1), V(120, -218), V(100, 4));

        // ---------- Abajo a la izquierda: consejos ----------
        panelConsejo = new GameObject("Panel_Consejo", typeof(RectTransform), typeof(Image));
        panelConsejo.transform.SetParent(raiz.transform, false);
        Image imgPanel = panelConsejo.GetComponent<Image>();
        imgPanel.color = Vidrio;
        imgPanel.raycastTarget = false;
        Anclar((RectTransform)panelConsejo.transform, V(0, 0), V(0, 0), V(0, 0), V(120, 80), V(760, 200));
        AgregarBorde(panelConsejo.transform);

        TextMeshProUGUI cabecera = CrearTexto("Cabecera", panelConsejo.transform, "CONSEJO",
            20f, Rojo, TextAlignmentOptions.TopLeft, fuenteTexto, 4f);
        Anclar(cabecera.rectTransform, V(0, 1), V(1, 1), V(0, 1), V(32, -24), V(-64, 28));

        txtConsejo = CrearTexto("Texto_Consejo", panelConsejo.transform, "",
            26f, ColorTexto, TextAlignmentOptions.TopLeft, fuenteTexto, 0.5f);
        txtConsejo.textWrappingMode = TextWrappingModes.Normal;
        txtConsejo.lineSpacing = 4f;
        RectTransform rtConsejo = txtConsejo.rectTransform;
        rtConsejo.anchorMin = V(0, 0);
        rtConsejo.anchorMax = V(1, 1);
        rtConsejo.pivot = V(0.5f, 0.5f);
        rtConsejo.offsetMin = V(32, 24);
        rtConsejo.offsetMax = V(-32, -62);

        // ---------- Abajo a la derecha: barra de carga ----------
        RectTransform bloque = Crear("Bloque_Barra", raiz.transform);
        Anclar(bloque, V(1, 0), V(1, 0), V(1, 0), V(-120, 80), V(560, 64));

        txtEstado = CrearTexto("Estado", bloque, "",
            22f, ColorTexto, TextAlignmentOptions.TopRight, fuenteTexto, 3f);
        Anclar(txtEstado.rectTransform, V(0, 1), V(1, 1), V(0, 1), Vector2.zero, V(0, 34));

        Image fondoBarra = CrearImagen("Barra_Fondo", bloque, new Color(1f, 1f, 1f, 0.12f));
        Anclar(fondoBarra.rectTransform, V(0, 0), V(1, 0), V(0, 0), Vector2.zero, V(0, 8));

        Image relleno = CrearImagen("Barra_Relleno", fondoBarra.transform, Rojo);
        rellenoRect = relleno.rectTransform;
        rellenoRect.anchorMin = V(0, 0);
        rellenoRect.anchorMax = V(0, 1);
        rellenoRect.offsetMin = Vector2.zero;
        rellenoRect.offsetMax = Vector2.zero;
    }

    // ---------- helpers de UI ----------

    static Vector2 V(float x, float y) => new Vector2(x, y);

    static RectTransform Crear(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anclar(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                       Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        RectTransform rt = Crear(nombre, padre);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI CrearTexto(string nombre, Transform padre, string texto, float tam,
                               Color color, TextAlignmentOptions alineacion,
                               TMP_FontAsset fuente, float espaciado)
    {
        RectTransform rt = Crear(nombre, padre);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();

        if (fuente != null)
            t.font = fuente;

        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.alignment = alineacion;
        t.characterSpacing = espaciado;
        t.raycastTarget = false;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // Borde fino de 1 px + acento rojo en la esquina superior izquierda
    static void AgregarBorde(Transform padre)
    {
        Linea(padre, V(0, 1), V(1, 1), V(0, -1), V(0, 0), Borde);   // arriba
        Linea(padre, V(0, 0), V(1, 0), V(0, 0), V(0, 1), Borde);    // abajo
        Linea(padre, V(0, 0), V(0, 1), V(0, 0), V(1, 0), Borde);    // izquierda
        Linea(padre, V(1, 0), V(1, 1), V(-1, 0), V(0, 0), Borde);   // derecha
        Linea(padre, V(0, 1), V(0, 1), V(0, -3), V(72, 0), Rojo);   // acento
    }

    static void Linea(Transform padre, Vector2 aMin, Vector2 aMax,
                      Vector2 oMin, Vector2 oMax, Color color)
    {
        Image img = CrearImagen("Borde", padre, color);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }

    // Sprite 1x256 con alpha vertical suavizado.
    static Sprite Degradado(bool opacoArriba)
    {
        Sprite cache = opacoArriba ? degradadoArriba : degradadoAbajo;
        if (cache != null)
            return cache;

        const int alto = 256;
        var tex = new Texture2D(1, alto, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[alto];
        for (int i = 0; i < alto; i++)
        {
            float t = i / (float)(alto - 1);            // 0 abajo, 1 arriba
            float a = opacoArriba ? t : 1f - t;
            a = a * a * (3f - 2f * a);                  // smoothstep
            px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }

        tex.SetPixels32(px);
        tex.Apply();

        cache = Sprite.Create(tex, new Rect(0, 0, 1, alto), new Vector2(0.5f, 0.5f), 100f);

        if (opacoArriba) degradadoArriba = cache;
        else degradadoAbajo = cache;

        return cache;
    }
}