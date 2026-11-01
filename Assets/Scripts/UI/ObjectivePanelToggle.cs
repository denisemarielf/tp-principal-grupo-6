using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Paneles de objetivo/safezone que se deslizan desde un costado al mantener Tab (o alternar).
//
// Estilo: con "Apply Menu Style" activo se dibuja UN solo fondo de vidrio transparente
// (borde fino + acento rojo, como las tarjetas del menu) detras de TODOS los paneles.
// Se ajusta solo al contenido visible (iconos y textos), no se sale de la pantalla
// y se desliza junto con los paneles: entra primero y sale al final.
public class ObjectivePanelToggle : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference viewObjective;
    [Header("Paneles")]
    [SerializeField] private RectTransform[] panels;
    [SerializeField] private bool holdToShow = true; // false = Tab alterna

    [Header("Animacion")]
    [SerializeField] private float duration = 0.35f;          // tiempo de entrada/salida
    [SerializeField] private float staggerDelay = 0.08f;      // retraso entre un panel y el siguiente
    [SerializeField] private float slideDistance = 0f;        // 0 = usa el ancho del fondo/panel + margen
    [SerializeField] private float extraMargin = 40f;
    [Tooltip("Los paneles entran desde la DERECHA por defecto. Activalo para que entren desde la izquierda.")]
    [SerializeField] private bool slideFromLeft = false;

    [Header("Estilo (mismo look que el menú)")]
    [Tooltip("Agrega un fondo de 'vidrio' transparente con borde fino y acento rojo detras de los paneles.")]
    [SerializeField] private bool applyMenuStyle = true;
    [Tooltip("Margen (px) entre el contenido y el borde del fondo.")]
    [SerializeField] private float backdropPadding = 16f;

    private static readonly Color StyleGlass = new Color(0.03f, 0.035f, 0.04f, 0.58f);
    private static readonly Color StyleBorder = new Color(1f, 1f, 1f, 0.10f);
    private static readonly Color StyleRed = new Color(0.80f, 0.12f, 0.12f, 1f);

    private CanvasGroup[] groups;
    private Vector2[] shownPositions;
    private Vector2[] hiddenPositions;
    private float[] progress;
    private float[] delay;
    private bool visible;
    private Vector2 slideDirection;

    // Fondo unico
    private RectTransform backdrop;
    private CanvasGroup backdropGroup;
    private Vector2 backdropShown;
    private Vector2 backdropHidden;
    private float backdropProgress;
    private float backdropDelay;
    private float commonDistance;
    private float refreshTimer;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly List<Graphic> graphicsBuffer = new List<Graphic>();

    private void Awake()
    {
        int count = panels.Length;
        groups = new CanvasGroup[count];
        shownPositions = new Vector2[count];
        hiddenPositions = new Vector2[count];
        progress = new float[count];
        delay = new float[count];

        for (int i = 0; i < count; i++)
        {
            groups[i] = panels[i].GetComponent<CanvasGroup>();
            if (groups[i] == null)
                groups[i] = panels[i].gameObject.AddComponent<CanvasGroup>();
        }

        if (applyMenuStyle)
            CreateBackdrop();
    }

    private void Start()
    {
        // Se captura en Start para que los layouts ya hayan calculado su posicion final.
        slideDirection = slideFromLeft ? Vector2.left : Vector2.right;

        Canvas.ForceUpdateCanvases();

        // Primero se guardan TODAS las posiciones finales (antes de mover ningun panel).
        for (int i = 0; i < panels.Length; i++)
            shownPositions[i] = panels[i].anchoredPosition;

        RefreshBackdrop();
        RecomputeHiddenPositions();

        for (int i = 0; i < panels.Length; i++)
            Apply(i);

        ApplyBackdrop();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            enabled = false;
    }

  

    private void Update()
    {
      

        float target = visible ? 1f : 0f;
        float step = Time.unscaledDeltaTime / Mathf.Max(duration, 0.01f);

        // El contenido puede cambiar mientras esta visible (aparece la distancia, los civiles...).
        if (backdrop != null && (visible || backdropProgress > 0f))
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.25f;
                RefreshBackdrop();
            }
        }

        if (backdrop != null)
        {
            if (backdropDelay > 0f)
            {
                backdropDelay -= Time.unscaledDeltaTime;
            }
            else if (!Mathf.Approximately(backdropProgress, target))
            {
                backdropProgress = Mathf.MoveTowards(backdropProgress, target, step);
                ApplyBackdrop();
            }
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (delay[i] > 0f)
            {
                delay[i] -= Time.unscaledDeltaTime;
                continue;
            }

            if (!Mathf.Approximately(progress[i], target))
            {
                progress[i] = Mathf.MoveTowards(progress[i], target, step);
                Apply(i);
            }
        }
    }
    public void SetHeld(bool pressed)
    {
        if (holdToShow)
        {
            if (pressed != visible) SetVisible(pressed);
        }
        else if (pressed)
        {
            SetVisible(!visible);
        }
    }
    private void SetVisible(bool value)
    {
        visible = value;

        if (value)
            RefreshBackdrop();

        for (int i = 0; i < panels.Length; i++)
        {
            // Al entrar, el primero va primero; al salir, el orden se invierte.
            int order = value ? i : panels.Length - 1 - i;
            delay[i] = order * staggerDelay;
        }

        // El fondo entra con el primer panel y sale junto con el ultimo en irse.
        backdropDelay = value ? 0f : Mathf.Max(0, panels.Length - 1) * staggerDelay;
    }

    private void Apply(int i)
    {
        float t = progress[i];
        // Ease-out cubico: arranca rapido y frena suave al llegar.
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        panels[i].anchoredPosition = Vector2.LerpUnclamped(hiddenPositions[i], shownPositions[i], eased);
        groups[i].alpha = eased;
        groups[i].blocksRaycasts = t > 0.95f;
        groups[i].interactable = t > 0.95f;
    }

    private void ApplyBackdrop()
    {
        if (backdrop == null)
            return;

        float eased = 1f - Mathf.Pow(1f - backdropProgress, 3f);

        backdrop.anchoredPosition = Vector2.LerpUnclamped(backdropHidden, backdropShown, eased);
        backdropGroup.alpha = eased;
    }

    // Con fondo, todos los paneles y el fondo recorren la MISMA distancia, asi se ven pegados.
    private void RecomputeHiddenPositions()
    {
        for (int i = 0; i < panels.Length; i++)
        {
            float distance;

            if (slideDistance > 0f)
                distance = slideDistance;
            else if (backdrop != null && commonDistance > 0f)
                distance = commonDistance;
            else
                distance = panels[i].rect.width + extraMargin;

            hiddenPositions[i] = shownPositions[i] + slideDirection * distance;
        }
    }

    // ============================================================
    // FONDO DE VIDRIO (uno solo, detras de todos los paneles)
    // ============================================================

    private void CreateBackdrop()
    {
        if (panels.Length == 0)
            return;

        RectTransform parent = panels[0].parent as RectTransform;
        if (parent == null)
            return;

        GameObject go = new GameObject(
            "Fondo_Vidrio_Objetivos",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;   // no participa de ningun layout

        Image image = go.GetComponent<Image>();
        image.color = StyleGlass;
        image.raycastTarget = false;                            // un HUD no debe bloquear los clics

        backdropGroup = go.GetComponent<CanvasGroup>();
        backdropGroup.alpha = 0f;
        backdropGroup.blocksRaycasts = false;
        backdropGroup.interactable = false;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        const float g = 1.5f;

        CreateBorderLine(rect, "Borde_Sup", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, g), StyleBorder);
        CreateBorderLine(rect, "Borde_Inf", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, g), StyleBorder);
        CreateBorderLine(rect, "Borde_Izq", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(g, 0f), StyleBorder);
        CreateBorderLine(rect, "Borde_Der", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(g, 0f), StyleBorder);
        CreateBorderLine(rect, "Acento_Esquina", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, 3f), StyleRed);

        // Se dibuja detras del primer panel (y de los demas).
        int index = panels[0].GetSiblingIndex();
        foreach (RectTransform panel in panels)
        {
            if (panel != null && panel.parent == parent)
                index = Mathf.Min(index, panel.GetSiblingIndex());
        }
        rect.SetSiblingIndex(index);

        backdrop = rect;
    }

    // Ajusta el fondo al rectangulo que ocupan, TODOS juntos, los iconos y textos visibles de los paneles.
    private void RefreshBackdrop()
    {
        if (backdrop == null)
            return;

        RectTransform parent = (RectTransform)backdrop.parent;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        bool found = false;

        for (int i = 0; i < panels.Length; i++)
        {
            // Si el panel esta a mitad de la animacion, se mide como si estuviera en su posicion final.
            Vector3 offset = panels[i].anchoredPosition - shownPositions[i];

            panels[i].GetComponentsInChildren(false, graphicsBuffer);

            foreach (Graphic graphic in graphicsBuffer)
            {
                if (graphic.rectTransform == panels[i]) continue;   // ni el grafico propio del panel
                if (graphic.color.a < 0.02f) continue;              // ni lo invisible

                graphic.rectTransform.GetWorldCorners(corners);

                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = parent.InverseTransformPoint(corners[k]) - offset;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                    found = true;
                }
            }
        }

        backdrop.gameObject.SetActive(found);
        if (!found)
            return;

        min -= Vector2.one * backdropPadding;
        max += Vector2.one * backdropPadding;

        // Nunca se sale de la pantalla (el area del canvas que lo contiene).
        min = Vector2.Max(min, parent.rect.min);
        max = Vector2.Min(max, parent.rect.max);

        Vector2 size = Vector2.Max(max - min, Vector2.zero);
        Vector2 center = (min + max) * 0.5f;

        // Las coordenadas locales son relativas al pivote del padre; el fondo se ancla al centro de su rect.
        backdrop.sizeDelta = size;
        backdropShown = center - parent.rect.center;

        commonDistance = slideDistance > 0f ? slideDistance : size.x + extraMargin;
        backdropHidden = backdropShown + slideDirection * commonDistance;

        RecomputeHiddenPositions();
        ApplyBackdrop();
    }

    private static void CreateBorderLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        GameObject line = new GameObject(name, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);

        Image image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }
}