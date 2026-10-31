using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Serialization;


public class GeneradorMenuPrincipal : MonoBehaviour
{
    // ============================================================
    // CONFIGURACIÓN
    // ============================================================

    [Header("Configuración de Escenas (3 Mapas)")]
    public string nombreEscenaMapa1 = "SampleScene";
    public string nombreEscenaMapa2 = "MapaOficina";
    public string nombreEscenaMapa3 = "TestConexion";

    [Header("Objetivos de los Mapas")]
    [TextArea(2, 5)]
    public string objetivoMapa1 =
        "Recorre el suburbio, encuentra suministros y sobrevive el tiempo suficiente para alcanzar el punto de salida.";

    [TextArea(2, 5)]
    public string objetivoMapa2 =
        "Avanza por la zona urbana, busca recursos y encuentra una ruta segura para escapar del complejo de oficinas.";

    [TextArea(2, 5)]
    public string objetivoMapa3 =
        "Estacion de tren. Modo Supervivencia: defende el refugio frente a oleadas. La victoria es sobrevivir el tiempo con al menos un civil vivo.";

    [Header("Ambientación y Arte")]
    public Sprite imagenFondoCustom;

    [Header("Tipografía")]
    [Tooltip("TMP Font Asset para los TÍTULOS. Recomendado: fuente stencil / militar (Black Ops One, Saira Stencil One, Stardos Stencil).")]
    public TMP_FontAsset fuenteTitulo;

    [Tooltip("TMP Font Asset para botones y textos. Recomendado: fuente condensada y legible (Rajdhani Bold, Teko, Oswald).")]
    public TMP_FontAsset fuenteTexto;

    [Tooltip("Escala general de la tipografía.")]
    [Range(0.7f, 1.5f)]
    [FormerlySerializedAs("escalaFuenteRetro")]
    public float escalaFuente = 1.0f;

    [Header("Diseño (px sobre un canvas de 1920x1080)")]
    [Tooltip("Distancia desde el borde izquierdo de la pantalla. Todo el menú se alinea a esta línea.")]
    public float margenIzquierdo = 120f;

    [Tooltip("Distancia desde el borde superior hasta el título.")]
    public float margenSuperior = 70f;

    [Tooltip("Ancho de las tarjetas con opciones (crear sala, unirse, opciones).")]
    public float anchoTarjeta = 600f;

    [Header("Audio y Efectos Sonoros")]
    public AudioClip musicaFondo;
    public AudioClip sfxHover;
    public AudioClip sfxClick;

    private int personajeSeleccionado = 0;
    private TMP_Text txtBtnPersonajeC;
    private TMP_Text txtBtnPersonajeU;
    [SerializeField] private CharacterPreview characterPreview;

    // ============================================================
    // PANELES
    // ============================================================

    private GameObject panelPrincipal;
    private GameObject panelOpciones;
    private GameObject panelSeleccionRed;
    private GameObject panelCrearSala;
    private GameObject panelUnirseSala;
    private GameObject panelLobby;
    private GameObject panelLogros;


    // ============================================================
    // ELEMENTOS DINÁMICOS DE UI
    // ============================================================

    private TextMeshProUGUI txtDetallesLobby;

    private TextMeshProUGUI[] listaRanurasJugadores =
        new TextMeshProUGUI[4];

    private TMP_InputField inputCodigoSala;
    private TMP_InputField inputNombreHost;
    private TMP_InputField inputNombreCliente;

    // Solo el host puede iniciar la partida; los clientes no ven el botón.
    private Button btnIniciarPartida;


    // ============================================================
    // ESTADO DE RED
    // ============================================================

    private string codigoSala;

    // Se incrementa al salir del lobby, para descartar una creación/conexión
    // que todavía estaba en curso cuando el jugador se fue.
    private int intentoConexion;

    private bool suscritoARed;


    // Nombres en el lobby
    private const string MsgNombre = "LobbyNombre";   // cliente -> host
    private const string MsgLista = "LobbyLista";     // host -> todos

    private readonly Dictionary<ulong, string> nombresLobby =
        new Dictionary<ulong, string>();              // solo lo usa el host

    private readonly List<string> nombresLobbyActuales =
        new List<string>();                           // lo que se muestra en pantalla

    private bool mensajesRegistrados;
    //


    // ============================================================
    // DATOS SELECCIONADOS
    // ============================================================

    private int mapaSeleccionado = 0;
    private int dificultadSeleccionada = 1;

    private TextMeshProUGUI txtBtnMapa;
    private TextMeshProUGUI txtBtnDificultad;

    // Descripción del objetivo del mapa seleccionado
    private TextMeshProUGUI txtObjetivoMapa;


    // ============================================================
    // AUDIO
    // ============================================================

    private AudioSource audioSourceMusica;
    private AudioSource audioSourceSFX;


    // ============================================================
    // COLORES
    // ============================================================

    private Color colorRojoSangre = new Color(0.80f, 0.12f, 0.12f, 1f);
    private Color colorRojoHover = new Color(0.95f, 0.25f, 0.15f, 1f);
    private Color colorVerdeZombie = new Color(0.2f, 0.85f, 0.3f, 1f);   // lo usa el lobby
    private Color colorAmbar = new Color(0.95f, 0.78f, 0.28f, 1f);

    private Color colorTexto = new Color(0.93f, 0.94f, 0.92f, 1f);
    private Color colorTextoSuave = new Color(0.68f, 0.71f, 0.70f, 1f);
    private Color colorTextoApagado = new Color(0.52f, 0.55f, 0.54f, 1f);

    // "Vidrio": paneles semitransparentes con borde fino.
    private Color colorVidrio = new Color(0.03f, 0.035f, 0.04f, 0.58f);
    private Color colorBordePanel = new Color(1f, 1f, 1f, 0.10f);

    // Degradados generados por código (no hace falta ningún asset).
    private Sprite spriteDegradadoSuave;    // alpha 1 -> 0 lineal
    private Sprite spriteDegradadoFuerte;   // alpha 1 -> 0 curvado (oscurece el lado izquierdo)


    // ============================================================
    // AWAKE
    // ============================================================

    void Awake()
    {
        InicializarAudio();
        ConstruirInterfazCompleta();
    }


    void OnDestroy()
    {
        DesuscribirDeRed();
    }


    // ============================================================
    // AUDIO
    // ============================================================

    private void InicializarAudio()
    {
        audioSourceMusica = gameObject.AddComponent<AudioSource>();
        audioSourceMusica.loop = true;
        audioSourceMusica.playOnAwake = false;

        audioSourceSFX = gameObject.AddComponent<AudioSource>();
        audioSourceSFX.loop = false;
        audioSourceSFX.playOnAwake = false;

        if (musicaFondo == null)
            musicaFondo = Resources.Load<AudioClip>("MusicaMenu");

        if (sfxHover == null)
            sfxHover = Resources.Load<AudioClip>("SFX_Hover");

        if (sfxClick == null)
            sfxClick = Resources.Load<AudioClip>("SFX_Click");

        if (musicaFondo != null)
        {
            audioSourceMusica.clip = musicaFondo;
            audioSourceMusica.Play();
        }
    }


    public void ReproducirSFX(AudioClip clip)
    {
        if (clip != null && audioSourceSFX != null)
        {
            audioSourceSFX.PlayOneShot(clip);
        }
    }

    // ============================================================
    // CONSTRUIR INTERFAZ
    // ============================================================

    // Y donde empieza el contenido (botones / tarjetas), debajo del título.
    private float InicioContenidoY => margenSuperior + 240f;

    private void ConstruirInterfazCompleta()
    {
        spriteDegradadoSuave = CrearSpriteDegradadoHorizontal(1f);
        spriteDegradadoFuerte = CrearSpriteDegradadoHorizontal(1.6f);

        // ========================================================
        // 1. CANVAS
        // ========================================================

        GameObject canvasObj = new GameObject("Canvas_Menu");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // pixelPerfect desactivado: con tipografía moderna suavizada
        // y animaciones de hover se ve borroso / tiembla.
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();


        // ========================================================
        // 2. EVENT SYSTEM
        // ========================================================

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }


        // ========================================================
        // 3. FONDO
        // ========================================================

        GameObject fondoObj = new GameObject(
            "Fondo_Villa", typeof(RectTransform), typeof(Image));
        fondoObj.transform.SetParent(canvasObj.transform, false);

        Image imgFondo = fondoObj.GetComponent<Image>();
        imgFondo.raycastTarget = false;
        RectTransform rtFondo = fondoObj.GetComponent<RectTransform>();

        // Prioridad igual que antes: Resources/FondoMenu y, si no existe,
        // la imagen asignada en el Inspector.
        Sprite fondoAuto = Resources.Load<Sprite>("FondoMenu");
        Sprite spriteFondo = fondoAuto != null ? fondoAuto : imagenFondoCustom;

        if (spriteFondo != null)
        {
            imgFondo.sprite = spriteFondo;
            imgFondo.color = Color.white;
            imgFondo.preserveAspect = false;

            // La imagen cubre toda la pantalla SIN deformarse
            // (recorta lo que sobre según la relación de aspecto).
            rtFondo.anchorMin = new Vector2(0.5f, 0.5f);
            rtFondo.anchorMax = new Vector2(0.5f, 0.5f);
            rtFondo.pivot = new Vector2(0.5f, 0.5f);
            rtFondo.anchoredPosition = Vector2.zero;

            AspectRatioFitter ajuste = fondoObj.AddComponent<AspectRatioFitter>();
            ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            ajuste.aspectRatio = spriteFondo.rect.width / spriteFondo.rect.height;
        }
        else
        {
            imgFondo.color = new Color(0.04f, 0.045f, 0.05f, 1f);
            Estirar(rtFondo);
        }


        // ========================================================
        // 4. CAPAS DE OSCURECIDO (legibilidad del menú a la izquierda)
        // ========================================================

        // Velo general suave
        GameObject veloObj = new GameObject(
            "Velo_General", typeof(RectTransform), typeof(Image));
        veloObj.transform.SetParent(canvasObj.transform, false);
        Image imgVelo = veloObj.GetComponent<Image>();
        imgVelo.color = new Color(0f, 0f, 0f, 0.22f);
        imgVelo.raycastTarget = false;
        Estirar(veloObj.GetComponent<RectTransform>());

