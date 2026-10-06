using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

public class GeneradorMenuPrincipal : MonoBehaviour
{
    // ============================================================
    // CONFIGURACIÓN
    // ============================================================

    [Header("Configuración de Escenas (3 Mapas)")]
    public string nombreEscenaMapa1 = "SampleScene";
    public string nombreEscenaMapa2 = "MapaOficinas";
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
        "Adéntrate en el bosque, localiza el punto de extracción y sobrevive a los infectados que bloquean el camino.";

    [Header("Ambientación y Arte")]
    public Sprite imagenFondoCustom;

    [Header("Tipografía Retro")]
    [Tooltip("Arrastrá acá el TMP Font Asset de tu fuente PSX/pixel.")]
    public TMP_FontAsset fuenteRetro;

    [Tooltip("Escala general de la tipografía.")]
    [Range(0.7f, 1.5f)]
    public float escalaFuenteRetro = 1.0f;

    [Header("Audio y Efectos Sonoros")]
    public AudioClip musicaFondo;
    public AudioClip sfxHover;
    public AudioClip sfxClick;


    // ============================================================
    // PANELES
    // ============================================================

    private GameObject panelPrincipal;
    private GameObject panelOpciones;
    private GameObject panelSeleccionRed;
    private GameObject panelCrearSala;
    private GameObject panelUnirseSala;
    private GameObject panelLobby;


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

    private Color colorRojoSangre =
        new Color(0.8f, 0.12f, 0.12f, 1f);

    private Color colorRojoHover =
        new Color(0.95f, 0.25f, 0.15f, 1f);

    private Color colorVerdeZombie =
        new Color(0.2f, 0.85f, 0.3f, 1f);

    private Color colorGrisOscuro =
        new Color(0.08f, 0.09f, 0.11f, 0.95f);

    private Color colorBordePanel =
        new Color(0.25f, 0.28f, 0.32f, 0.8f);


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

    private void ConstruirInterfazCompleta()
    {
        // ========================================================
        // 1. CANVAS
        // ========================================================

        GameObject canvasObj =
            new GameObject("Canvas_Menu");

        Canvas canvas =
            canvasObj.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.pixelPerfect = true;

        CanvasScaler scaler =
            canvasObj.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920, 1080);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();


        // ========================================================
        // 2. EVENT SYSTEM
        // ========================================================

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem =
                new GameObject("EventSystem");

            eventSystem.AddComponent<EventSystem>();

