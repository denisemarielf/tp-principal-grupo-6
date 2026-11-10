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
        card.raycastTarget = false;
        // Recorta el texto si no entra: evita que el resumen se dibuje fuera de pantalla.
        root.AddComponent<RectMask2D>();
        AddBorder(rect);

        GameObject textGo = new GameObject("Texto", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(22f, 16f);
        textRect.offsetMax = new Vector2(-22f, -16f);

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = TextMain;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Truncate;
        label.richText = true;

        TMP_FontAsset font = GeneradorMenuPrincipal.FuenteTextoActual;
        if (font == null)
            font = TMP_Settings.defaultFontAsset;
        if (font != null)
            label.font = font;

        MatchBoardWidget widget = root.AddComponent<MatchBoardWidget>();
        widget.body = label;
        widget.Apply(null);
        return widget;
    }

    public void Apply(MatchBoard board)
    {
        if (body != null)
            body.text = MatchBoardText.Format(board);
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
