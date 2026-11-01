using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Créditos a pantalla completa: fondo oscurecido y texto que sube solo en bucle.
//   ESC                         -> volver
//   ESPACIO / ABAJO (mantener)  -> acelerar
public class CreditosUI : MonoBehaviour
{
    // ---------------- DATOS (editá acá) ----------------
    private const string NombreJuego = "ÚLTIMO TREN A RETIRO";

    // Si querés roles: "Nombre  —  Programación"
    private static readonly string[] Integrantes =
    {
        "Jeremías Nehuén Nieva",
        "Brenda Nicole Martinez",
        "Denise Mariel Fernandez",
        "Morena Arias",
        "Felipe Monsegur"
    };

    private const string Materia = "Proyecto Profesional 1";
    private const string Universidad = "Universidad Nacional de General Sarmiento";
    private const string Cierre = "GRACIAS POR JUGAR";
    // ----------------------------------------------------

    private const float Velocidad = 70f;             // px por segundo
    private const float MultiplicadorRapido = 4f;
    private const float PausaEntreVueltas = 1.5f;    // pantalla vacía antes de reiniciar
    private const float AltoDegradado = 240f;        // desvanecido arriba y abajo

    private static readonly Color Rojo = new Color(0.95f, 0.25f, 0.15f, 1f);
    private static readonly Color ColorTexto = new Color(0.93f, 0.94f, 0.92f, 1f);
    private static readonly Color ColorEtiqueta = new Color(0.68f, 0.71f, 0.70f, 1f);

    private TMP_FontAsset fuenteTitulo;
    private TMP_FontAsset fuenteTexto;
    private float escala;
    private Action alVolver;

    private RectTransform viewport;
    private RectTransform contenido;

    private bool preparar;
    private float altoViewport;
    private float altoContenido;
    private float tiempoPausa;


    public void Build(
        TMP_FontAsset fuenteTitulo,
        TMP_FontAsset fuenteTexto,
        float escala,
        Sprite logo,
        Action alVolver)
    {
        this.fuenteTitulo = fuenteTitulo;
        this.fuenteTexto = fuenteTexto;
        this.escala = escala;
        this.alVolver = alVolver;

        // ---------- fondo oscurecido ----------
        GameObject velo = new GameObject(
            "Oscurecido", typeof(RectTransform), typeof(Image));
        velo.transform.SetParent(transform, false);
        Estirar(velo.GetComponent<RectTransform>());
        Image imgVelo = velo.GetComponent<Image>();
        imgVelo.color = new Color(0f, 0f, 0f, 0.85f);
        imgVelo.raycastTarget = true;

        // ---------- viewport con máscara ----------
        GameObject vp = new GameObject(
            "Viewport_Creditos", typeof(RectTransform), typeof(RectMask2D));
        vp.transform.SetParent(transform, false);
        viewport = vp.GetComponent<RectTransform>();
        Estirar(viewport);

        // ---------- contenido que sube ----------
        GameObject ct = new GameObject(
            "Contenido_Creditos", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        ct.transform.SetParent(viewport, false);

        contenido = ct.GetComponent<RectTransform>();
        contenido.anchorMin = new Vector2(0.5f, 1f);
        contenido.anchorMax = new Vector2(0.5f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.sizeDelta = new Vector2(1100f, 0f);

        VerticalLayoutGroup layout = ct.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = ct.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ---------- contenido ----------
        Texto(NombreJuego, 84f, Rojo, true, 110f, 4f);
        Texto("PROYECTO FINAL", 22f, ColorEtiqueta, false, 34f, 6f);

        Espaciador(120f);

        Texto("DESARROLLADO POR", 22f, ColorEtiqueta, false, 34f, 6f);
        Espaciador(8f);
        foreach (string nombre in Integrantes)
            Texto(nombre, 38f, ColorTexto, false, 54f, 1f);

        Espaciador(110f);

        Texto("MATERIA", 22f, ColorEtiqueta, false, 34f, 6f);
        Espaciador(8f);
        Texto(Materia, 38f, ColorTexto, false, 54f, 1f);

        Espaciador(110f);

        Texto("UNIVERSIDAD", 22f, ColorEtiqueta, false, 34f, 6f);
        Espaciador(8f);
        Texto(Universidad, 38f, ColorTexto, false, 54f, 1f);

        if (logo != null)
        {
            Espaciador(40f);
            Logo(logo, 200f);
        }

        Espaciador(160f);
        Texto(Cierre, 56f, Rojo, true, 80f, 4f);

        // ---------- degradados arriba y abajo (el texto se desvanece) ----------
        Sprite degradado = CrearDegradadoVertical();
        CrearDegradado("Degradado_Superior", degradado, true);
        CrearDegradado("Degradado_Inferior", degradado, false);

        // ---------- ayuda: esquina inferior derecha ----------
        TextMeshProUGUI ayuda = CrearTexto(
            transform, "Ayuda",
            "ESC  —  VOLVER\nESPACIO (MANTENER)  —  ACELERAR",
            18f, new Color(1f, 1f, 1f, 0.5f), false, 3f);

        ayuda.alignment = TextAlignmentOptions.BottomRight;

        RectTransform rtAyuda = ayuda.rectTransform;
        rtAyuda.anchorMin = new Vector2(1f, 0f);
        rtAyuda.anchorMax = new Vector2(1f, 0f);
        rtAyuda.pivot = new Vector2(1f, 0f);
        rtAyuda.anchoredPosition = new Vector2(-48f, 36f);
        rtAyuda.sizeDelta = new Vector2(520f, 64f);
    }


    // ============================================================
    // ANIMACIÓN
    // ============================================================

    private void OnEnable()
    {
        // El tamaño real se mide en el primer Update, cuando el layout ya existe.
        preparar = true;
    }


    private void Update()
    {
        if (contenido == null)
            return;

        Keyboard kb = Keyboard.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            alVolver?.Invoke();
            return;
        }

        if (preparar)
        {
            Preparar();
            preparar = false;
        }

        Vector2 pos = contenido.anchoredPosition;

        // Ya salió todo por arriba: pausa breve y vuelve a entrar desde abajo.
        if (pos.y >= altoContenido)
        {
            tiempoPausa += Time.unscaledDeltaTime;

            if (tiempoPausa >= PausaEntreVueltas)
                Preparar();

            return;
        }

        bool rapido =
            kb != null && (kb.spaceKey.isPressed || kb.downArrowKey.isPressed);

        float v = Velocidad * (rapido ? MultiplicadorRapido : 1f);

        pos.y += v * Time.unscaledDeltaTime;
        contenido.anchoredPosition = pos;
    }


    // Mide y deja el contenido justo debajo de la pantalla.
    private void Preparar()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contenido);