            eventSystem.AddComponent<InputSystemUIInputModule>();
        }


        // ========================================================
        // 3. FONDO
        // ========================================================

        GameObject fondoObj =
            new GameObject("Fondo_Villa");

        fondoObj.transform.SetParent(
            canvasObj.transform,
            false
        );

        Image imgFondo =
            fondoObj.AddComponent<Image>();

        Sprite fondoAuto =
            Resources.Load<Sprite>("FondoMenu");

        if (fondoAuto != null)
        {
            imgFondo.sprite = fondoAuto;
            imgFondo.color =
                new Color(0.6f, 0.6f, 0.65f, 1f);

            imgFondo.preserveAspect = false;
        }
        else if (imagenFondoCustom != null)
        {
            imgFondo.sprite =
                imagenFondoCustom;

            imgFondo.preserveAspect = false;
        }
        else
        {
            imgFondo.color =
                new Color(0.04f, 0.02f, 0.03f, 1f);
        }

        RectTransform rtFondo =
            fondoObj.GetComponent<RectTransform>();

        rtFondo.anchorMin =
            Vector2.zero;

        rtFondo.anchorMax =
            Vector2.one;

        rtFondo.offsetMin =
            Vector2.zero;

        rtFondo.offsetMax =
            Vector2.zero;


        // ========================================================
        // 4. VIÑETA
        // ========================================================

        GameObject vinetaObj =
            new GameObject("Sombra_Viñeta");

        vinetaObj.transform.SetParent(
            fondoObj.transform,
            false
        );

        Image imgVineta =
            vinetaObj.AddComponent<Image>();

        imgVineta.color =
            new Color(0.05f, 0.02f, 0.02f, 0.72f);

        imgVineta.raycastTarget = false;

        RectTransform rtVineta =
            vinetaObj.GetComponent<RectTransform>();

        rtVineta.anchorMin =
            Vector2.zero;

        rtVineta.anchorMax =
            Vector2.one;

        rtVineta.offsetMin =
            Vector2.zero;

        rtVineta.offsetMax =
            Vector2.zero;


        // ========================================================
        // 5. PANEL PRINCIPAL
        // ========================================================

        panelPrincipal =
            CrearPanelContenedor(
                canvasObj,
                "Panel_Principal"
            );

        CrearTitulo(
            panelPrincipal.transform,
            "ULTIMO TREN A RETIRO",
            "— SOBREVIVE EN EL CONURBANO —"
        );

        GameObject contBotonesP =
            CrearContenedorVertical(
                panelPrincipal.transform,
                new Vector2(0, -60),
                new Vector2(500, 420)
            );

        CrearBotonZombieTMP(
            contBotonesP.transform,
            "JUGAR",
            AbrirSeleccionRed,
            colorRojoSangre
        );

        CrearBotonZombieTMP(
            contBotonesP.transform,
            "OPCIONES",
            AbrirOpciones,
            new Color(0.2f, 0.22f, 0.25f)
        );

        CrearBotonZombieTMP(
            contBotonesP.transform,
            "SALIR",
            Salir,
            new Color(0.12f, 0.12f, 0.14f)
        );


        // ========================================================
        // 6. SELECCIÓN DE RED
        // ========================================================

        panelSeleccionRed =
            CrearPanelContenedor(
                canvasObj,
                "Panel_SeleccionRed"
            );

        CrearTitulo(
            panelSeleccionRed.transform,
            "MULTIJUGADOR",
            "— SELECCIONA UNA OPCIÓN —"
        );

        GameObject contBotonesRed =
            CrearContenedorVertical(
                panelSeleccionRed.transform,
                new Vector2(0, -60),
                new Vector2(500, 420)
            );

        CrearBotonZombieTMP(
            contBotonesRed.transform,
            "CREAR SALA",
            AbrirCrearSala,
            colorRojoSangre
        );

        CrearBotonZombieTMP(
            contBotonesRed.transform,
            "UNIRSE A SALA",
            AbrirUnirseSala,
            new Color(0.2f, 0.22f, 0.25f)
        );

        CrearBotonZombieTMP(
            contBotonesRed.transform,
            "VOLVER",
            VolverAMenuPrincipal,
            new Color(0.12f, 0.12f, 0.14f)
        );


        // ========================================================
        // 7. CREAR SALA
        // ========================================================

        panelCrearSala =
            CrearPanelContenedor(
                canvasObj,
                "Panel_CrearSala"
            );

        CrearTitulo(
            panelCrearSala.transform,
            "CREAR PARTIDA",
            "— CONFIGURACIÓN DE LA SALA —"
        );

        GameObject marcoCrear =
            CrearMarcoContenedor(
                panelCrearSala.transform,
                new Vector2(0, -50),
                new Vector2(550, 520)
            );

        GameObject contCrear =
            CrearContenedorVertical(
                marcoCrear.transform,
                Vector2.zero,
                new Vector2(500, 480)
            );

        inputNombreHost =
            CrearCampoTextoRetro(
                contCrear.transform,
                "NOMBRE DEL JUGADOR",
                "Host_Zombie"
            );


        // MAPA

        txtBtnMapa =
            CrearBotonOpcionCiclicaTMP(
                contCrear.transform,
                "MAPA: SUBURBIO",
                () =>
                {
                    mapaSeleccionado =
                        (mapaSeleccionado + 1) % 3;

                    string[] mapas =
                    {
                        "MAPA: SUBURBIO",
                        "MAPA: COMPLEJO DE OFICINAS",
                        "MAPA: ZONA DE EXTRACCION"
                    };

                    txtBtnMapa.text =
                        mapas[mapaSeleccionado];

                    ActualizarObjetivoMapa();
                }
            );

        // Objetivo del mapa seleccionado
        txtObjetivoMapa = CrearTextoObjetivoMapa(
            marcoCrear.transform
        );

        ActualizarObjetivoMapa();


        // DIFICULTAD

        txtBtnDificultad =
            CrearBotonOpcionCiclicaTMP(
                contCrear.transform,
                "DIFICULTAD: NORMAL",
                () =>
                {
                    dificultadSeleccionada =
                        (dificultadSeleccionada + 1) % 3;

                    string[] difs =
                    {
                        "DIFICULTAD: FÁCIL",
                        "DIFICULTAD: NORMAL",
                        "DIFICULTAD: DIFÍCIL"
                    };

                    txtBtnDificultad.text =
                        difs[dificultadSeleccionada];
                }
            );


        CrearBotonZombieTMP(
            contCrear.transform,
            "CONFIRMAR Y CREAR",
            ConfirmarCrearSala,
            colorRojoSangre
        );

        CrearBotonZombieTMP(
            contCrear.transform,
            "VOLVER",
            AbrirSeleccionRed,
            new Color(0.12f, 0.12f, 0.14f)
        );


        // ========================================================
        // 8. UNIRSE A SALA
        // ========================================================

        panelUnirseSala =
            CrearPanelContenedor(
                canvasObj,
                "Panel_UnirseSala"
            );

        CrearTitulo(
            panelUnirseSala.transform,
            "UNIRSE A SALA",
            "— CONEXIÓN POR CÓDIGO DE SALA —"
        );

        GameObject marcoUnirse =
            CrearMarcoContenedor(
                panelUnirseSala.transform,
                new Vector2(0, -50),
                new Vector2(550, 540)
            );

        GameObject contUnirse =
            CrearContenedorVertical(
                marcoUnirse.transform,
                Vector2.zero,
                new Vector2(500, 500)
            );

        inputNombreCliente =
            CrearCampoTextoRetro(
                contUnirse.transform,
                "TU NOMBRE",
                "Sobreviviente"
            );

        inputCodigoSala =
            CrearCampoTextoRetro(
                contUnirse.transform,
                "CÓDIGO DE SALA",
                ""
            );

        CrearBotonZombieTMP(
            contUnirse.transform,
            "CONECTARSE",
            ConfirmarUnirseSala,
            colorRojoSangre
        );

        CrearBotonZombieTMP(
            contUnirse.transform,
            "VOLVER",
            AbrirSeleccionRed,
            new Color(0.12f, 0.12f, 0.14f)
        );


        // ========================================================
        // 9. LOBBY
        // ========================================================

        panelLobby =
            CrearPanelContenedor(
                canvasObj,
                "Panel_Lobby"
            );

        CrearTitulo(
            panelLobby.transform,
            "LOBBY DE ESPERA",
            "— ESPERANDO SOBREVIVIENTES (MÁX 4) —"
        );


        // DETALLES

        GameObject detallesObj =
            new GameObject("Texto_DetallesLobby");

        detallesObj.transform.SetParent(
            panelLobby.transform,
            false
        );

        txtDetallesLobby =
            detallesObj.AddComponent<TextMeshProUGUI>();

        txtDetallesLobby.text =
            "ESPERANDO...";

        txtDetallesLobby.fontSize =
            24 * escalaFuenteRetro;

        txtDetallesLobby.alignment =
            TextAlignmentOptions.Center;

        txtDetallesLobby.color =
            new Color(0.95f, 0.85f, 0.3f);

        txtDetallesLobby.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txtDetallesLobby
        );

        RectTransform rtDetalles =
            detallesObj.GetComponent<RectTransform>();

        rtDetalles.anchoredPosition =
            new Vector2(0, 180);

        // Dos líneas: código de sala + detalles de la partida.
        rtDetalles.sizeDelta =
            new Vector2(900, 90);


        // MARCO

        GameObject marcoLobby =
            CrearMarcoContenedor(
                panelLobby.transform,
                new Vector2(0, -10),
                new Vector2(650, 300)
            );

        GameObject contSlots =
            CrearContenedorVertical(
                marcoLobby.transform,
                Vector2.zero,
                new Vector2(600, 270)
            );


        // SLOTS

        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj =
                new GameObject(
                    "Slot_Jugador_" + (i + 1)
                );

            slotObj.transform.SetParent(
                contSlots.transform,
                false
            );

            Image imgSlot =
                slotObj.AddComponent<Image>();

            imgSlot.color =
                new Color(
                    0.12f,
                    0.14f,
                    0.18f,
                    0.95f
                );

            RectTransform rtSlot =
                slotObj.GetComponent<RectTransform>();

            rtSlot.sizeDelta =
                new Vector2(580, 52);


            GameObject txtSlotObj =
                new GameObject("Texto_Nombre");

            txtSlotObj.transform.SetParent(
                slotObj.transform,
                false
            );

            listaRanurasJugadores[i] =
                txtSlotObj.AddComponent<TextMeshProUGUI>();

            listaRanurasJugadores[i].text =
                (i == 0)
                ? "1. Esperando Host..."
                : $"{i + 1}. [ Ranura Vacía ]";

            listaRanurasJugadores[i].fontSize =
                22 * escalaFuenteRetro;

            listaRanurasJugadores[i].fontStyle =
                FontStyles.Bold;

            listaRanurasJugadores[i].alignment =
                TextAlignmentOptions.Left;

            listaRanurasJugadores[i].color =
                (i == 0)
                ? colorVerdeZombie
                : new Color(0.5f, 0.5f, 0.5f);

            AplicarEstiloRetro(
                listaRanurasJugadores[i]
            );

            RectTransform rtTxtSlot =
                txtSlotObj.GetComponent<RectTransform>();

            rtTxtSlot.anchorMin =
                Vector2.zero;

            rtTxtSlot.anchorMax =
                Vector2.one;

            rtTxtSlot.offsetMin =
                new Vector2(25, 0);

            rtTxtSlot.offsetMax =
                new Vector2(-25, 0);
        }


        // BOTONES LOBBY

        GameObject contBotonesLobby =
            new GameObject(
                "Contenedor_BotonesLobby"
            );

        contBotonesLobby.transform.SetParent(
            panelLobby.transform,
            false
        );

        HorizontalLayoutGroup layoutH =
            contBotonesLobby.AddComponent<
                HorizontalLayoutGroup
            >();

        layoutH.spacing = 30;
        layoutH.childAlignment =
            TextAnchor.MiddleCenter;

        layoutH.childControlWidth = false;
        layoutH.childControlHeight = false;

        RectTransform rtContLobby =
            contBotonesLobby.GetComponent<RectTransform>();

        rtContLobby.anchoredPosition =
            new Vector2(0, -220);

        rtContLobby.sizeDelta =
            new Vector2(900, 80);

        btnIniciarPartida =
            CrearBotonZombieTMP(
                contBotonesLobby.transform,
                "INICIAR PARTIDA",
                IniciarPartidaDesdeLobby,
                colorRojoSangre
            );

        CrearBotonZombieTMP(
            contBotonesLobby.transform,
            "SALIR DEL LOBBY",
            SalirDelLobby,
            new Color(0.2f, 0.22f, 0.25f)
        );


        // ========================================================
        // 10. OPCIONES
        // ========================================================

        panelOpciones =
            CrearPanelContenedor(
                canvasObj,
                "Panel_Opciones"
            );

        CrearTitulo(
            panelOpciones.transform,
            "CONFIGURACIÓN",
            "— AJUSTES DEL JUEGO —"
        );

        GameObject marcoOpciones =
            CrearMarcoContenedor(
                panelOpciones.transform,
                new Vector2(0, -30),
                new Vector2(550, 360)
            );

        GameObject contOpciones =
            CrearContenedorVertical(
                marcoOpciones.transform,
                Vector2.zero,
                new Vector2(500, 320)
            );


        // VOLUMEN

        CrearControlSlider(
            contOpciones.transform,
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

        CrearBotonOpcionTMP(
            contOpciones.transform,
            txtPantalla,
            (btnText) =>
            {
                bool nuevoEstado =
                    !Screen.fullScreen;

                Screen.fullScreenMode =
                    nuevoEstado
                    ? FullScreenMode.ExclusiveFullScreen
                    : FullScreenMode.Windowed;

                Screen.fullScreen =
                    nuevoEstado;

                btnText.text =
                    nuevoEstado
                    ? "PANTALLA: COMPLETA"
                    : "PANTALLA: VENTANA";
            }
        );


        // VOLVER

        CrearBotonZombieTMP(
            contOpciones.transform,
            "VOLVER AL MENU",
            CerrarOpciones,
            new Color(0.2f, 0.22f, 0.25f)
        );


        // ========================================================
        // MOSTRAR MENÚ PRINCIPAL
        // ========================================================

        OcultarTodosLosPaneles();

        panelPrincipal.SetActive(true);

        LimpiarSeleccionUI();
    }


    // ============================================================
    // FUENTE RETRO
    // ============================================================

    private TMP_FontAsset ObtenerFuenteRetro()
    {
        // Primero usamos la asignada desde Inspector.

        if (fuenteRetro != null)
            return fuenteRetro;


        // Después intentamos buscarla automáticamente
        // en Resources/Fonts/FuenteRetro.

        TMP_FontAsset fuenteResources =
            Resources.Load<TMP_FontAsset>(
                "Fonts/FuenteRetro"
            );

        if (fuenteResources != null)
            return fuenteResources;


        // Finalmente usamos la fuente predeterminada de TMP.

        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;


        Debug.LogError(
            "NO SE ENCONTRÓ UNA FUENTE TMP. " +
            "Importá TMP Essential Resources y asigná " +
            "una TMP_FontAsset en 'Fuente Retro'."
        );

        return null;
    }


    // ============================================================
    // ESTILO RETRO
    // ============================================================

    private void AplicarEstiloRetro(
        TextMeshProUGUI texto
    )
    {
        if (texto == null)
            return;


        TMP_FontAsset fuente =
            ObtenerFuenteRetro();

        if (fuente != null)
        {
            texto.font =
                fuente;
        }


        texto.textWrappingMode = TextWrappingModes.NoWrap;

        texto.enableAutoSizing = true;
        texto.fontSizeMin = 12f;
        texto.fontSizeMax = texto.fontSize;

        texto.overflowMode =
            TextOverflowModes.Overflow;

        texto.raycastTarget =
            false;


        // Outline

        texto.outlineWidth =
            0.16f;

        texto.outlineColor =
            new Color32(
                0,
                0,
                0,
                255
            );


        // Sombra / Underlay

        if (texto.fontMaterial != null)
        {
            texto.fontMaterial.EnableKeyword(
                "UNDERLAY_ON"
            );

            texto.fontMaterial.SetColor(
                ShaderUtilities.ID_UnderlayColor,
                new Color32(
                    0,
                    0,
                    0,
                    210
                )
            );

            texto.fontMaterial.SetFloat(
                ShaderUtilities.ID_UnderlayOffsetX,
                0.8f
            );

            texto.fontMaterial.SetFloat(
                ShaderUtilities.ID_UnderlayOffsetY,
                -0.8f
            );

            texto.fontMaterial.SetFloat(
                ShaderUtilities.ID_UnderlayDilate,
                0.10f
            );

            texto.fontMaterial.SetFloat(
                ShaderUtilities.ID_UnderlaySoftness,
                0.05f
            );
        }
    }


    // ============================================================
    // OBJETIVO DEL MAPA
    // ============================================================

    private TextMeshProUGUI CrearTextoObjetivoMapa(Transform padre)
    {
        // ========================================================
        // TARJETA LATERAL DE INFORMACIÓN DEL MAPA
        // ========================================================
        // Hija directa de panelCrearSala: no pertenece a ningún
        // LayoutGroup y por lo tanto NO mueve los controles centrales.

        GameObject caja = new GameObject("Panel_Objetivo_Mapa");
        caja.transform.SetParent(padre, false);

        Image imgCaja = caja.AddComponent<Image>();
        imgCaja.color = new Color(0.045f, 0.05f, 0.06f, 0.97f);
        imgCaja.raycastTarget = true;

        RectTransform rtCaja = caja.GetComponent<RectTransform>();
        rtCaja.anchorMin = new Vector2(0.5f, 0.5f);
        rtCaja.anchorMax = new Vector2(0.5f, 0.5f);
        rtCaja.pivot = new Vector2(0.5f, 0.5f);

        // Un poquito más ancha que la versión anterior.
        rtCaja.anchoredPosition = new Vector2(545f, -45f);
        rtCaja.sizeDelta = new Vector2(440f, 350f);

        // ========================================================
        // BORDE
        // ========================================================

        GameObject borde = new GameObject("Borde_Objetivo_Mapa");
        borde.transform.SetParent(caja.transform, false);

        Image imgBorde = borde.AddComponent<Image>();
        imgBorde.color = new Color(0.27f, 0.29f, 0.32f, 0.95f);
        imgBorde.raycastTarget = false;

        RectTransform rtBorde = borde.GetComponent<RectTransform>();
        rtBorde.anchorMin = Vector2.zero;
        rtBorde.anchorMax = Vector2.one;
        rtBorde.offsetMin = new Vector2(-1.5f, -1.5f);
        rtBorde.offsetMax = new Vector2(1.5f, 1.5f);
        borde.transform.SetAsFirstSibling();

        // ========================================================
        // FRANJA SUPERIOR
        // ========================================================

        GameObject linea = new GameObject("Linea_Objetivo_Mapa");
        linea.transform.SetParent(caja.transform, false);

        Image imgLinea = linea.AddComponent<Image>();
        imgLinea.color = new Color(0.55f, 0.57f, 0.60f, 0.75f);
        imgLinea.raycastTarget = false;

        RectTransform rtLinea = linea.GetComponent<RectTransform>();
        rtLinea.anchorMin = new Vector2(0f, 1f);
        rtLinea.anchorMax = new Vector2(1f, 1f);
        rtLinea.pivot = new Vector2(0.5f, 1f);
        rtLinea.offsetMin = new Vector2(22f, -3f);
        rtLinea.offsetMax = new Vector2(-22f, 0f);

        // ========================================================
        // VIEWPORT DEL SCROLL
        // ========================================================

        GameObject viewport = new GameObject("Viewport_Objetivo_Mapa");
        viewport.transform.SetParent(caja.transform, false);

        RectTransform rtViewport = viewport.AddComponent<RectTransform>();
        rtViewport.anchorMin = Vector2.zero;
        rtViewport.anchorMax = Vector2.one;
        rtViewport.offsetMin = new Vector2(20f, 20f);
        rtViewport.offsetMax = new Vector2(-36f, -25f);

        Image imgViewport = viewport.AddComponent<Image>();
        imgViewport.color = new Color(1f, 1f, 1f, 0f);
        imgViewport.raycastTarget = true;

        RectMask2D mascara = viewport.AddComponent<RectMask2D>();

        // ========================================================
        // CONTENIDO DEL SCROLL
        // ========================================================

        GameObject contenido = new GameObject("Contenido_Objetivo_Mapa");
        contenido.transform.SetParent(viewport.transform, false);

        RectTransform rtContenido = contenido.AddComponent<RectTransform>();
        rtContenido.anchorMin = new Vector2(0f, 1f);
        rtContenido.anchorMax = new Vector2(1f, 1f);
        rtContenido.pivot = new Vector2(0.5f, 1f);
        rtContenido.anchoredPosition = Vector2.zero;
        rtContenido.sizeDelta = new Vector2(0f, 330f);

        // ========================================================
        // TEXTO
        // ========================================================

        GameObject obj = new GameObject("Texto_Objetivo_Mapa");
        obj.transform.SetParent(contenido.transform, false);

        TextMeshProUGUI txt = obj.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 25f * escalaFuenteRetro;
        txt.alignment = TextAlignmentOptions.TopLeft;
        txt.color = new Color(0.92f, 0.92f, 0.89f, 1f);
        txt.fontStyle = FontStyles.Normal;
        txt.raycastTarget = false;

        AplicarEstiloRetro(txt);

        // La tarjeta necesita WRAP real, no NoWrap.
        txt.textWrappingMode = TextWrappingModes.Normal;
        txt.enableAutoSizing = false;
        txt.fontSize = 25f * escalaFuenteRetro;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.wordSpacing = 0f;
        txt.characterSpacing = 0f;

        RectTransform rtTexto = obj.GetComponent<RectTransform>();
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = new Vector2(1f, 1f);
        rtTexto.pivot = new Vector2(0.5f, 1f);
        rtTexto.offsetMin = new Vector2(4f, 0f);
        rtTexto.offsetMax = new Vector2(-4f, 0f);

        // ========================================================
        // SCROLL RECT
        // ========================================================

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

        // ========================================================
        // BARRA DE SCROLL
        // ========================================================

        GameObject barra = new GameObject("Scrollbar_Objetivo_Mapa");
        barra.transform.SetParent(caja.transform, false);

        Image imgBarra = barra.AddComponent<Image>();
        imgBarra.color = new Color(0.10f, 0.11f, 0.13f, 0.85f);
        imgBarra.raycastTarget = true;

        RectTransform rtBarra = barra.GetComponent<RectTransform>();
        rtBarra.anchorMin = new Vector2(1f, 0f);
        rtBarra.anchorMax = new Vector2(1f, 1f);
        rtBarra.pivot = new Vector2(1f, 0.5f);
        rtBarra.offsetMin = new Vector2(-17f, 20f);
        rtBarra.offsetMax = new Vector2(-7f, -25f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(barra.transform, false);

        Image imgHandle = handle.AddComponent<Image>();
        imgHandle.color = new Color(0.42f, 0.44f, 0.47f, 0.95f);
        imgHandle.raycastTarget = true;

        Scrollbar scrollbar = barra.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = imgHandle;

        RectTransform rtHandle = handle.GetComponent<RectTransform>();
        rtHandle.anchorMin = Vector2.zero;
        rtHandle.anchorMax = Vector2.one;
        rtHandle.offsetMin = Vector2.zero;
        rtHandle.offsetMax = Vector2.zero;

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = -3f;

        // La barra debe quedar por encima del contenido visual.
        barra.transform.SetAsLastSibling();

        // El ScrollRect necesita seguir recibiendo rueda aunque el
        // texto no sea clickeable.
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
            "ZONA DE EXTRACCIÓN"
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
            "\nINFORMACIÓN DEL MAPA\n\n" +
            nombresMapas[indice] +
            "\n\n" +
            objetivo;

        txtObjetivoMapa.textWrappingMode =
            TextWrappingModes.Normal;

        txtObjetivoMapa.enableAutoSizing =
            false;

        txtObjetivoMapa.fontSize =
            25f * escalaFuenteRetro;

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

    public async void ConfirmarCrearSala()
    {
        int intento =
            PrepararLobbyRed("CREANDO SALA...");

        try
        {
            await GameSessionManager.PrepareHostAsync();

            if (intento != intentoConexion)
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
                listaRanurasJugadores[i].text =
                    (i == 0)
                    ? "1. Jugador 1 (LÍDER)"
                    : $"{i + 1}. Jugador {i + 1}";

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
            "Centro Urbano",
            "Bosque Oscuro"
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
                        ActualizarLobbyRed(data.ClientId);
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

    public void AbrirOpciones()
    {
        OcultarTodosLosPaneles();

        panelOpciones.SetActive(true);

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
    // CREAR PANEL
    // ============================================================

    private GameObject CrearPanelContenedor(
        GameObject padre,
        string nombre
    )
    {
        GameObject panel =
            new GameObject(nombre);

        panel.transform.SetParent(
            padre.transform,
            false
        );

        RectTransform rt =
            panel.AddComponent<RectTransform>();

        rt.anchorMin =
            Vector2.zero;

        rt.anchorMax =
            Vector2.one;

        rt.offsetMin =
            Vector2.zero;

        rt.offsetMax =
            Vector2.zero;

        return panel;
    }


    // ============================================================
    // MARCO
    // ============================================================

    private GameObject CrearMarcoContenedor(
        Transform padre,
        Vector2 posicion,
        Vector2 tamano
    )
    {
        GameObject marco =
            new GameObject("Marco_Fondo");

        marco.transform.SetParent(
            padre,
            false
        );

        Image imgMarco =
            marco.AddComponent<Image>();

        imgMarco.color =
            colorGrisOscuro;


        RectTransform rtMarco =
            marco.GetComponent<RectTransform>();

        rtMarco.anchoredPosition =
            posicion;

        rtMarco.sizeDelta =
            tamano;


        // Borde

        GameObject borde =
            new GameObject("Borde_Marco");

        borde.transform.SetParent(
            marco.transform,
            false
        );

        Image imgBorde =
            borde.AddComponent<Image>();

        imgBorde.color =
            colorBordePanel;

        imgBorde.raycastTarget =
            false;


        RectTransform rtBorde =
            borde.GetComponent<RectTransform>();

        rtBorde.anchorMin =
            Vector2.zero;

        rtBorde.anchorMax =
            Vector2.one;

        rtBorde.offsetMin =
            new Vector2(-3, -3);

        rtBorde.offsetMax =
            new Vector2(3, 3);

        borde.transform.SetAsFirstSibling();


        return marco;
    }


    // ============================================================
    // CONTENEDOR VERTICAL
    // ============================================================

    private GameObject CrearContenedorVertical(
        Transform padre,
        Vector2 posicion,
        Vector2 tamano
    )
    {
        GameObject contenedor =
            new GameObject(
                "Contenedor_Vertical"
            );

        contenedor.transform.SetParent(
            padre,
            false
        );

        VerticalLayoutGroup layout =
            contenedor.AddComponent<
                VerticalLayoutGroup
            >();

        layout.spacing = 16;

        layout.childAlignment =
            TextAnchor.MiddleCenter;

        layout.childControlWidth =
            false;

        layout.childControlHeight =
            false;

        RectTransform rt =
            contenedor.GetComponent<RectTransform>();

        rt.anchoredPosition =
            posicion;

        rt.sizeDelta =
            tamano;

        return contenedor;
    }


    // ============================================================
    // TÍTULO
    // ============================================================

    private void CrearTitulo(
        Transform padre,
        string tituloPrincipal,
        string subtitulo
    )
    {
        // ========================================================
        // SOMBRA
        // ========================================================

        GameObject tituloSombraObj =
            new GameObject(
                "Texto_Titulo_Sombra"
            );

        tituloSombraObj.transform.SetParent(
            padre,
            false
        );

        TextMeshProUGUI txtSombra =
            tituloSombraObj.AddComponent<
                TextMeshProUGUI
            >();

        txtSombra.text =
            tituloPrincipal;

        txtSombra.fontSize =
            78 * escalaFuenteRetro;

        txtSombra.alignment =
            TextAlignmentOptions.Center;

        txtSombra.color =
            new Color(
                0f,
                0f,
                0f,
                0.95f
            );

        txtSombra.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txtSombra
        );


        RectTransform rtSombra =
            tituloSombraObj.GetComponent<
                RectTransform
            >();

        rtSombra.anchoredPosition =
            new Vector2(5, 310);

        rtSombra.sizeDelta =
            new Vector2(1200, 150);


        // ========================================================
        // TÍTULO
        // ========================================================

        GameObject tituloObj =
            new GameObject(
                "Texto_Titulo"
            );

        tituloObj.transform.SetParent(
            padre,
            false
        );

        TextMeshProUGUI txtTitulo =
            tituloObj.AddComponent<
                TextMeshProUGUI
            >();

        txtTitulo.text =
            tituloPrincipal;

        txtTitulo.fontSize =
            78 * escalaFuenteRetro;

        txtTitulo.alignment =
            TextAlignmentOptions.Center;

        txtTitulo.color =
            colorRojoSangre;

        txtTitulo.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txtTitulo
        );


        RectTransform rtTitulo =
            tituloObj.GetComponent<
                RectTransform
            >();

        rtTitulo.anchoredPosition =
            new Vector2(0, 316);

        rtTitulo.sizeDelta =
            new Vector2(1200, 150);


        // ========================================================
        // SUBTÍTULO
        // ========================================================

        GameObject subTituloObj =
            new GameObject(
                "Texto_Subtitulo"
            );

        subTituloObj.transform.SetParent(
            padre,
            false
        );

        TextMeshProUGUI txtSub =
            subTituloObj.AddComponent<
                TextMeshProUGUI
            >();

        txtSub.text =
            subtitulo;

        txtSub.fontSize =
            22 * escalaFuenteRetro;

        txtSub.alignment =
            TextAlignmentOptions.Center;

        txtSub.color =
            new Color(
                0.85f,
                0.85f,
                0.8f,
                0.95f
            );

        txtSub.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txtSub
        );


        RectTransform rtSub =
            subTituloObj.GetComponent<
                RectTransform
            >();

        rtSub.anchoredPosition =
            new Vector2(0, 245);

        rtSub.sizeDelta =
            new Vector2(1000, 50);
    }


    // ============================================================
    // BOTÓN PRINCIPAL
    // ============================================================

    private Button CrearBotonZombieTMP(
        Transform padre,
        string texto,
        UnityEngine.Events.UnityAction accion,
        Color colorBase
    )
    {
        GameObject btnObj =
            new GameObject(
                "Btn_" + texto
            );

        btnObj.transform.SetParent(
            padre,
            false
        );


        Image imgBtn =
            btnObj.AddComponent<Image>();

        imgBtn.color =
            colorBase;


        Button btn =
            btnObj.AddComponent<Button>();

        Navigation navegacion = btn.navigation;
        navegacion.mode = Navigation.Mode.None;
        btn.navigation = navegacion;


        btn.onClick.AddListener(
            () =>
            {
                ReproducirSFX(
                    sfxClick
                );

                accion?.Invoke();
                LimpiarSeleccionUI();
            }
        );


        // Hover mejorado: color + escala + borde + texto
        // Hover mejorado: color + escala + borde + texto
        ConfigurarHoverBoton(
            btnObj,
            btn,
            imgBtn,
            colorBase
        );


        // Colores

        ColorBlock cb =
            btn.colors;

        cb.normalColor =
            colorBase;

        cb.highlightedColor =
            colorRojoHover;

        cb.pressedColor =
            new Color(
                0.35f,
                0.05f,
                0.05f
            );

        cb.selectedColor =
            cb.highlightedColor;

        cb.fadeDuration =
            0.08f;

        btn.colors =
            cb;


        // Tamaño

        RectTransform rtBtn =
            btnObj.GetComponent<
                RectTransform
            >();

        rtBtn.sizeDelta =
            new Vector2(
                440,
                62
            );


        // ========================================================
        // BORDE
        // ========================================================

        GameObject marcoObj =
            new GameObject(
                "Borde_Marco"
            );

        marcoObj.transform.SetParent(
            btnObj.transform,
            false
        );

        Image imgMarco =
            marcoObj.AddComponent<Image>();

        imgMarco.color =
            new Color(
                0.02f,
                0.02f,
                0.03f,
                0.9f
            );

        imgMarco.raycastTarget =
            false;


        RectTransform rtMarco =
            marcoObj.GetComponent<
                RectTransform
            >();

        rtMarco.anchorMin =
            Vector2.zero;

        rtMarco.anchorMax =
            Vector2.one;

        rtMarco.offsetMin =
            new Vector2(-3, -3);

        rtMarco.offsetMax =
            new Vector2(3, 3);

        marcoObj.transform.SetAsFirstSibling();


        // ========================================================
        // TEXTO
        // ========================================================

        GameObject txtObj =
            new GameObject(
                "Texto_TMP"
            );

        txtObj.transform.SetParent(
            btnObj.transform,
            false
        );

        TextMeshProUGUI txt =
            txtObj.AddComponent<
                TextMeshProUGUI
            >();

        txt.text =
            texto;

        txt.fontSize =
            25 * escalaFuenteRetro;

        txt.alignment =
            TextAlignmentOptions.Center;

        txt.color =
            new Color(
                0.98f,
                0.98f,
                0.95f
            );

        txt.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txt
        );


        RectTransform rtTxt =
            txtObj.GetComponent<
                RectTransform
            >();

        rtTxt.anchorMin =
            Vector2.zero;

        rtTxt.anchorMax =
            Vector2.one;

        rtTxt.offsetMin =
            Vector2.zero;

        rtTxt.offsetMax =
            Vector2.zero;

        return btn;
    }


    // ============================================================
    // BOTÓN CÍCLICO
    // ============================================================

    private TextMeshProUGUI CrearBotonOpcionCiclicaTMP(
        Transform padre,
        string textoInicial,
        UnityEngine.Events.UnityAction accion
    )
    {
        GameObject btnObj =
            new GameObject(
                "Btn_Ciclico"
            );

        btnObj.transform.SetParent(
            padre,
            false
        );


        Image imgBtn =
            btnObj.AddComponent<Image>();

        imgBtn.color =
            new Color(
                0.2f,
                0.22f,
                0.26f
            );


        Button btn =
            btnObj.AddComponent<Button>();

        Navigation navegacion = btn.navigation;
        navegacion.mode = Navigation.Mode.None;
        btn.navigation = navegacion;


        ColorBlock cb =
            btn.colors;

        cb.normalColor =
            new Color(
                0.2f,
                0.22f,
                0.26f
            );

        cb.highlightedColor =
            colorRojoHover;

        cb.pressedColor =
            new Color(
                0.35f,
                0.05f,
                0.05f
            );

        cb.selectedColor =
            cb.highlightedColor;

        cb.fadeDuration =
            0.08f;

        btn.colors =
            cb;


        RectTransform rtBtn =
            btnObj.GetComponent<
                RectTransform
            >();

        rtBtn.sizeDelta =
            new Vector2(
                440,
                62
            );


        // Borde

        GameObject marcoObj =
            new GameObject(
                "Borde_Marco"
            );

        marcoObj.transform.SetParent(
            btnObj.transform,
            false
        );

        Image imgMarco =
            marcoObj.AddComponent<Image>();

        imgMarco.color =
            new Color(
                0.02f,
                0.02f,
                0.03f,
                0.9f
            );

        imgMarco.raycastTarget =
            false;


        RectTransform rtMarco =
            marcoObj.GetComponent<
                RectTransform
            >();

        rtMarco.anchorMin =
            Vector2.zero;

        rtMarco.anchorMax =
            Vector2.one;

        rtMarco.offsetMin =
            new Vector2(-3, -3);

        rtMarco.offsetMax =
            new Vector2(3, 3);

        marcoObj.transform.SetAsFirstSibling();


        // Texto

        GameObject txtObj =
            new GameObject(
                "Texto_TMP"
            );

        txtObj.transform.SetParent(
            btnObj.transform,
            false
        );

        TextMeshProUGUI txt =
            txtObj.AddComponent<
                TextMeshProUGUI
            >();

        txt.text =
            textoInicial;

        txt.fontSize =
            23 * escalaFuenteRetro;

        txt.alignment =
            TextAlignmentOptions.Center;

        txt.color =
            Color.white;

        txt.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txt
        );


        RectTransform rtTxt =
            txtObj.GetComponent<
                RectTransform
            >();

        rtTxt.anchorMin =
            Vector2.zero;

        rtTxt.anchorMax =
            Vector2.one;

        rtTxt.offsetMin =
            Vector2.zero;

        rtTxt.offsetMax =
            Vector2.zero;


        btn.onClick.AddListener(
            () =>
            {
                ReproducirSFX(
                    sfxClick
                );

                accion?.Invoke();
                LimpiarSeleccionUI();
            }
        );


        // Hover mejorado: color + escala + borde + texto
        ConfigurarHoverBoton(
            btnObj,
            btn,
            imgBtn,
            new Color(0.2f, 0.22f, 0.26f)
        );


        return txt;
    }


    // ============================================================
    // INPUT FIELD TMP
    // ============================================================

    private TMP_InputField CrearCampoTextoRetro(
        Transform padre,
        string etiqueta,
        string valorPorDefecto
    )
    {
        // ========================================================
        // CONTENEDOR
        // ========================================================

        GameObject contenedor =
            new GameObject(
                "Campo_" + etiqueta
            );

        contenedor.transform.SetParent(
            padre,
            false
        );

        RectTransform rtCont =
            contenedor.AddComponent<
                RectTransform
            >();

        rtCont.sizeDelta =
            new Vector2(
                440,
                72
            );


        // ========================================================
        // ETIQUETA
        // ========================================================

        GameObject etiquetaObj =
            new GameObject(
                "Etiqueta"
            );

        etiquetaObj.transform.SetParent(
            contenedor.transform,
            false
        );

        TextMeshProUGUI txtEtiqueta =
            etiquetaObj.AddComponent<
                TextMeshProUGUI
            >();

        txtEtiqueta.text =
            etiqueta;

        txtEtiqueta.fontSize =
            17 * escalaFuenteRetro;

        txtEtiqueta.alignment =
            TextAlignmentOptions.Left;

        txtEtiqueta.color =
            new Color(
                0.85f,
                0.85f,
                0.8f
            );

        txtEtiqueta.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txtEtiqueta
        );


        RectTransform rtEtiqueta =
            etiquetaObj.GetComponent<
                RectTransform
            >();

        rtEtiqueta.anchorMin =
            new Vector2(0, 0.5f);

        rtEtiqueta.anchorMax =
            new Vector2(1, 0.5f);

        rtEtiqueta.pivot =
            new Vector2(0.5f, 0.5f);

        rtEtiqueta.anchoredPosition =
            new Vector2(0, 23);

        rtEtiqueta.sizeDelta =
            new Vector2(0, 24);


        // ========================================================
        // OBJETO INPUT
        // ========================================================

        GameObject inputObj =
            new GameObject(
                "Input_TMP"
            );

        inputObj.transform.SetParent(
            contenedor.transform,
            false
        );


        Image imgInput =
            inputObj.AddComponent<Image>();

        imgInput.color =
            new Color(
                0.035f,
                0.035f,
                0.05f,
                0.97f
            );


        TMP_InputField input =
            inputObj.AddComponent<
                TMP_InputField
            >();


        RectTransform rtInput =
            inputObj.GetComponent<
                RectTransform
            >();

        rtInput.anchorMin =
            new Vector2(0, 0);

        rtInput.anchorMax =
            new Vector2(1, 0);

        rtInput.pivot =
            new Vector2(0.5f, 0);

        rtInput.anchoredPosition =
            new Vector2(0, -3);

        rtInput.sizeDelta =
            new Vector2(0, 40);


        // ========================================================
        // TEXTO DEL INPUT
        // ========================================================

        GameObject textoObj =
            new GameObject(
                "Texto_Input"
            );

        textoObj.transform.SetParent(
            inputObj.transform,
            false
        );

        TextMeshProUGUI texto =
            textoObj.AddComponent<
                TextMeshProUGUI
            >();

        texto.text =
            valorPorDefecto;

        texto.fontSize =
            20 * escalaFuenteRetro;

        texto.alignment =
            TextAlignmentOptions.Left;

        texto.color =
            Color.white;

        texto.raycastTarget =
            false;

        AplicarEstiloRetro(
            texto
        );


        RectTransform rtTexto =
            textoObj.GetComponent<
                RectTransform
            >();

        rtTexto.anchorMin =
            Vector2.zero;

        rtTexto.anchorMax =
            Vector2.one;

        rtTexto.offsetMin =
            new Vector2(12, 0);

        rtTexto.offsetMax =
            new Vector2(-12, 0);


        // ========================================================
        // PLACEHOLDER
        // ========================================================

        GameObject placeholderObj =
            new GameObject(
                "Placeholder"
            );

        placeholderObj.transform.SetParent(
            inputObj.transform,
            false
        );

        TextMeshProUGUI placeholder =
            placeholderObj.AddComponent<
                TextMeshProUGUI
            >();

        placeholder.text =
            "Escribí aquí...";

        placeholder.fontSize =
            19 * escalaFuenteRetro;

        placeholder.alignment =
            TextAlignmentOptions.Left;

        placeholder.color =
            new Color(
                0.35f,
                0.35f,
                0.38f,
                1f
            );

        placeholder.raycastTarget =
            false;

        AplicarEstiloRetro(
            placeholder
        );


        RectTransform rtPlaceholder =
            placeholderObj.GetComponent<
                RectTransform
            >();

        rtPlaceholder.anchorMin =
            Vector2.zero;

        rtPlaceholder.anchorMax =
            Vector2.one;

        rtPlaceholder.offsetMin =
            new Vector2(12, 0);

        rtPlaceholder.offsetMax =
            new Vector2(-12, 0);


        // ========================================================
        // CONFIGURAR TMP INPUT FIELD
        // ========================================================

        input.textViewport =
            rtInput;

        input.textComponent =
            texto;

        input.placeholder =
            placeholder;

        input.text =
            valorPorDefecto;

        input.fontAsset =
            ObtenerFuenteRetro();

        input.pointSize =
            20 * escalaFuenteRetro;

        input.characterValidation =
            TMP_InputField.CharacterValidation.None;

        input.lineType =
            TMP_InputField.LineType.SingleLine;

        input.interactable =
            true;


        return input;
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
        GameObject contenedor =
            new GameObject(
                "Control_Slider_" + etiqueta
            );

        contenedor.transform.SetParent(
            padre,
            false
        );

        RectTransform rtCont =
            contenedor.AddComponent<
                RectTransform
            >();

        rtCont.sizeDelta =
            new Vector2(
                440,
                65
            );


        // Etiqueta

        GameObject txtObj =
            new GameObject(
                "Etiqueta"
            );

        txtObj.transform.SetParent(
            contenedor.transform,
            false
        );

        TextMeshProUGUI txt =
            txtObj.AddComponent<
                TextMeshProUGUI
            >();

        txt.text =
            etiqueta;

        txt.fontSize =
            20 * escalaFuenteRetro;

        txt.alignment =
            TextAlignmentOptions.Center;

        txt.color =
            new Color(
                0.9f,
                0.9f,
                0.85f
            );

        txt.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txt
        );


        RectTransform rtTxt =
            txtObj.GetComponent<
                RectTransform
            >();

        rtTxt.anchoredPosition =
            new Vector2(0, 20);

        rtTxt.sizeDelta =
            new Vector2(
                440,
                25
            );


        // Slider

        DefaultControls.Resources res =
            new DefaultControls.Resources();

        GameObject sliderObj =
            DefaultControls.CreateSlider(
                res
            );

        sliderObj.transform.SetParent(
            contenedor.transform,
            false
        );


        Slider slider =
            sliderObj.GetComponent<
                Slider
            >();

        slider.value =
            valorInicial;

        slider.onValueChanged.AddListener(
            (v) =>
            {
                alCambiarValor?.Invoke(v);
            }
        );


        RectTransform rtSlider =
            sliderObj.GetComponent<
                RectTransform
            >();

        rtSlider.anchoredPosition =
            new Vector2(0, -12);

        rtSlider.sizeDelta =
            new Vector2(
                400,
                22
            );
    }


    // ============================================================
    // BOTÓN DE OPCIÓN
    // ============================================================

    private void CrearBotonOpcionTMP(
        Transform padre,
        string textoInicial,
        System.Action<TextMeshProUGUI> accionAlClic
    )
    {
        GameObject btnObj =
            new GameObject(
                "Btn_Opcion"
            );

        btnObj.transform.SetParent(
            padre,
            false
        );


        Image imgBtn =
            btnObj.AddComponent<Image>();

        imgBtn.color =
            new Color(
                0.2f,
                0.22f,
                0.26f
            );


        Button btn =
            btnObj.AddComponent<Button>();

        Navigation navegacion = btn.navigation;
        navegacion.mode = Navigation.Mode.None;
        btn.navigation = navegacion;


        ColorBlock cb =
            btn.colors;

        cb.normalColor =
            new Color(
                0.2f,
                0.22f,
                0.26f
            );

        cb.highlightedColor =
            colorRojoHover;

        cb.pressedColor =
            new Color(
                0.35f,
                0.05f,
                0.05f
            );

        cb.selectedColor =
            cb.highlightedColor;

        cb.fadeDuration =
            0.08f;

        btn.colors =
            cb;


        RectTransform rtBtn =
            btnObj.GetComponent<
                RectTransform
            >();

        rtBtn.sizeDelta =
            new Vector2(
                440,
                62
            );


        // Borde

        GameObject marcoObj =
            new GameObject(
                "Borde_Marco"
            );

        marcoObj.transform.SetParent(
            btnObj.transform,
            false
        );

        Image imgMarco =
            marcoObj.AddComponent<Image>();

        imgMarco.color =
            new Color(
                0.02f,
                0.02f,
                0.03f,
                0.9f
            );

        imgMarco.raycastTarget =
            false;


        RectTransform rtMarco =
            marcoObj.GetComponent<
                RectTransform
            >();

        rtMarco.anchorMin =
            Vector2.zero;

        rtMarco.anchorMax =
            Vector2.one;

        rtMarco.offsetMin =
            new Vector2(-3, -3);

        rtMarco.offsetMax =
            new Vector2(3, 3);

        marcoObj.transform.SetAsFirstSibling();


        // Texto

        GameObject txtObj =
            new GameObject(
                "Texto_TMP"
            );

        txtObj.transform.SetParent(
            btnObj.transform,
            false
        );

        TextMeshProUGUI txt =
            txtObj.AddComponent<
                TextMeshProUGUI
            >();

        txt.text =
            textoInicial;

        txt.fontSize =
            23 * escalaFuenteRetro;

        txt.alignment =
            TextAlignmentOptions.Center;

        txt.color =
            Color.white;

        txt.fontStyle =
            FontStyles.Bold;

        AplicarEstiloRetro(
            txt
        );


        RectTransform rtTxt =
            txtObj.GetComponent<
                RectTransform
            >();

        rtTxt.anchorMin =
            Vector2.zero;

        rtTxt.anchorMax =
            Vector2.one;

        rtTxt.offsetMin =
            Vector2.zero;

        rtTxt.offsetMax =
            Vector2.zero;


        // Click

        btn.onClick.AddListener(
            () =>
            {
                ReproducirSFX(
                    sfxClick
                );

                accionAlClic?.Invoke(
                    txt
                );

                LimpiarSeleccionUI();
            }
        );


        // Hover mejorado: color + escala + borde + texto
        ConfigurarHoverBoton(
            btnObj,
            btn,
            imgBtn,
            new Color(0.2f, 0.22f, 0.26f)
        );
    }


    // ============================================================
    // HOVER VISUAL DE BOTONES
    // ============================================================

    private void ConfigurarHoverBoton(
    GameObject btnObj,
    Button btn,
    Image imgBtn,
    Color colorBase
)
    {
        // Desactivamos la transición automática del Button
        // para que el hover lo controlemos nosotros.
        btn.transition = Selectable.Transition.None;

        // Buscar el marco y el texto dentro del botón
        Image imgMarco = null;
        TextMeshProUGUI txt = null;

        Image[] imagenes = btnObj.GetComponentsInChildren<Image>(true);

        foreach (Image img in imagenes)
        {
            if (img != imgBtn)
            {
                imgMarco = img;
                break;
            }
        }

        txt = btnObj.GetComponentInChildren<TextMeshProUGUI>(true);

        EventTrigger trigger = btnObj.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = btnObj.AddComponent<EventTrigger>();

        // =========================
        // POINTER ENTER
        // =========================

        EventTrigger.Entry entryHover =
            new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };

        entryHover.callback.AddListener((data) =>
        {
            ReproducirSFX(sfxHover);

            btnObj.transform.localScale =
                new Vector3(1.045f, 1.045f, 1f);

            imgBtn.color = colorRojoHover;

            if (imgMarco != null)
            {
                imgMarco.color =
                    new Color(1f, 0.16f, 0.08f, 1f);
            }

            if (txt != null)
            {
                txt.color =
                    new Color(1f, 0.9f, 0.72f, 1f);
            }
        });

        trigger.triggers.Add(entryHover);

        // =========================
        // POINTER EXIT
        // =========================

        EventTrigger.Entry entryExit =
            new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };

        entryExit.callback.AddListener((data) =>
        {
            btnObj.transform.localScale = Vector3.one;

            imgBtn.color = colorBase;

            if (imgMarco != null)
            {
                imgMarco.color =
                    new Color(0.02f, 0.02f, 0.03f, 0.9f);
            }

            if (txt != null)
            {
                txt.color = Color.white;
            }
        });

        trigger.triggers.Add(entryExit);
    }
}