        // Degradado fuerte desde el borde izquierdo
        GameObject degObj = new GameObject(
            "Degradado_Izquierda", typeof(RectTransform), typeof(Image));
        degObj.transform.SetParent(canvasObj.transform, false);
        Image imgDeg = degObj.GetComponent<Image>();
        imgDeg.sprite = spriteDegradadoFuerte;
        imgDeg.color = new Color(0.01f, 0.012f, 0.015f, 0.92f);
        imgDeg.raycastTarget = false;
        RectTransform rtDeg = degObj.GetComponent<RectTransform>();
        rtDeg.anchorMin = new Vector2(0f, 0f);
        rtDeg.anchorMax = new Vector2(0.78f, 1f);
        rtDeg.offsetMin = Vector2.zero;
        rtDeg.offsetMax = Vector2.zero;


        // ========================================================
        // 5. PANEL PRINCIPAL
        // ========================================================

        panelPrincipal = CrearPanelContenedor(canvasObj, "Panel_Principal");

        CrearTitulo(
            panelPrincipal.transform,
            "ULTIMO TREN A RETIRO",
            "SOBREVIVE EN EL CONURBANO",
            true
        );

        GameObject colPrincipal = CrearColumna(
            panelPrincipal.transform,
            margenIzquierdo,
            InicioContenidoY + 10f,
            520f,
            8f
        );

        CrearBotonMenu(colPrincipal.transform, "JUGAR", AbrirSeleccionRed, EstiloBoton.Primario, true);
        CrearBotonMenu(colPrincipal.transform, "LOGROS", AbrirLogros, EstiloBoton.Normal, true);
        CrearBotonMenu(colPrincipal.transform, "OPCIONES", AbrirOpciones, EstiloBoton.Normal, true);
        CrearBotonMenu(colPrincipal.transform, "SALIR", Salir, EstiloBoton.Secundario, true);


        // ========================================================
        // 6. SELECCIÓN DE RED
        // ========================================================

        panelSeleccionRed = CrearPanelContenedor(canvasObj, "Panel_SeleccionRed");

        CrearTitulo(
            panelSeleccionRed.transform,
            "MULTIJUGADOR",
            "SELECCIONA UNA OPCIÓN"
        );

        GameObject colRed = CrearColumna(
            panelSeleccionRed.transform,
            margenIzquierdo,
            InicioContenidoY + 10f,
            520f,
            8f
        );

        CrearBotonMenu(colRed.transform, "CREAR SALA", AbrirCrearSala, EstiloBoton.Primario, true);
        CrearBotonMenu(colRed.transform, "UNIRSE A SALA", AbrirUnirseSala, EstiloBoton.Normal, true);
        CrearBotonMenu(colRed.transform, "VOLVER", VolverAMenuPrincipal, EstiloBoton.Secundario, true);


        // ========================================================
        // 7. CREAR SALA
        // ========================================================

        panelCrearSala = CrearPanelContenedor(canvasObj, "Panel_CrearSala");

        CrearTitulo(
            panelCrearSala.transform,
            "CREAR PARTIDA",
            "CONFIGURACIÓN DE LA SALA"
        );

        GameObject tarjetaCrear = CrearTarjeta(
            panelCrearSala.transform,
            margenIzquierdo,
            InicioContenidoY,
            anchoTarjeta
        );

        inputNombreHost = CrearCampoTexto(
            tarjetaCrear.transform,
            "NOMBRE DEL JUGADOR",
            "Host_Zombie"
        );

        // MAPA
        txtBtnMapa = CrearBotonCiclico(
            tarjetaCrear.transform,
            "MAPA: SUBURBIO",
            () =>
            {
                mapaSeleccionado = (mapaSeleccionado + 1) % 3;

                string[] mapas =
                {
                    "MAPA: SUBURBIO",
                    "MAPA: COMPLEJO DE OFICINAS",
                    "MAPA: ESTACION DE TREN"
                };

                txtBtnMapa.text = mapas[mapaSeleccionado];
                ActualizarObjetivoMapa();
            }
        );

        // PERSONAJE (la vista previa va a la derecha de la pantalla)
        RawImage raw = CrearVistaPreviewPersonaje(panelCrearSala.transform);

        if (characterPreview != null)
        {
            characterPreview.Display = raw;
            characterPreview.Show(personajeSeleccionado);
        }

        PlayerModelChoice.SelectedIndex = personajeSeleccionado;   // la opcion mostrada al abrir tambien cuenta

        txtBtnPersonajeC = CrearBotonCiclico(
            tarjetaCrear.transform,
            "PERSONAJE: MASCARA GAS",
            () =>
            {
                personajeSeleccionado = (personajeSeleccionado + 1) % 4;
                PlayerModelChoice.SelectedIndex = personajeSeleccionado;

                if (characterPreview != null)
                    characterPreview.Show(personajeSeleccionado);

                string[] personajes =
                {
                    "PERSONAJE: MASCARA GAS",
                    "PERSONAJE: SWAT",
                    "PERSONAJE: SOLDADO",
                    "PERSONAJE: TRABAJADOR"
                };

                if (txtBtnPersonajeU != null) txtBtnPersonajeU.text = personajes[personajeSeleccionado];
                txtBtnPersonajeC.text = personajes[personajeSeleccionado];
            }
        );

        // Tarjeta con el objetivo del mapa, a la derecha del formulario.
        txtObjetivoMapa = CrearTextoObjetivoMapa(
            panelCrearSala.transform,
            margenIzquierdo + anchoTarjeta + 40f,
            InicioContenidoY
        );

        ActualizarObjetivoMapa();

        // DIFICULTAD
        txtBtnDificultad = CrearBotonCiclico(
            tarjetaCrear.transform,
            "DIFICULTAD: NORMAL",
            () =>
            {
                dificultadSeleccionada = (dificultadSeleccionada + 1) % 3;

                string[] difs =
                {
                    "DIFICULTAD: FÁCIL",
                    "DIFICULTAD: NORMAL",
                    "DIFICULTAD: DIFÍCIL"
                };

                txtBtnDificultad.text = difs[dificultadSeleccionada];
            }
        );

        CrearEspaciador(tarjetaCrear.transform, 6f);

        CrearBotonMenu(tarjetaCrear.transform, "CONFIRMAR Y CREAR", ConfirmarCrearSala, EstiloBoton.Primario);
        CrearBotonMenu(tarjetaCrear.transform, "VOLVER", AbrirSeleccionRed, EstiloBoton.Secundario);


        // ========================================================
        // 8. UNIRSE A SALA
        // ========================================================

        panelUnirseSala = CrearPanelContenedor(canvasObj, "Panel_UnirseSala");

        CrearTitulo(
            panelUnirseSala.transform,
            "UNIRSE A SALA",
            "CONEXIÓN POR CÓDIGO DE SALA"
        );

        GameObject tarjetaUnirse = CrearTarjeta(
            panelUnirseSala.transform,
            margenIzquierdo,
            InicioContenidoY,
            anchoTarjeta
        );

        inputNombreCliente = CrearCampoTexto(
            tarjetaUnirse.transform,
            "TU NOMBRE",
            "Sobreviviente"
        );

        // PERSONAJE
        RawImage rawU = CrearVistaPreviewPersonaje(panelUnirseSala.transform);

        if (characterPreview != null)
        {
            characterPreview.Display = rawU;
            characterPreview.Show(personajeSeleccionado);
        }

        PlayerModelChoice.SelectedIndex = personajeSeleccionado;

        txtBtnPersonajeU = CrearBotonCiclico(
            tarjetaUnirse.transform,
            "PERSONAJE: MASCARA GAS",
            () =>
            {
                personajeSeleccionado = (personajeSeleccionado + 1) % 4;
                PlayerModelChoice.SelectedIndex = personajeSeleccionado;

                if (characterPreview != null)
                    characterPreview.Show(personajeSeleccionado);

                string[] personajes =
                {
                    "PERSONAJE: MASCARA GAS",
                    "PERSONAJE: SWAT",
                    "PERSONAJE: SOLDADO",
                    "PERSONAJE: TRABAJADOR"
                };

                txtBtnPersonajeU.text = personajes[personajeSeleccionado];
                if (txtBtnPersonajeC != null) txtBtnPersonajeC.text = personajes[personajeSeleccionado];
            }
        );

        inputCodigoSala = CrearCampoTexto(
            tarjetaUnirse.transform,
            "CÓDIGO DE SALA",
            ""
        );

        CrearEspaciador(tarjetaUnirse.transform, 6f);

        CrearBotonMenu(tarjetaUnirse.transform, "CONECTARSE", ConfirmarUnirseSala, EstiloBoton.Primario);
        CrearBotonMenu(tarjetaUnirse.transform, "VOLVER", AbrirSeleccionRed, EstiloBoton.Secundario);


        // ========================================================
        // 9. LOBBY
        // ========================================================

        panelLobby = CrearPanelContenedor(canvasObj, "Panel_Lobby");

        CrearTitulo(
            panelLobby.transform,
            "LOBBY DE ESPERA",
            "ESPERANDO SOBREVIVIENTES (MÁX 4)"
        );

        // Un poco más ancha: la línea de detalles es larga.
        GameObject tarjetaLobby = CrearTarjeta(
            panelLobby.transform,
            margenIzquierdo,
            InicioContenidoY,
            anchoTarjeta + 80f
        );

        // DETALLES (código de sala + mapa / dificultad)
        txtDetallesLobby = CrearTexto(
            tarjetaLobby.transform,
            "Texto_DetallesLobby",
            "ESPERANDO...",
            22f,
            colorAmbar,
            TextAlignmentOptions.MidlineLeft,
            false,
            1.5f
        );

        FijarAltura(txtDetallesLobby.gameObject, 64f);

        CrearEspaciador(tarjetaLobby.transform, 4f);

