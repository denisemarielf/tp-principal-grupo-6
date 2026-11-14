using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tarjeta de vidrio con el tablero. La pantalla de fin y el panel de nivel completado la crean en código.
public class MatchBoardWidget : MonoBehaviour
{
    private static readonly Color Glass = new Color(0.03f, 0.035f, 0.04f, 0.72f);
    private static readonly Color Border = new Color(1f, 1f, 1f, 0.10f);
    private static readonly Color Accent = new Color(0.80f, 0.12f, 0.12f, 1f);
    private static readonly Color TextMain = new Color(0.93f, 0.94f, 0.92f, 1f);

    private TextMeshProUGUI body;
    private RectTransform rootRect;
    private RectTransform contentRect;
    private ScrollRect scroll;
    private float maxHeight = 560f;

    public static MatchBoardWidget Create(
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject root = new GameObject("Tablero", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image card = root.AddComponent<Image>();
        card.color = Glass;
        card.raycastTarget = true;
        AddBorder(rect);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportGo.transform.SetParent(root.transform, false);
        RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-8f, -8f);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, size.y);

        GameObject textGo = new GameObject("Texto", typeof(RectTransform));
        textGo.transform.SetParent(contentGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -8f);
        textRect.sizeDelta = new Vector2(-28f, size.y);

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = TextMain;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.richText = true;

        TMP_FontAsset font = GeneradorMenuPrincipal.FuenteTextoActual;
        if (font == null)
            font = TMP_Settings.defaultFontAsset;
        if (font != null)
            label.font = font;

        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;

        MatchBoardWidget widget = root.AddComponent<MatchBoardWidget>();
        widget.body = label;
        widget.rootRect = rect;
        widget.contentRect = contentRect;
        widget.scroll = scroll;
        widget.maxHeight = Mathf.Max(size.y, 520f);
        widget.Apply(null);
        return widget;
    }

    public void Apply(MatchBoard board)
    {
        if (body == null)
            return;

        body.text = MatchBoardText.Format(board);

        int players = board != null && board.Players != null ? board.Players.Count : 0;
        float textHeight = 168f + Mathf.Max(players, 1) * 64f;
        RectTransform textRect = body.rectTransform;
        textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, textHeight);

        if (contentRect != null)
            contentRect.sizeDelta = new Vector2(0f, textHeight + 24f);

        if (rootRect != null)
        {
            float cardHeight = Mathf.Min(textHeight + 28f, maxHeight);
            rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, cardHeight);
        }

        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;
    }

    private static void AddBorder(RectTransform frame)
    {
        const float thickness = 1.5f;
        CreateLine(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), Border);
        CreateLine(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), Border);
        CreateLine(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), Border);
        CreateLine(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), Border);
        CreateLine(frame, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, 3f), Accent);
    }

    private static void CreateLine(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        GameObject go = new GameObject("Borde", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }
}
