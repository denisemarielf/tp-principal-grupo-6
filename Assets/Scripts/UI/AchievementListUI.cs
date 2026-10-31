using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementListUI : MonoBehaviour
{
    private static readonly Color UnlockedColor = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color LockedColor = new Color(0.52f, 0.55f, 0.54f, 1f);
    private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.92f, 1f);
    private static readonly Color DescriptionColor = new Color(0.68f, 0.71f, 0.70f, 1f);

    // Filas transparentes: bloqueada = velo blanco tenue, desbloqueada = velo verde.
    private static readonly Color UnlockedRowColor = new Color(0.2f, 0.85f, 0.3f, 0.16f);
    private static readonly Color LockedRowColor = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color LockedBarColor = new Color(1f, 1f, 1f, 0.28f);

    private const float RowHeight = 70f;

    private static Sprite fadeSprite;

    private class Row
    {
        public AchievementDef Def;
        public Image Background;
        public Image Indicator;   // barra de acento a la izquierda de la fila
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Status;
    }

    private readonly List<Row> rows = new List<Row>();
    private TextMeshProUGUI summary;
    private TMP_FontAsset font;
    private float fontScale = 1f;

    public void Build(TMP_FontAsset fontAsset, float scale)
    {
        font = fontAsset;
        fontScale = scale;

        // Resumen arriba a la izquierda, alineado con el resto del menú.
        summary = CreateLabel(transform, "Resumen", "", 22f, UnlockedColor, TextAlignmentOptions.MidlineLeft, 3f);
        RectTransform summaryRect = summary.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 1f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.pivot = new Vector2(0f, 1f);
        summaryRect.anchoredPosition = new Vector2(32f, -20f);
        summaryRect.sizeDelta = new Vector2(-64f, 34f);

        RectTransform content = BuildScroll();
        foreach (AchievementDef def in AchievementCatalog.All)
            rows.Add(BuildRow(content, def));

        Refresh();
    }

    private void OnEnable()
    {
        if (rows.Count > 0)
            Refresh();
    }

    public void Refresh()
    {
        int unlockedCount = 0;

        foreach (Row row in rows)
        {
            bool unlocked = AchievementStore.IsUnlocked(row.Def.Id);
            if (unlocked) unlockedCount++;

            row.Background.color = unlocked ? UnlockedRowColor : LockedRowColor;
            row.Indicator.color = unlocked ? UnlockedColor : LockedBarColor;
            row.Name.color = unlocked ? UnlockedColor : TextColor;
            row.Status.text = unlocked ? "DESBLOQUEADO" : "BLOQUEADO";
            row.Status.color = unlocked ? UnlockedColor : LockedColor;
        }

        summary.text = $"DESBLOQUEADOS: {unlockedCount} / {rows.Count}";
    }

    private RectTransform BuildScroll()
    {
        GameObject viewport = new GameObject("Viewport_Logros", typeof(RectTransform));
        viewport.transform.SetParent(transform, false);

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(28f, 24f);
        viewportRect.offsetMax = new Vector2(-40f, -66f);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.canvasRenderer.cullTransparentMesh = false;
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = new GameObject("Contenido_Logros", typeof(RectTransform));
        contentObject.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        scroll.verticalScrollbar = BuildScrollbar();
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        return contentRect;
    }

    // Barra fina y discreta, igual que la del panel de objetivo del mapa.
    private Scrollbar BuildScrollbar()
    {
        GameObject barObject = new GameObject("Barra_Logros", typeof(RectTransform));
        barObject.transform.SetParent(transform, false);

        Image barBackground = barObject.AddComponent<Image>();
        barBackground.color = new Color(1f, 1f, 1f, 0.06f);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-22f, 24f);
        barRect.offsetMax = new Vector2(-16f, -66f);

        GameObject handleObject = new GameObject("Manija", typeof(RectTransform));
        handleObject.transform.SetParent(barObject.transform, false);

        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(1f, 1f, 1f, 0.38f);

        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        Scrollbar scrollbar = barObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    // Sin layout groups anidados: cada texto se posiciona con anclas fijas,
    // así nunca puede colapsar a ancho/alto cero.
    private Row BuildRow(Transform parent, AchievementDef def)
    {
        GameObject rowObject = new GameObject("Logro_" + def.Id, typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        // Fondo con degradado: se desvanece hacia la derecha.
        Image background = rowObject.AddComponent<Image>();
        background.sprite = GetFadeSprite();
        background.raycastTarget = false;

        // El único layout es el del contenido (VerticalLayoutGroup del scroll).
        LayoutElement rowSize = rowObject.AddComponent<LayoutElement>();
        rowSize.minHeight = RowHeight;
        rowSize.preferredHeight = RowHeight;

        // Barra de acento a la izquierda
        GameObject indicatorObject = new GameObject("Estado", typeof(RectTransform));
        indicatorObject.transform.SetParent(rowObject.transform, false);
        Image indicator = indicatorObject.AddComponent<Image>();
        indicator.raycastTarget = false;

        RectTransform indicatorRect = indicatorObject.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0f, 0f);
        indicatorRect.anchorMax = new Vector2(0f, 1f);
        indicatorRect.pivot = new Vector2(0f, 0.5f);
        indicatorRect.anchoredPosition = Vector2.zero;
        indicatorRect.sizeDelta = new Vector2(3f, -14f);

        // Nombre (arriba)
        TextMeshProUGUI nameLabel = CreateLabel(rowObject.transform, "Nombre", def.Title.ToUpperInvariant(), 26f, TextColor, TextAlignmentOptions.MidlineLeft, 2f);
        RectTransform nameRect = nameLabel.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.offsetMin = new Vector2(28f, nameRect.offsetMin.y);
        nameRect.offsetMax = new Vector2(-230f, nameRect.offsetMax.y);
        nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, -8f);
        nameRect.sizeDelta = new Vector2(nameRect.sizeDelta.x, 32f);

        // Descripción (abajo)
        TextMeshProUGUI descriptionLabel = CreateLabel(rowObject.transform, "Descripcion", def.Description, 18f, DescriptionColor, TextAlignmentOptions.MidlineLeft, 0.5f);
        RectTransform descRect = descriptionLabel.rectTransform;
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descRect.offsetMin = new Vector2(28f, descRect.offsetMin.y);
        descRect.offsetMax = new Vector2(-230f, descRect.offsetMax.y);
        descRect.anchoredPosition = new Vector2(descRect.anchoredPosition.x, -40f);
        descRect.sizeDelta = new Vector2(descRect.sizeDelta.x, 24f);

        // Estado (derecha, centrado en vertical)
        TextMeshProUGUI statusLabel = CreateLabel(rowObject.transform, "Estado_Texto", "", 18f, LockedColor, TextAlignmentOptions.MidlineRight, 3f);
        RectTransform statusRect = statusLabel.rectTransform;
        statusRect.anchorMin = new Vector2(1f, 0f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(1f, 0.5f);
        statusRect.anchoredPosition = new Vector2(-20f, 0f);
        statusRect.sizeDelta = new Vector2(200f, 0f);

        return new Row
        {
            Def = def,
            Background = background,
            Indicator = indicator,
            Name = nameLabel,
            Status = statusLabel
        };
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string objectName, string text, float size, Color color, TextAlignmentOptions alignment, float letterSpacing = 0f)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size * fontScale;
        label.alignment = alignment;
        label.color = color;
        label.characterSpacing = letterSpacing;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        if (font != null)
            label.font = font;

        return label;
    }

    // Sprite blanco 256x1 cuyo alpha baja de 1 a 0 (izquierda -> derecha).
    private static Sprite GetFadeSprite()
    {
        if (fadeSprite != null)
            return fadeSprite;

        const int width = 256;
        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[width];
        for (int i = 0; i < width; i++)
        {
            float u = i / (float)(width - 1);
            pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt((1f - u) * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        fadeSprite = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 100f);
        return fadeSprite;
    }
}