        // SLOTS
        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj = new GameObject(
                "Slot_Jugador_" + (i + 1),
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            slotObj.transform.SetParent(tarjetaLobby.transform, false);
            FijarAltura(slotObj, 54f);

            Image imgSlot = slotObj.GetComponent<Image>();
            imgSlot.sprite = spriteDegradadoSuave;
            imgSlot.color = new Color(1f, 1f, 1f, 0.07f);
            imgSlot.raycastTarget = false;

            GameObject marcaObj = new GameObject(
                "Marca", typeof(RectTransform), typeof(Image));
            marcaObj.transform.SetParent(slotObj.transform, false);
            Image imgMarca = marcaObj.GetComponent<Image>();
            imgMarca.color = new Color(1f, 1f, 1f, 0.28f);
            imgMarca.raycastTarget = false;
            RectTransform rtMarca = marcaObj.GetComponent<RectTransform>();
            rtMarca.anchorMin = new Vector2(0f, 0f);
            rtMarca.anchorMax = new Vector2(0f, 1f);
            rtMarca.pivot = new Vector2(0f, 0.5f);
            rtMarca.anchoredPosition = Vector2.zero;
            rtMarca.sizeDelta = new Vector2(3f, -14f);

            listaRanurasJugadores[i] = CrearTexto(
                slotObj.transform,
                "Texto_Nombre",
                (i == 0) ? "1. Esperando Host..." : $"{i + 1}. [ Ranura Vacía ]",
                24f,
                (i == 0) ? colorVerdeZombie : new Color(0.5f, 0.5f, 0.5f),
                TextAlignmentOptions.MidlineLeft,
                false,
                1f
            );

            RectTransform rtTxtSlot = listaRanurasJugadores[i].rectTransform;
            rtTxtSlot.anchorMin = Vector2.zero;
            rtTxtSlot.anchorMax = Vector2.one;
            rtTxtSlot.offsetMin = new Vector2(24f, 0f);
            rtTxtSlot.offsetMax = new Vector2(-16f, 0f);
        }

        CrearEspaciador(tarjetaLobby.transform, 8f);

        // BOTONES LOBBY
        btnIniciarPartida = CrearBotonMenu(
            tarjetaLobby.transform,
            "INICIAR PARTIDA",
            IniciarPartidaDesdeLobby,
            EstiloBoton.Primario
        );

        CrearBotonMenu(
            tarjetaLobby.transform,
            "SALIR DEL LOBBY",
            SalirDelLobby,
            EstiloBoton.Secundario
        );


        // ========================================================
        // 10. OPCIONES
        // ========================================================

        InicializarOpcionesVideo();

        panelOpciones = CrearPanelContenedor(canvasObj, "Panel_Opciones");

        CrearTitulo(
            panelOpciones.transform,
            "CONFIGURACIÓN",
            "AJUSTES DEL JUEGO"
        );

        GameObject tarjetaOpciones = CrearTarjeta(
            panelOpciones.transform,
            margenIzquierdo,
            InicioContenidoY,
            anchoTarjeta
        );

        // VOLUMEN
        CrearControlSlider(
            tarjetaOpciones.transform,
            "VOLUMEN GENERAL",
            AudioListener.volume,
            (val) =>
            {
                AudioListener.volume = val;
            }
        );

        // PANTALLA
        string txtPantalla =
            Screen.fullScreen
            ? "PANTALLA: COMPLETA"
            : "PANTALLA: VENTANA";

        CrearBotonOpcion(
            tarjetaOpciones.transform,
            txtPantalla,
            (btnText) =>
            {
                bool nuevoEstado = !Screen.fullScreen;

                Screen.fullScreenMode =
                    nuevoEstado
                    ? FullScreenMode.ExclusiveFullScreen
                    : FullScreenMode.Windowed;

                Screen.fullScreen = nuevoEstado;

                btnText.text =
                    nuevoEstado
                    ? "PANTALLA: COMPLETA"
                    : "PANTALLA: VENTANA";
            }
        );

        // RESOLUCIÓN / CALIDAD / VSYNC / FPS
        CrearBotonOpcion(tarjetaOpciones.transform, TextoResolucion(), CambiarResolucion);
        CrearBotonOpcion(tarjetaOpciones.transform, TextoCalidad(), CambiarCalidad);
        CrearBotonOpcion(tarjetaOpciones.transform, TextoVSync(), CambiarVSync);
        CrearBotonOpcion(tarjetaOpciones.transform, TextoLimiteFps(), CambiarLimiteFps);

        CrearEspaciador(tarjetaOpciones.transform, 6f);

        CrearBotonMenu(tarjetaOpciones.transform, "VOLVER AL MENU", CerrarOpciones, EstiloBoton.Secundario);


        // ========================================================
        // 11. LOGROS
        // ========================================================

        panelLogros = CrearPanelContenedor(canvasObj, "Panel_Logros");

        CrearTitulo(
            panelLogros.transform,
            "LOGROS",
            "OBJETIVOS ADICIONALES"
        );

        const float altoMarcoLogros = 480f;

        GameObject marcoLogros = CrearMarcoFijo(
            panelLogros.transform,
            margenIzquierdo,
            InicioContenidoY,
            1000f,
            altoMarcoLogros
        );

        marcoLogros.AddComponent<AchievementListUI>().Build(
            ObtenerFuenteTexto(),
            escalaFuente
        );

        GameObject colVolverLogros = CrearColumna(
            panelLogros.transform,
            margenIzquierdo,
            InicioContenidoY + altoMarcoLogros + 24f,
            520f,
            8f
        );

        CrearBotonMenu(colVolverLogros.transform, "VOLVER AL MENU", CerrarLogros, EstiloBoton.Secundario);


        // ========================================================
        // MOSTRAR MENÚ PRINCIPAL
        // ========================================================

        OcultarTodosLosPaneles();

        panelPrincipal.SetActive(true);