        altoViewport = viewport.rect.height;
        altoContenido = contenido.rect.height;
        tiempoPausa = 0f;

        contenido.anchoredPosition = new Vector2(0f, -altoViewport);
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }


    // Sprite de 1x256: opaco arriba, transparente abajo.
    private static Sprite CrearDegradadoVertical()
    {
        const int alto = 256;

        Texture2D tex = new Texture2D(1, alto, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] px = new Color32[alto];

        for (int y = 0; y < alto; y++)
        {
            float u = y / (float)(alto - 1);      // 0 abajo, 1 arriba
            byte a = (byte)Mathf.RoundToInt(Mathf.Pow(u, 1.4f) * 255f);
            px[y] = new Color32(0, 0, 0, a);
        }

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(
            tex, new Rect(0, 0, 1, alto), new Vector2(0.5f, 0.5f), 100f);
    }


    private void CrearDegradado(string nombre, Sprite sprite, bool arriba)
    {
        GameObject go = new GameObject(
            nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, arriba ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, arriba ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, arriba ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, AltoDegradado);

        // El de abajo es el mismo sprite dado vuelta.
        if (!arriba)
            rt.localScale = new Vector3(1f, -1f, 1f);
    }


    private void Texto(
        string contenidoTexto, float tamano, Color color,
        bool esTitulo, float alto, float espaciadoLetras)
    {
        TextMeshProUGUI t = CrearTexto(
            contenido, "Texto", contenidoTexto, tamano, color,
            esTitulo, espaciadoLetras);

        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;
    }


    private TextMeshProUGUI CrearTexto(
        Transform padre, string nombre, string texto, float tamano,
        Color color, bool esTitulo, float espaciadoLetras)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tamano * escala;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.characterSpacing = espaciadoLetras;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;

        TMP_FontAsset fuente = esTitulo ? fuenteTitulo : fuenteTexto;
        if (fuente != null)
            t.font = fuente;

        return t;
    }


    private void Espaciador(float alto)
    {
        GameObject go = new GameObject(
            "Espaciador", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(contenido, false);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;
    }


    private void Logo(Sprite sprite, float alto)
    {
        GameObject go = new GameObject(
            "Logo_Universidad", typeof(RectTransform),
            typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(contenido, false);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minHeight = alto;
        le.preferredHeight = alto;
    }
}