        LimpiarSeleccionUI();
    }


    // ============================================================
    // FUENTES
    // ============================================================

    // Fuente de TÍTULOS. Orden de búsqueda:
    //   1) la asignada en el Inspector
    //   2) Resources/Fonts/FuenteTitulo
    //   3) la fuente de texto
    private TMP_FontAsset ObtenerFuenteTitulo()
    {
        if (fuenteTitulo != null)
            return fuenteTitulo;

        TMP_FontAsset deResources =
            Resources.Load<TMP_FontAsset>("Fonts/FuenteTitulo");

        if (deResources != null)
            return deResources;

        return ObtenerFuenteTexto();
    }


    // Fuente de TEXTOS / BOTONES. Orden de búsqueda:
    //   1) la asignada en el Inspector
    //   2) Resources/Fonts/FuenteTexto
    //   3) la fuente predeterminada de TMP
    private TMP_FontAsset ObtenerFuenteTexto()
    {
        if (fuenteTexto != null)
            return fuenteTexto;

        TMP_FontAsset deResources =
            Resources.Load<TMP_FontAsset>("Fonts/FuenteTexto");

        if (deResources != null)
            return deResources;

        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        Debug.LogError(
            "NO SE ENCONTRÓ UNA FUENTE TMP. " +
            "Importá TMP Essential Resources y asigná " +
            "una TMP_FontAsset en 'Fuente Texto' / 'Fuente Titulo'."
        );

        return null;
    }


    // ============================================================
    // ESTILO DE TEXTO
    // ============================================================

    // Llamar DESPUÉS de asignar fontSize (se usa como tamaño máximo
    // del auto-size).
    private void AplicarEstiloTexto(
        TextMeshProUGUI texto,
        bool esTitulo = false
    )
    {
        if (texto == null)
            return;

        TMP_FontAsset fuente =
            esTitulo ? ObtenerFuenteTitulo() : ObtenerFuenteTexto();

        if (fuente != null)
            texto.font = fuente;

        texto.textWrappingMode = TextWrappingModes.NoWrap;

        texto.enableAutoSizing = true;
        texto.fontSizeMin = Mathf.Max(10f, texto.fontSize * 0.5f);
        texto.fontSizeMax = texto.fontSize;

        texto.overflowMode = TextOverflowModes.Overflow;
        texto.raycastTarget = false;

        // Sin contorno: look limpio / moderno.
        texto.outlineWidth = 0f;

        // Sombra suave para que el texto se lea sobre cualquier fondo.
        Material mat = texto.fontMaterial;

        if (mat != null)
        {
            mat.EnableKeyword("UNDERLAY_ON");

            mat.SetColor(
                ShaderUtilities.ID_UnderlayColor,
                new Color32(0, 0, 0, 150)
            );

            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.05f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
        }
    }


    private TextMeshProUGUI CrearTexto(
        Transform padre,
        string nombre,
        string contenido,
        float tamano,
        Color color,
        TextAlignmentOptions alineacion,
        bool esTitulo = false,
        float espaciadoLetras = 0f
    )
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = contenido;
        t.fontSize = tamano * escalaFuente;
        t.alignment = alineacion;
        t.color = color;
        t.characterSpacing = espaciadoLetras;

        AplicarEstiloTexto(t, esTitulo);

        return t;
    }


    // ============================================================
    // TÍTULO (arriba a la izquierda)
    // ============================================================

    private void CrearTitulo(
        Transform padre,
        string tituloPrincipal,
        string subtitulo,
        bool principal = false
    )
    {
        float tamano = principal ? 96f : 68f;

        // En el menú principal la última palabra va en rojo.
        string textoTitulo = tituloPrincipal;

        if (principal)
        {
            int ultimoEspacio = tituloPrincipal.LastIndexOf(' ');

            if (ultimoEspacio > 0)
            {
                textoTitulo =
                    tituloPrincipal.Substring(0, ultimoEspacio + 1) +
                    "<color=#" + ColorUtility.ToHtmlStringRGB(colorRojoSangre) + ">" +
                    tituloPrincipal.Substring(ultimoEspacio + 1) +
                    "</color>";
            }
        }

        TextMeshProUGUI txtTitulo = CrearTexto(
            padre,
            "Texto_Titulo",
            textoTitulo,
            tamano,
            colorTexto,
            TextAlignmentOptions.BottomLeft,
            true,
            principal ? 2f : 3f
        );

        AnclarArribaIzquierda(
            txtTitulo.rectTransform,
            margenIzquierdo,
            margenSuperior,
            1300f,
            120f
        );

        // Línea de acento
        GameObject lineaObj = new GameObject(
            "Linea_Titulo", typeof(RectTransform), typeof(Image));
        lineaObj.transform.SetParent(padre, false);

        Image imgLinea = lineaObj.GetComponent<Image>();
        imgLinea.color = colorRojoSangre;
        imgLinea.raycastTarget = false;

        AnclarArribaIzquierda(
            lineaObj.GetComponent<RectTransform>(),
            margenIzquierdo,
            margenSuperior + 130f,
            96f,
            5f
        );

        // Subtítulo (se quitan los guiones largos decorativos)
        string sub = subtitulo.Trim().Trim('—').Trim();

        TextMeshProUGUI txtSub = CrearTexto(
            padre,
            "Texto_Subtitulo",
            sub,
            22f,
            colorTextoSuave,
            TextAlignmentOptions.MidlineLeft,
            false,
            6f
        );

        AnclarArribaIzquierda(
            txtSub.rectTransform,
            margenIzquierdo,
            margenSuperior + 148f,
            1000f,
            34f
        );
    }


    // ============================================================
    // OBJETIVO DEL MAPA (tarjeta informativa)
    // ============================================================

    // IMPORTANTE: ActualizarObjetivoMapa() depende de esta jerarquía:
    //   caja (ScrollRect) > viewport > contenido > texto
    private TextMeshProUGUI CrearTextoObjetivoMapa(
        Transform padre,
        float x,
        float y
    )
    {
        GameObject caja = CrearMarcoFijo(padre, x, y, 460f, 360f);
        caja.name = "Panel_Objetivo_Mapa";

        // VIEWPORT
        GameObject viewport = new GameObject(
            "Viewport_Objetivo_Mapa",
            typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(caja.transform, false);

        RectTransform rtViewport = viewport.GetComponent<RectTransform>();
        rtViewport.anchorMin = Vector2.zero;
        rtViewport.anchorMax = Vector2.one;
        rtViewport.offsetMin = new Vector2(28f, 24f);
        rtViewport.offsetMax = new Vector2(-36f, -30f);

        Image imgViewport = viewport.GetComponent<Image>();
        imgViewport.color = new Color(1f, 1f, 1f, 0f);
        imgViewport.raycastTarget = true;
        imgViewport.canvasRenderer.cullTransparentMesh = false;

        // CONTENIDO
        GameObject contenido = new GameObject(
            "Contenido_Objetivo_Mapa", typeof(RectTransform));
        contenido.transform.SetParent(viewport.transform, false);

        RectTransform rtContenido = contenido.GetComponent<RectTransform>();
        rtContenido.anchorMin = new Vector2(0f, 1f);
        rtContenido.anchorMax = new Vector2(1f, 1f);
        rtContenido.pivot = new Vector2(0.5f, 1f);
        rtContenido.anchoredPosition = Vector2.zero;
        rtContenido.sizeDelta = new Vector2(0f, 300f);

        // TEXTO
        TextMeshProUGUI txt = CrearTexto(
            contenido.transform,
            "Texto_Objetivo_Mapa",
            "",
            24f,
            new Color(colorTexto.r, colorTexto.g, colorTexto.b, 0.92f),
            TextAlignmentOptions.TopLeft
        );

        // Esta tarjeta necesita WRAP real, no NoWrap.
        txt.textWrappingMode = TextWrappingModes.Normal;
        txt.enableAutoSizing = false;
        txt.fontSize = 24f * escalaFuente;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.lineSpacing = 6f;

        RectTransform rtTexto = txt.rectTransform;
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.pivot = new Vector2(0.5f, 1f);
        rtTexto.offsetMin = new Vector2(0f, 0f);
        rtTexto.offsetMax = new Vector2(0f, 0f);

        // SCROLL
        ScrollRect scroll = caja.AddComponent<ScrollRect>();
        scroll.viewport = rtViewport;
        scroll.content = rtContenido;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 35f;
        scroll.verticalNormalizedPosition = 1f;

        // BARRA DE SCROLL (fina)
        GameObject barra = new GameObject(
            "Scrollbar_Objetivo_Mapa", typeof(RectTransform), typeof(Image));
        barra.transform.SetParent(caja.transform, false);

        Image imgBarra = barra.GetComponent<Image>();
        imgBarra.color = new Color(1f, 1f, 1f, 0.06f);
        imgBarra.raycastTarget = true;

        RectTransform rtBarra = barra.GetComponent<RectTransform>();
        rtBarra.anchorMin = new Vector2(1f, 0f);
        rtBarra.anchorMax = new Vector2(1f, 1f);
        rtBarra.pivot = new Vector2(1f, 0.5f);
        rtBarra.offsetMin = new Vector2(-16f, 24f);
        rtBarra.offsetMax = new Vector2(-10f, -30f);

        GameObject handle = new GameObject(
            "Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(barra.transform, false);

        Image imgHandle = handle.GetComponent<Image>();
        imgHandle.color = new Color(1f, 1f, 1f, 0.38f);
        imgHandle.raycastTarget = true;

        RectTransform rtHandle = handle.GetComponent<RectTransform>();
        rtHandle.anchorMin = Vector2.zero;
        rtHandle.anchorMax = Vector2.one;
        rtHandle.offsetMin = Vector2.zero;
        rtHandle.offsetMax = Vector2.zero;

        Scrollbar scrollbar = barra.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = imgHandle;
        scrollbar.handleRect = rtHandle;

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = -3f;

        barra.transform.SetAsLastSibling();

        return txt;
    }

    private void ActualizarObjetivoMapa()
    {
        if (txtObjetivoMapa == null)
            return;

        string[] nombresMapas =
        {
            "SUBURBIO",
            "COMPLEJO DE OFICINAS",
            "ESTACION DE TREN"
        };

        string[] objetivos =
        {
            objetivoMapa1,
            objetivoMapa2,
            objetivoMapa3
        };

        int indice = Mathf.Clamp(
            mapaSeleccionado,
            0,
            objetivos.Length - 1
        );

        string objetivo = objetivos[indice] ?? "";

        objetivo = objetivo
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();

        txtObjetivoMapa.text =
            "<size=70%><color=#8C9490><cspace=0.15em>INFORMACIÓN DEL MAPA</cspace></color></size>\n" +
            "<size=135%><color=#FFFFFF>" + nombresMapas[indice] + "</color></size>\n\n" +
            objetivo;

        txtObjetivoMapa.textWrappingMode =
            TextWrappingModes.Normal;

        txtObjetivoMapa.enableAutoSizing =
            false;

        txtObjetivoMapa.fontSize =
            24f * escalaFuente;

        txtObjetivoMapa.overflowMode =
            TextOverflowModes.Overflow;

        // El contenido se hace tan alto como necesite el texto.
        RectTransform rtTexto =
            txtObjetivoMapa.GetComponent<RectTransform>();

        TextMeshProUGUI texto = txtObjetivoMapa;

        // Forzamos la actualización antes de medir.
        texto.ForceMeshUpdate();

        float altoNecesario =
            texto.GetPreferredValues(
                texto.text,
                rtTexto.rect.width,
                0f
            ).y;

        RectTransform rtContenido =
            rtTexto.parent.GetComponent<RectTransform>();

        RectTransform rtViewport =
            rtContenido.parent.GetComponent<RectTransform>();

        float altoViewport =
            rtViewport.rect.height;

        float altoContenido =
            Mathf.Max(altoViewport, altoNecesario);

        rtContenido.sizeDelta =
            new Vector2(0f, altoContenido);

        rtTexto.anchorMin =
            new Vector2(0f, 1f);

        rtTexto.anchorMax =
            new Vector2(1f, 1f);

        rtTexto.pivot =
            new Vector2(0.5f, 1f);

        rtTexto.anchoredPosition =
            Vector2.zero;

        rtTexto.sizeDelta =
            new Vector2(0f, altoNecesario);

        // Arrancar siempre mostrando la parte superior.
        ScrollRect scroll =
            rtContenido.parent.parent.GetComponent<ScrollRect>();

        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    // ============================================================
    // LIMPIAR SELECCIÓN DE UI
    // ============================================================

    private void LimpiarSeleccionUI()
    {
        EventSystem eventSystem =
            EventSystem.current;

        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }


    // ============================================================
    // NAVEGACIÓN
    // ============================================================

    private void OcultarTodosLosPaneles()
    {
        if (panelPrincipal)
            panelPrincipal.SetActive(false);

        if (panelOpciones)
            panelOpciones.SetActive(false);

        if (panelSeleccionRed)
            panelSeleccionRed.SetActive(false);

        if (panelCrearSala)
            panelCrearSala.SetActive(false);

        if (panelUnirseSala)
            panelUnirseSala.SetActive(false);

        if (panelLobby)
            panelLobby.SetActive(false);

        if (panelLogros)
            panelLogros.SetActive(false);
    }


    public void VolverAMenuPrincipal()
    {
        OcultarTodosLosPaneles();

        panelPrincipal.SetActive(true);

        LimpiarSeleccionUI();
    }


    public void AbrirSeleccionRed()
    {
        OcultarTodosLosPaneles();

        panelSeleccionRed.SetActive(true);

        LimpiarSeleccionUI();
    }


    public void AbrirCrearSala()
    {
        OcultarTodosLosPaneles();

        panelCrearSala.SetActive(true);

        LimpiarSeleccionUI();
    }


    public void AbrirUnirseSala()
    {
        OcultarTodosLosPaneles();

        panelUnirseSala.SetActive(true);

        LimpiarSeleccionUI();
    }


    // ============================================================
    // CREAR SALA (HOST)
    // ============================================================

    // Valida y guarda el nombre del jugador. Devuelve false si está vacío.
    private bool GuardarNombreJugador(TMP_InputField campo)
    {
        string nombre = campo.text.Trim();

        if (string.IsNullOrEmpty(nombre))
        {
            if (campo.placeholder is TMP_Text placeholder)
                placeholder.text = "¡Ingresá tu nombre!";

            return false;
        }

        if (nombre.Length > 16)
            nombre = nombre.Substring(0, 16);

        NombrePlayer.NombreJugador = nombre;
        Debug.Log("Nombre guardado: " + nombre);

        return true;
    } //nuevo



    public async void ConfirmarCrearSala()
    {
        if (!GuardarNombreJugador(inputNombreHost))
            return;
        int intento =
            PrepararLobbyRed("CREANDO SALA...");
        Debug.Log("Personaje seleccionado: " + personajeSeleccionado);
        try
        {
            await GameSessionManager.PrepareHostAsync();

            if (intento != intentoConexion) //nuevo
                return;

            // Antes de arrancar, para recibir también la conexión del propio host.
            SuscribirARed();

            string codigo =
                await RelayConnectionManager.StartHostAsync(
                    GameSessionManager.MaxPlayers - 1
                );

            if (intento != intentoConexion)
            {
                // El jugador salió del lobby mientras se creaba la sala.
                CerrarConexion();
                return;
            }

            codigoSala = codigo;

            Debug.Log(
                $"GeneradorMenuPrincipal: código de sala -> {codigoSala}"
            );

            btnIniciarPartida.gameObject.SetActive(true);
            btnIniciarPartida.interactable = true;

            ActualizarLobbyRed();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);

            if (intento == intentoConexion)
                TerminarLobbyConMensaje("NO SE PUDO CREAR LA SALA.");
        }
    }


    // ============================================================
    // UNIRSE A SALA (CLIENTE)
    // ============================================================

    public async void ConfirmarUnirseSala()
    {
        string codigo =
            inputCodigoSala.text.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(codigo))
        {
            if (inputCodigoSala.placeholder is TMP_Text placeholder)
                placeholder.text = "¡Ingresá un código de sala!";

            return;
        }
        if (!GuardarNombreJugador(inputNombreCliente))
            return; //nuevo

        int intento =
            PrepararLobbyRed($"CONECTANDO A LA SALA {codigo}...");

        codigoSala = codigo;

        try
        {
            await GameSessionManager.PrepareClientAsync();

            if (intento != intentoConexion)
                return;

            SuscribirARed();

            await RelayConnectionManager.StartClientAsync(codigo);

            if (intento != intentoConexion)
                CerrarConexion();

            // El lobby se actualiza al llegar el evento de conexión (OnEventoConexion).
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);

            if (intento == intentoConexion)
                TerminarLobbyConMensaje("NO SE PUDO CONECTAR. VERIFICÁ EL CÓDIGO.");
        }
    }


    // ============================================================
    // LOBBY EN RED
    // ============================================================

    // Muestra el lobby vacío con un mensaje de estado y devuelve el
    // número de intento, para detectar si el jugador se fue mientras tanto.
    private int PrepararLobbyRed(string estado)
    {
        OcultarTodosLosPaneles();

        panelLobby.SetActive(true);

        LimpiarSeleccionUI();

        codigoSala = null;

        btnIniciarPartida.gameObject.SetActive(false);

        txtDetallesLobby.text = estado;

        MostrarRanurasJugadores(0);

        return ++intentoConexion;
    }


    // clienteSaliente: id que se está desconectando y puede seguir en la lista.
    private void ActualizarLobbyRed(ulong? clienteSaliente = null)
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
            return;

        int cantidad = 0;

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (clientId != clienteSaliente)
                cantidad++;
        }
        // En los clientes, la lista que manda el host es la que manda.
        if (!networkManager.IsServer && nombresLobbyActuales.Count > 0)
            cantidad = nombresLobbyActuales.Count;
        MostrarRanurasJugadores(cantidad);

        if (string.IsNullOrEmpty(codigoSala))
            return;

        if (networkManager.IsServer)
        {
            txtDetallesLobby.text =
                $"CÓDIGO DE SALA: {codigoSala}\n" +
                TextoDetallesPartida();
        }
        else if (networkManager.IsConnectedClient)
        {
            txtDetallesLobby.text =
                $"SALA: {codigoSala}\n" +
                "ESPERANDO A QUE EL LÍDER INICIE LA PARTIDA";
        }
    }

    private void MostrarRanurasJugadores(int cantidadConectados)
    {
        for (int i = 0; i < listaRanurasJugadores.Length; i++)
        {
            if (i < cantidadConectados)
            {
                string nombre =
                    (i < nombresLobbyActuales.Count &&
                     !string.IsNullOrEmpty(nombresLobbyActuales[i]))
                    ? nombresLobbyActuales[i]
                    : $"Jugador {i + 1}";

                listaRanurasJugadores[i].text =
                    (i == 0)
                    ? $"1. {nombre} (LÍDER)"
                    : $"{i + 1}. {nombre}";

                listaRanurasJugadores[i].color =
                    colorVerdeZombie;
            }
            else
            {
                listaRanurasJugadores[i].text =
                    $"{i + 1}. [ Esperando Jugador... ]";

                listaRanurasJugadores[i].color =
                    Color.gray;
            }
        }
    }

    private string TextoDetallesPartida()
    {
        string[] nombresMapas =
        {
            "Suburbio Zombie",
            "Complejo de Oficinas",
            "Estacion de tren"
        };

        string[] nombresDifs =
        {
            "Fácil",
            "Normal",
            "Difícil"
        };

        return
            $"MAPA: {nombresMapas[mapaSeleccionado].ToUpper()}  |  " +
            $"DIFICULTAD: {nombresDifs[dificultadSeleccionada].ToUpper()}";
    }


    private void OnEventoConexion(
    NetworkManager networkManager,
    ConnectionEventData data
)
    {
        switch (data.EventType)
        {
            case ConnectionEvent.ClientConnected:
                RegistrarMensajesLobby();

                if (!networkManager.IsServer)
                {
                    // Cliente: le manda su nombre al host.
                    EnviarNombreAlHost();
                }
                else if (data.ClientId == NetworkManager.ServerClientId)
                {
                    // Host: arma la lista con su propio nombre.
                    EnviarListaLobby();
                }

                ActualizarLobbyRed();
                break;

            case ConnectionEvent.PeerConnected:
                ActualizarLobbyRed();
                break;

            case ConnectionEvent.PeerDisconnected:
                ActualizarLobbyRed(data.ClientId);
                break;

            case ConnectionEvent.ClientDisconnected:
                if (networkManager.IsServer)
                {
                    if (data.ClientId != NetworkManager.ServerClientId)
                    {
                        nombresLobby.Remove(data.ClientId);
                        EnviarListaLobby(data.ClientId);
                    }
                }
                else
                {
                    // Rechazado (partida iniciada / sala llena) o el host cerró la sala.
                    string motivo =
                        string.IsNullOrEmpty(networkManager.DisconnectReason)
                        ? "EL LÍDER CERRÓ LA SALA O SE PERDIÓ LA CONEXIÓN."
                        : networkManager.DisconnectReason.ToUpper();

                    TerminarLobbyConMensaje(motivo);
                }
                break;
        }
    }

    private void OnFalloTransporte()
    {
        TerminarLobbyConMensaje("SE PERDIÓ LA CONEXIÓN DE RED.");
    }


    // Corta la conexión pero deja el lobby abierto mostrando el motivo;
    // el jugador vuelve con "SALIR DEL LOBBY".
    private void TerminarLobbyConMensaje(string mensaje)
    {
        intentoConexion++;

        CerrarConexion();

        codigoSala = null;

        btnIniciarPartida.gameObject.SetActive(false);

        MostrarRanurasJugadores(0);

        txtDetallesLobby.text = mensaje;
    }

    //metodos nuevos para los nombres

    // ---------- NOMBRES EN EL LOBBY ----------

    private void RegistrarMensajesLobby()
    {
        if (mensajesRegistrados)
            return;

        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.CustomMessagingManager == null)
            return;

        nm.CustomMessagingManager.RegisterNamedMessageHandler(
            MsgNombre, AlRecibirNombreLobby);

        nm.CustomMessagingManager.RegisterNamedMessageHandler(
            MsgLista, AlRecibirListaLobby);

        mensajesRegistrados = true;
    }


    // Cliente -> host
    private void EnviarNombreAlHost()
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || nm.CustomMessagingManager == null)
            return;

        string nombre = NombrePlayer.NombreJugador;

        if (string.IsNullOrWhiteSpace(nombre))
            nombre = "Jugador";

        using (var writer = new FastBufferWriter(256, Allocator.Temp))
        {
            writer.WriteValueSafe(nombre);

            nm.CustomMessagingManager.SendNamedMessage(
                MsgNombre,
                NetworkManager.ServerClientId,
                writer
            );
        }
    }


    // Host: recibe el nombre de un cliente
    private void AlRecibirNombreLobby(ulong remitente, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string nombre);

        nombre = (nombre ?? "").Trim();

        if (nombre.Length > 16)
            nombre = nombre.Substring(0, 16);

        if (string.IsNullOrWhiteSpace(nombre))
            nombre = "Jugador " + (remitente + 1);

        nombresLobby[remitente] = nombre;

        EnviarListaLobby();
    }


    // Host: arma la lista en orden de conexión y se la manda a todos
    private void EnviarListaLobby(ulong? clienteSaliente = null)
    {
        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsServer)
            return;

        nombresLobbyActuales.Clear();

        foreach (ulong id in nm.ConnectedClientsIds)
        {
            if (id == clienteSaliente)
                continue;

            string nombre;

            if (id == NetworkManager.ServerClientId)
                nombre = NombrePlayer.NombreJugador;
            else
                nombresLobby.TryGetValue(id, out nombre);

            nombresLobbyActuales.Add(nombre ?? "");
        }

        // Actualiza la pantalla del propio host
        ActualizarLobbyRed(clienteSaliente);

        // Avisa a los clientes (si hay alguno)
        if (nombresLobbyActuales.Count > 1 && nm.CustomMessagingManager != null)
        {
            using (var writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteValueSafe(nombresLobbyActuales.Count);

                foreach (string nombre in nombresLobbyActuales)
                    writer.WriteValueSafe(nombre);

                nm.CustomMessagingManager.SendNamedMessageToAll(MsgLista, writer);
            }
        }
    }


    // Cliente: recibe la lista completa del host
    private void AlRecibirListaLobby(ulong remitente, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int cantidad);

        nombresLobbyActuales.Clear();

        for (int i = 0; i < cantidad; i++)
        {
            reader.ReadValueSafe(out string nombre);
            nombresLobbyActuales.Add(nombre);
        }

        MostrarRanurasJugadores(cantidad);
    } //ultimo metodo nuevo

    public void SalirDelLobby()
    {
        intentoConexion++;

        CerrarConexion();

        codigoSala = null;

        AbrirSeleccionRed();
    }


    // Si el que sale es el host, la sala se cierra para todos.
    private void CerrarConexion()
    {
        nombresLobby.Clear();
        nombresLobbyActuales.Clear();
        mensajesRegistrados = false;
        DesuscribirDeRed();

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager != null &&
            networkManager.IsListening &&
            !networkManager.ShutdownInProgress)
        {
            networkManager.Shutdown();
        }
    }


    private void SuscribirARed()
    {
        if (suscritoARed)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;

        networkManager.OnConnectionEvent += OnEventoConexion;
        networkManager.OnTransportFailure += OnFalloTransporte;

        suscritoARed = true;
    }


    private void DesuscribirDeRed()
    {
        if (!suscritoARed)
            return;

        suscritoARed = false;

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        networkManager.OnConnectionEvent -= OnEventoConexion;
        networkManager.OnTransportFailure -= OnFalloTransporte;
    }


    // ============================================================
    // INICIAR PARTIDA (SOLO HOST)
    // ============================================================

    // Inicia con los jugadores que estén en el lobby en ese momento (1 a 4).
    public void IniciarPartidaDesdeLobby()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsServer)
            return;

        string escenaACargar =
            nombreEscenaMapa1;

        if (mapaSeleccionado == 1)
            escenaACargar =
                nombreEscenaMapa2;

        if (mapaSeleccionado == 2)
            escenaACargar =
                nombreEscenaMapa3;

        if (GameSessionManager.StartGame(escenaACargar))
        {
            btnIniciarPartida.interactable = false;
        }
        else
        {
            txtDetallesLobby.text =
                $"CÓDIGO DE SALA: {codigoSala}\n" +
                $"NO SE PUDO CARGAR LA ESCENA '{escenaACargar}'";
        }
    }


    // ============================================================
    // OPCIONES
    // ============================================================
    public static class DatosJugador
    {
        public static int PersonajeSeleccionado = 0;
    }


    public class CharacterSelectorUI : MonoBehaviour
    {
        public void SelectCharacter(int index) => PlayerModelChoice.SelectedIndex = index;
    }
    public void AbrirOpciones()
    {
        OcultarTodosLosPaneles();

        panelOpciones.SetActive(true);

        LimpiarSeleccionUI();
    }


    public void AbrirLogros()
    {
        OcultarTodosLosPaneles();

        panelLogros.SetActive(true);

        LimpiarSeleccionUI();
    }


    public void CerrarLogros()
    {
        OcultarTodosLosPaneles();

        panelPrincipal.SetActive(true);

        LimpiarSeleccionUI();
    }


    public void CerrarOpciones()
    {
        OcultarTodosLosPaneles();

        panelPrincipal.SetActive(true);

        LimpiarSeleccionUI();
    }


    // ============================================================
    // SALIR
    // ============================================================

    public void Salir()
    {
        Debug.Log(
            "Saliendo del juego..."
        );

#if UNITY_EDITOR

        UnityEditor.EditorApplication
            .isPlaying = false;

#else

        Application.Quit();

#endif
    }

    // ============================================================
    // HELPERS DE LAYOUT
    // ============================================================

    private enum EstiloBoton
    {
        Primario,     // acción principal (JUGAR, CONFIRMAR...)
        Normal,       // opciones comunes
        Secundario    // volver / salir
    }


    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }


    // Ancla el elemento a la esquina superior izquierda de su padre.
    // x e y se miden hacia la derecha y hacia ABAJO.
    private static void AnclarArribaIzquierda(
        RectTransform rt,
        float x,
        float y,
        float ancho,
        float alto
    )
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(ancho, alto);
    }


    private static LayoutElement FijarAltura(GameObject go, float alto)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();

        if (le == null)
            le = go.AddComponent<LayoutElement>();

        le.minHeight = alto;
        le.preferredHeight = alto;

        return le;
    }


    private void CrearEspaciador(Transform padre, float alto)
    {
        GameObject go = new GameObject(
            "Espaciador", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(padre, false);
        FijarAltura(go, alto);
    }


    // Sprite blanco de 256x1 con alpha que baja de 1 a 0 (izq -> der).
    // 'potencia' > 1 concentra la opacidad cerca del borde izquierdo.
    private Sprite CrearSpriteDegradadoHorizontal(float potencia)
    {
        const int ancho = 256;

        Texture2D tex = new Texture2D(
            ancho, 1, TextureFormat.RGBA32, false);

        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixeles = new Color32[ancho];

        for (int i = 0; i < ancho; i++)
        {
            float u = i / (float)(ancho - 1);
            float a = Mathf.Pow(1f - u, potencia);

            pixeles[i] = new Color32(
                255, 255, 255,
                (byte)Mathf.RoundToInt(a * 255f));
        }

        tex.SetPixels32(pixeles);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, ancho, 1),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }


    // ============================================================
    // PANEL / COLUMNA / TARJETA
    // ============================================================

    private GameObject CrearPanelContenedor(
        GameObject padre,
        string nombre
    )
    {
        GameObject panel = new GameObject(nombre);
        panel.transform.SetParent(padre.transform, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        Estirar(rt);

        return panel;
    }


    // Columna vertical alineada a la izquierda, sin fondo (menús de botones).
    private GameObject CrearColumna(
        Transform padre,
        float x,
        float y,
        float ancho,
        float espaciado
    )
    {
        GameObject columna = new GameObject(
            "Columna", typeof(RectTransform));
        columna.transform.SetParent(padre, false);

        AnclarArribaIzquierda(
            columna.GetComponent<RectTransform>(), x, y, ancho, 0f);

        VerticalLayoutGroup layout =
            columna.AddComponent<VerticalLayoutGroup>();

        layout.spacing = espaciado;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            columna.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return columna;
    }


    // Tarjeta "vidrio" que crece sola según su contenido.
    // Todo lo que se agregue como hijo queda alineado y con el mismo ancho.
    private GameObject CrearTarjeta(
        Transform padre,
        float x,
        float y,
        float ancho
    )
    {
        GameObject tarjeta = new GameObject(
            "Tarjeta", typeof(RectTransform), typeof(Image));
        tarjeta.transform.SetParent(padre, false);

        Image img = tarjeta.GetComponent<Image>();
        img.color = colorVidrio;

        AnclarArribaIzquierda(
            tarjeta.GetComponent<RectTransform>(), x, y, ancho, 0f);

        VerticalLayoutGroup layout =
            tarjeta.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(32, 32, 30, 30);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            tarjeta.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AgregarBordeFino(tarjeta.transform);

        return tarjeta;
    }


    // Marco "vidrio" de tamaño fijo (para contenido que se posiciona solo,
    // como la lista de logros o la tarjeta del objetivo del mapa).
    private GameObject CrearMarcoFijo(
        Transform padre,
        float x,
        float y,
        float ancho,
        float alto
    )
    {
        GameObject marco = new GameObject(
            "Marco_Fondo", typeof(RectTransform), typeof(Image));
        marco.transform.SetParent(padre, false);

        Image img = marco.GetComponent<Image>();
        img.color = colorVidrio;

        AnclarArribaIzquierda(
            marco.GetComponent<RectTransform>(), x, y, ancho, alto);

        AgregarBordeFino(marco.transform);

        return marco;
    }


    // Borde de 1.5px + pequeño acento rojo en la esquina superior izquierda.
    private void AgregarBordeFino(Transform marco)
    {
        const float g = 1.5f;

        CrearLineaBorde(marco, "Borde_Sup",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, g), colorBordePanel);

        CrearLineaBorde(marco, "Borde_Inf",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, g), colorBordePanel);

        CrearLineaBorde(marco, "Borde_Izq",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0.5f), new Vector2(g, 0f), colorBordePanel);

        CrearLineaBorde(marco, "Borde_Der",
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f), new Vector2(g, 0f), colorBordePanel);

        CrearLineaBorde(marco, "Acento_Esquina",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(72f, 3f), colorRojoSangre);
    }


    private void CrearLineaBorde(
        Transform marco,
        string nombre,
        Vector2 anclaMin,
        Vector2 anclaMax,
        Vector2 pivote,
        Vector2 tamano,
        Color color
    )
    {
        GameObject go = new GameObject(
            nombre,
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(marco, false);

        // No participa del layout de la tarjeta.
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.pivot = pivote;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = tamano;
    }


    // ============================================================
    // BOTONES
    // ============================================================

    private const float PaddingTextoBoton = 28f;

    // Botón principal de menú: transparente, texto a la izquierda,
    // barra de acento + resalte con degradado al pasar el mouse.
    private Button CrearBotonMenu(
        Transform padre,
        string texto,
        UnityEngine.Events.UnityAction accion,
        EstiloBoton estilo = EstiloBoton.Normal,
        bool grande = false
    )
    {
        TextMeshProUGUI txt;

        Button btn = CrearBotonBase(
            padre,
            texto,
            estilo,
            grande ? 72f : 58f,
            grande ? 38f : 28f,
            false,
            out txt
        );

        btn.onClick.AddListener(
            () =>
            {
                ReproducirSFX(sfxClick);
                accion?.Invoke();
                LimpiarSeleccionUI();
            }
        );

        return btn;
    }


    // Botón de opción: el callback recibe el texto para poder cambiarlo.
    private TextMeshProUGUI CrearBotonOpcion(
        Transform padre,
        string textoInicial,
        System.Action<TextMeshProUGUI> accionAlClic
    )
    {
        TextMeshProUGUI txt;

        Button btn = CrearBotonBase(
            padre,
            textoInicial,
            EstiloBoton.Normal,
            54f,
            24f,
            true,
            out txt
        );

        TextMeshProUGUI txtLocal = txt;

        btn.onClick.AddListener(
            () =>
            {
                ReproducirSFX(sfxClick);
                accionAlClic?.Invoke(txtLocal);
                LimpiarSeleccionUI();
            }
        );

        return txt;
    }


    // Igual que CrearBotonOpcion, pero para callbacks sin parámetros.
    private TextMeshProUGUI CrearBotonCiclico(
        Transform padre,
        string textoInicial,
        System.Action accion
    )
    {
        return CrearBotonOpcion(
            padre,
            textoInicial,
            (t) => accion?.Invoke()
        );
    }


    private Button CrearBotonBase(
        Transform padre,
        string texto,
        EstiloBoton estilo,
        float alto,
        float tamanoTexto,
        bool conIndicador,
        out TextMeshProUGUI txt
    )
    {
        GameObject btnObj = new GameObject(
            "Btn_" + texto,
            typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(padre, false);

        FijarAltura(btnObj, alto);

        // ---------- colores según estilo ----------
        Color rojo = colorRojoSangre;
        Color rojoHover = colorRojoHover;

        Color fondoReposo, fondoHover, barraReposo, barraHover, textoReposo;

        switch (estilo)
        {
            case EstiloBoton.Primario:
                fondoReposo = new Color(rojo.r, rojo.g, rojo.b, 0.30f);
                fondoHover = new Color(rojoHover.r, rojoHover.g, rojoHover.b, 0.62f);
                barraReposo = rojo;
                barraHover = rojoHover;
                textoReposo = Color.white;
                break;

            case EstiloBoton.Secundario:
                fondoReposo = new Color(1f, 1f, 1f, 0.02f);
                fondoHover = new Color(rojo.r, rojo.g, rojo.b, 0.30f);
                barraReposo = new Color(1f, 1f, 1f, 0.10f);
                barraHover = rojoHover;
                textoReposo = colorTextoApagado;
                break;

            default:
                fondoReposo = new Color(1f, 1f, 1f, 0.04f);
                fondoHover = new Color(rojo.r, rojo.g, rojo.b, 0.40f);
                barraReposo = new Color(1f, 1f, 1f, 0.28f);
                barraHover = rojoHover;
                textoReposo = colorTexto;
                break;
        }

        // ---------- fondo (también es el área clickeable) ----------
        Image fondo = btnObj.GetComponent<Image>();
        fondo.sprite = spriteDegradadoSuave;
        fondo.color = fondoReposo;
        fondo.canvasRenderer.cullTransparentMesh = false;

        // ---------- barra de acento ----------
        GameObject barraObj = new GameObject(
            "Barra_Acento", typeof(RectTransform), typeof(Image));
        barraObj.transform.SetParent(btnObj.transform, false);

        Image imgBarra = barraObj.GetComponent<Image>();
        imgBarra.color = barraReposo;
        imgBarra.raycastTarget = false;

        RectTransform rtBarra = barraObj.GetComponent<RectTransform>();
        rtBarra.anchorMin = new Vector2(0f, 0f);
        rtBarra.anchorMax = new Vector2(0f, 1f);
        rtBarra.pivot = new Vector2(0f, 0.5f);
        rtBarra.anchoredPosition = Vector2.zero;
        rtBarra.sizeDelta = new Vector2(3f, -18f);

        // ---------- texto ----------
        txt = CrearTexto(
            btnObj.transform,
            "Texto_TMP",
            texto,
            tamanoTexto,
            textoReposo,
            TextAlignmentOptions.MidlineLeft,
            false,
            2f
        );

        RectTransform rtTxt = txt.rectTransform;
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = new Vector2(PaddingTextoBoton, 0f);
        rtTxt.offsetMax = new Vector2(conIndicador ? -52f : -16f, 0f);

        // ---------- indicador de opción cíclica ----------
        TextMeshProUGUI indicador = null;

        if (conIndicador)
        {
            indicador = CrearTexto(
                btnObj.transform,
                "Indicador",
                ">",
                tamanoTexto,
                colorTextoApagado,
                TextAlignmentOptions.MidlineRight,
                false,
                0f
            );

            RectTransform rtInd = indicador.rectTransform;
            rtInd.anchorMin = Vector2.zero;
            rtInd.anchorMax = Vector2.one;
            rtInd.offsetMin = Vector2.zero;
            rtInd.offsetMax = new Vector2(-20f, 0f);
        }

        // ---------- botón ----------
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = fondo;
        btn.transition = Selectable.Transition.None;

        Navigation navegacion = btn.navigation;
        navegacion.mode = Navigation.Mode.None;
        btn.navigation = navegacion;

        // ---------- animación de hover ----------
        EfectoBotonMenu efecto = btnObj.AddComponent<EfectoBotonMenu>();

        efecto.Configurar(
            btn,
            fondo,
            imgBarra,
            rtBarra,
            txt,
            rtTxt,
            indicador,
            fondoReposo,
            fondoHover,
            barraReposo,
            barraHover,
            textoReposo,
            Color.white,
            () => ReproducirSFX(sfxHover)
        );

        return btn;
    }


    // Anima el hover: resalte con degradado, barra de acento que se
    // ensancha y texto que se corre un poco a la derecha.
    // También atenúa el botón cuando está desactivado (interactable = false).
    private sealed class EfectoBotonMenu :
        MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float Desplazamiento = 14f;
        private const float Velocidad = 9f;

        private Button boton;
        private Image fondo;
        private Image barra;
        private RectTransform rtBarra;
        private TextMeshProUGUI texto;
        private RectTransform rtTexto;
        private TextMeshProUGUI indicador;

        private Color fondoReposo, fondoHover;
        private Color barraReposo, barraHover;
        private Color textoReposo, textoHover;

        private System.Action alEntrar;

        private float paddingBase;
        private float t;
        private bool encima;
        private float ultimoK = -1f;
        private bool ultimoHabilitado = true;


        public void Configurar(
            Button boton,
            Image fondo,
            Image barra,
            RectTransform rtBarra,
            TextMeshProUGUI texto,
            RectTransform rtTexto,
            TextMeshProUGUI indicador,
            Color fondoReposo,
            Color fondoHover,
            Color barraReposo,
            Color barraHover,
            Color textoReposo,
            Color textoHover,
            System.Action alEntrar
        )
        {
            this.boton = boton;
            this.fondo = fondo;
            this.barra = barra;
            this.rtBarra = rtBarra;
            this.texto = texto;
            this.rtTexto = rtTexto;
            this.indicador = indicador;
            this.fondoReposo = fondoReposo;
            this.fondoHover = fondoHover;
            this.barraReposo = barraReposo;
            this.barraHover = barraHover;
            this.textoReposo = textoReposo;
            this.textoHover = textoHover;
            this.alEntrar = alEntrar;

            paddingBase = rtTexto.offsetMin.x;

            Aplicar(0f, boton.interactable);
        }


        private void OnEnable()
        {
            // Si el panel se oculta con el mouse encima, no queda "pegado".
            encima = false;
            t = 0f;
            ultimoK = -1f;

            if (boton != null)
                Aplicar(0f, boton.interactable);
        }


        public void OnPointerEnter(PointerEventData eventData)
        {
            if (boton == null || !boton.interactable)
                return;

            encima = true;
            alEntrar?.Invoke();
        }


        public void OnPointerExit(PointerEventData eventData)
        {
            encima = false;
        }


        private void Update()
        {
            if (boton == null)
                return;

            bool habilitado = boton.interactable;
            float objetivo = (encima && habilitado) ? 1f : 0f;

            t = Mathf.MoveTowards(
                t, objetivo, Time.unscaledDeltaTime * Velocidad);

            if (Mathf.Approximately(t, ultimoK) &&
                habilitado == ultimoHabilitado)
                return;

            ultimoK = t;
            ultimoHabilitado = habilitado;

            Aplicar(Mathf.SmoothStep(0f, 1f, t), habilitado);
        }


        private void Aplicar(float k, bool habilitado)
        {
            float alfa = habilitado ? 1f : 0.35f;

            Color cf = Color.Lerp(fondoReposo, fondoHover, k);
            cf.a *= alfa;
            fondo.color = cf;

            Color cb = Color.Lerp(barraReposo, barraHover, k);
            cb.a *= alfa;
            barra.color = cb;

            rtBarra.sizeDelta = new Vector2(
                Mathf.Lerp(3f, 6f, k), rtBarra.sizeDelta.y);

            Color ct = Color.Lerp(textoReposo, textoHover, k);
            ct.a *= alfa;
            texto.color = ct;

            Vector2 min = rtTexto.offsetMin;
            min.x = paddingBase + Desplazamiento * k;
            rtTexto.offsetMin = min;

            if (indicador != null)
            {
                Color ci = Color.Lerp(
                    new Color(0.52f, 0.55f, 0.54f, 1f), barraHover, k);
                ci.a *= alfa;
                indicador.color = ci;
            }
        }
    }


    // ============================================================
    // CAMPO DE TEXTO
    // ============================================================

    // Etiqueta pequeña arriba + campo con línea inferior que se
    // ilumina en rojo al enfocarlo.
    private TMP_InputField CrearCampoTexto(
        Transform padre,
        string etiqueta,
        string valorPorDefecto
    )
    {
        const float altoEtiqueta = 22f;
        const float altoInput = 52f;
        const float separacion = 6f;

        // ---------- contenedor ----------
        GameObject contenedor = new GameObject(
            "Campo_" + etiqueta,
            typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        contenedor.transform.SetParent(padre, false);

        FijarAltura(
            contenedor, altoEtiqueta + separacion + altoInput);

        VerticalLayoutGroup layout =
            contenedor.GetComponent<VerticalLayoutGroup>();

        layout.spacing = separacion;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // ---------- etiqueta ----------
        TextMeshProUGUI txtEtiqueta = CrearTexto(
            contenedor.transform,
            "Etiqueta",
            etiqueta,
            18f,
            colorTextoSuave,
            TextAlignmentOptions.MidlineLeft,
            false,
            3f
        );

        FijarAltura(txtEtiqueta.gameObject, altoEtiqueta);

        // ---------- input ----------
        GameObject inputObj = new GameObject(
            "Input_TMP",
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        inputObj.transform.SetParent(contenedor.transform, false);

        FijarAltura(inputObj, altoInput);

        Image imgInput = inputObj.GetComponent<Image>();
        Color fondoReposo = new Color(0f, 0f, 0f, 0.38f);
        Color fondoFoco = new Color(0f, 0f, 0f, 0.58f);
        imgInput.color = fondoReposo;

        // Línea inferior
        GameObject lineaObj = new GameObject(
            "Linea", typeof(RectTransform), typeof(Image));
        lineaObj.transform.SetParent(inputObj.transform, false);

        Image imgLinea = lineaObj.GetComponent<Image>();
        Color lineaReposo = new Color(1f, 1f, 1f, 0.25f);
        imgLinea.color = lineaReposo;
        imgLinea.raycastTarget = false;

        RectTransform rtLinea = lineaObj.GetComponent<RectTransform>();
        rtLinea.anchorMin = new Vector2(0f, 0f);
        rtLinea.anchorMax = new Vector2(1f, 0f);
        rtLinea.pivot = new Vector2(0.5f, 0f);
        rtLinea.anchoredPosition = Vector2.zero;
        rtLinea.sizeDelta = new Vector2(0f, 2f);

        // Área de texto (viewport con máscara)
        GameObject areaObj = new GameObject(
            "Area_Texto", typeof(RectTransform), typeof(RectMask2D));
        areaObj.transform.SetParent(inputObj.transform, false);

        RectTransform rtArea = areaObj.GetComponent<RectTransform>();
        rtArea.anchorMin = Vector2.zero;
        rtArea.anchorMax = Vector2.one;
        rtArea.offsetMin = new Vector2(16f, 4f);
        rtArea.offsetMax = new Vector2(-16f, -4f);

        // Texto del input
        TextMeshProUGUI texto = CrearTexto(
            areaObj.transform,
            "Texto_Input",
            valorPorDefecto,
            24f,
            Color.white,
            TextAlignmentOptions.MidlineLeft,
            false,
            1f
        );

        texto.enableAutoSizing = false;
        texto.fontSize = 24f * escalaFuente;
        Estirar(texto.rectTransform);

        // Placeholder
        TextMeshProUGUI placeholder = CrearTexto(
            areaObj.transform,
            "Placeholder",
            "Escribí aquí...",
            22f,
            new Color(1f, 1f, 1f, 0.30f),
            TextAlignmentOptions.MidlineLeft,
            false,
            1f
        );

        placeholder.enableAutoSizing = false;
        placeholder.fontSize = 22f * escalaFuente;
        Estirar(placeholder.rectTransform);

        // ---------- TMP_InputField ----------
        TMP_InputField input = inputObj.AddComponent<TMP_InputField>();

        input.textViewport = rtArea;
        input.textComponent = texto;
        input.placeholder = placeholder;
        input.text = valorPorDefecto;
        input.fontAsset = ObtenerFuenteTexto();
        input.pointSize = 24f * escalaFuente;
        input.characterValidation = TMP_InputField.CharacterValidation.None;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.interactable = true;

        input.targetGraphic = imgInput;
        input.transition = Selectable.Transition.None;

        input.customCaretColor = true;
        input.caretColor = colorRojoHover;
        input.caretWidth = 2;
        input.selectionColor = new Color(
            colorRojoSangre.r, colorRojoSangre.g, colorRojoSangre.b, 0.45f);

        // Efecto de foco
        EfectoCampoMenu efecto = inputObj.AddComponent<EfectoCampoMenu>();

        efecto.Configurar(
            input,
            imgInput,
            imgLinea,
            fondoReposo,
            fondoFoco,
            lineaReposo,
            colorRojoHover
        );

        return input;
    }


    private sealed class EfectoCampoMenu : MonoBehaviour
    {
        private TMP_InputField campo;
        private Image fondo;
        private Image linea;
        private Color fondoReposo, fondoFoco;
        private Color lineaReposo, lineaFoco;
        private float t;

        public void Configurar(
            TMP_InputField campo,
            Image fondo,
            Image linea,
            Color fondoReposo,
            Color fondoFoco,
            Color lineaReposo,
            Color lineaFoco
        )
        {
            this.campo = campo;
            this.fondo = fondo;
            this.linea = linea;
            this.fondoReposo = fondoReposo;
            this.fondoFoco = fondoFoco;
            this.lineaReposo = lineaReposo;
            this.lineaFoco = lineaFoco;
        }

        private void Update()
        {
            if (campo == null)
                return;

            float objetivo = campo.isFocused ? 1f : 0f;

            t = Mathf.MoveTowards(
                t, objetivo, Time.unscaledDeltaTime * 10f);

            fondo.color = Color.Lerp(fondoReposo, fondoFoco, t);
            linea.color = Color.Lerp(lineaReposo, lineaFoco, t);
        }
    }


    // ============================================================
    // SLIDER
    // ============================================================

    private void CrearControlSlider(
        Transform padre,
        string etiqueta,
        float valorInicial,
        System.Action<float> alCambiarValor
    )
    {
        // ---------- contenedor ----------
        GameObject contenedor = new GameObject(
            "Control_Slider_" + etiqueta,
            typeof(RectTransform), typeof(LayoutElement));
        contenedor.transform.SetParent(padre, false);

        FijarAltura(contenedor, 66f);

        // ---------- etiqueta (izquierda) ----------
        TextMeshProUGUI txtEtiqueta = CrearTexto(
            contenedor.transform,
            "Etiqueta",
            etiqueta,
            24f,
            colorTexto,
            TextAlignmentOptions.MidlineLeft,
            false,
            2f
        );

        RectTransform rtEt = txtEtiqueta.rectTransform;
        rtEt.anchorMin = new Vector2(0f, 1f);
        rtEt.anchorMax = new Vector2(1f, 1f);
        rtEt.pivot = new Vector2(0f, 1f);
        rtEt.anchoredPosition = new Vector2(0f, 0f);
        rtEt.sizeDelta = new Vector2(-90f, 30f);

        // ---------- valor en % (derecha) ----------
        TextMeshProUGUI txtValor = CrearTexto(
            contenedor.transform,
            "Valor",
            Mathf.RoundToInt(valorInicial * 100f) + "%",
            24f,
            colorRojoHover,
            TextAlignmentOptions.MidlineRight,
            false,
            1f
        );

        RectTransform rtVal = txtValor.rectTransform;
        rtVal.anchorMin = new Vector2(1f, 1f);
        rtVal.anchorMax = new Vector2(1f, 1f);
        rtVal.pivot = new Vector2(1f, 1f);
        rtVal.anchoredPosition = new Vector2(0f, 0f);
        rtVal.sizeDelta = new Vector2(80f, 30f);

        // ---------- slider ----------
        DefaultControls.Resources res = new DefaultControls.Resources();

        GameObject sliderObj = DefaultControls.CreateSlider(res);
        sliderObj.transform.SetParent(contenedor.transform, false);

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.value = valorInicial;

        EstilizarSlider(sliderObj);

        slider.onValueChanged.AddListener(
            (v) =>
            {
                txtValor.text = Mathf.RoundToInt(v * 100f) + "%";
                alCambiarValor?.Invoke(v);
            }
        );

        RectTransform rtSlider = sliderObj.GetComponent<RectTransform>();
        rtSlider.anchorMin = new Vector2(0f, 0f);
        rtSlider.anchorMax = new Vector2(1f, 0f);
        rtSlider.pivot = new Vector2(0.5f, 0f);
        rtSlider.anchoredPosition = new Vector2(0f, 2f);
        rtSlider.sizeDelta = new Vector2(0f, 26f);
    }


    // Reemplaza el look por defecto de Unity: línea fina, relleno rojo
    // y selector rectangular.
    private void EstilizarSlider(GameObject sliderObj)
    {
        Transform tFondo = sliderObj.transform.Find("Background");
        Transform tAreaRelleno = sliderObj.transform.Find("Fill Area");
        Transform tAreaSelector = sliderObj.transform.Find("Handle Slide Area");

        if (tFondo != null)
        {
            Image img = tFondo.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.16f);

            RectTransform rt = tFondo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 6f);
        }

        if (tAreaRelleno != null)
        {
            RectTransform rtArea = tAreaRelleno.GetComponent<RectTransform>();
            rtArea.anchorMin = new Vector2(0f, 0.5f);
            rtArea.anchorMax = new Vector2(1f, 0.5f);
            rtArea.pivot = new Vector2(0.5f, 0.5f);
            rtArea.anchoredPosition = Vector2.zero;
            rtArea.sizeDelta = new Vector2(-14f, 6f);

            Transform tRelleno = tAreaRelleno.Find("Fill");

            if (tRelleno != null)
            {
                tRelleno.GetComponent<Image>().color = colorRojoSangre;
                tRelleno.GetComponent<RectTransform>().sizeDelta =
                    Vector2.zero;
            }
        }

        if (tAreaSelector != null)
        {
            RectTransform rtArea = tAreaSelector.GetComponent<RectTransform>();
            rtArea.anchorMin = new Vector2(0f, 0f);
            rtArea.anchorMax = new Vector2(1f, 1f);
            rtArea.offsetMin = new Vector2(7f, 0f);
            rtArea.offsetMax = new Vector2(-7f, 0f);

            Transform tSelector = tAreaSelector.Find("Handle");

            if (tSelector != null)
            {
                tSelector.GetComponent<Image>().color =
                    new Color(0.95f, 0.95f, 0.93f, 1f);

                tSelector.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(14f, 0f);
            }
        }
    }

    // ========================================================
    // OPCIONES DE VIDEO (sin persistencia)
    // ========================================================
    private List<Resolution> resolucionesDisponibles = new List<Resolution>();
    private int indiceResolucion;

    private readonly int[] limitesFps = { 30, 60, 120, 144, -1 }; // -1 = ilimitado
    private int indiceLimiteFps;

    private void InicializarOpcionesVideo()
    {
        // Resoluciones únicas (width x height), ignorando las repetidas por refresco
        resolucionesDisponibles.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            bool existe = resolucionesDisponibles.Exists(
                x => x.width == r.width && x.height == r.height);
            if (!existe) resolucionesDisponibles.Add(r);
        }

        // Si por algún motivo no hay lista (ej: editor), usar la actual
        if (resolucionesDisponibles.Count == 0)
            resolucionesDisponibles.Add(Screen.currentResolution);

        // Buscar la resolución actual
        indiceResolucion = resolucionesDisponibles.Count - 1;
        for (int i = 0; i < resolucionesDisponibles.Count; i++)
        {
            if (resolucionesDisponibles[i].width == Screen.width &&
                resolucionesDisponibles[i].height == Screen.height)
            {
                indiceResolucion = i;
                break;
            }
        }

        // Buscar el límite de FPS actual
        indiceLimiteFps = limitesFps.Length - 1;
        for (int i = 0; i < limitesFps.Length; i++)
        {
            if (limitesFps[i] == Application.targetFrameRate)
            {
                indiceLimiteFps = i;
                break;
            }
        }
    }

    private string TextoResolucion()
    {
        Resolution r = resolucionesDisponibles[indiceResolucion];
        return "RESOLUCIÓN: " + r.width + " x " + r.height;
    }

    private string TextoCalidad()
    {
        string nombre = QualitySettings.names[QualitySettings.GetQualityLevel()];
        return "GRÁFICOS: " + nombre.ToUpper();
    }

    private string TextoVSync()
    {
        return QualitySettings.vSyncCount > 0
            ? "VSYNC: ACTIVADO"
            : "VSYNC: DESACTIVADO";
    }

    private string TextoLimiteFps()
    {
        int fps = limitesFps[indiceLimiteFps];
        return fps < 0 ? "LÍMITE FPS: ILIMITADO" : "LÍMITE FPS: " + fps;
    }

    private void CambiarResolucion(TMPro.TMP_Text btnText)
    {
        indiceResolucion = (indiceResolucion + 1) % resolucionesDisponibles.Count;
        Resolution r = resolucionesDisponibles[indiceResolucion];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        btnText.text = TextoResolucion();
    }

    private void CambiarCalidad(TMPro.TMP_Text btnText)
    {
        int nivel = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
        QualitySettings.SetQualityLevel(nivel, true);
        btnText.text = TextoCalidad();
    }

    private void CambiarVSync(TMPro.TMP_Text btnText)
    {
        QualitySettings.vSyncCount = QualitySettings.vSyncCount > 0 ? 0 : 1;
        btnText.text = TextoVSync();
    }

    private void CambiarLimiteFps(TMPro.TMP_Text btnText)
    {
        indiceLimiteFps = (indiceLimiteFps + 1) % limitesFps.Length;
        Application.targetFrameRate = limitesFps[indiceLimiteFps];
        btnText.text = TextoLimiteFps();
    }

    // Vista previa 3D del personaje: a la DERECHA de la pantalla,
    // para no tapar el menú alineado a la izquierda.
    private RawImage CrearVistaPreviewPersonaje(Transform parent)
    {
        var go = new GameObject(
            "PreviewPersonaje", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);                    // crece hacia la izquierda
        rect.sizeDelta = new Vector2(440f, 660f);              // mantiene la proporcion 2:3 de la textura
        rect.anchoredPosition = new Vector2(-140f, -10f);      // separacion del borde derecho

        var raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }
}
