using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementListUI : MonoBehaviour
{
    private static readonly Color UnlockedColor = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color LockedColor = new Color(0.45f, 0.47f, 0.5f, 1f);
    private static readonly Color UnlockedRowColor = new Color(0.1f, 0.2f, 0.12f, 0.95f);
    private static readonly Color LockedRowColor = new Color(0.055f, 0.06f, 0.07f, 0.95f);
    private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.88f, 1f);

    private const float RowHeight = 74f;

    private class Row
    {
        public AchievementDef Def;
        public Image Background;
        public Image Indicator;
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

        summary = CreateLabel(transform, "Resumen", "", 22f, UnlockedColor, TextAlignmentOptions.Center);
        RectTransform summaryRect = summary.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 1f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.pivot = new Vector2(0.5f, 1f);
        summaryRect.anchoredPosition = new Vector2(0f, -12f);
        summaryRect.sizeDelta = new Vector2(0f, 36f);

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
            row.Indicator.color = unlocked ? UnlockedColor : LockedColor;
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
        viewportRect.offsetMin = new Vector2(16f, 16f);
        viewportRect.offsetMax = new Vector2(-36f, -56f);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
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
        layout.childAlignment = TextAnchor.UpperCenter;
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

    private Scrollbar BuildScrollbar()
    {
        GameObject barObject = new GameObject("Barra_Logros", typeof(RectTransform));
        barObject.transform.SetParent(transform, false);

        Image barBackground = barObject.AddComponent<Image>();
        barBackground.color = new Color(0.12f, 0.13f, 0.15f, 1f);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.sizeDelta = new Vector2(12f, -72f);
        barRect.anchoredPosition = new Vector2(-12f, -20f);

        GameObject handleObject = new GameObject("Manija", typeof(RectTransform));
        handleObject.transform.SetParent(barObject.transform, false);

        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = UnlockedColor;

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

    private Row BuildRow(Transform parent, AchievementDef def)
    {
        GameObject rowObject = new GameObject("Logro_" + def.Id, typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        Image background = rowObject.AddComponent<Image>();

        LayoutElement rowSize = rowObject.AddComponent<LayoutElement>();
        rowSize.preferredHeight = RowHeight;

        HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 8, 8);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject indicatorObject = new GameObject("Estado", typeof(RectTransform));
        indicatorObject.transform.SetParent(rowObject.transform, false);
        Image indicator = indicatorObject.AddComponent<Image>();
        indicator.raycastTarget = false;
        LayoutElement indicatorSize = indicatorObject.AddComponent<LayoutElement>();
        indicatorSize.preferredWidth = 24f;
        indicatorSize.preferredHeight = 24f;

        GameObject textBlock = new GameObject("Textos", typeof(RectTransform));
        textBlock.transform.SetParent(rowObject.transform, false);
        LayoutElement textBlockSize = textBlock.AddComponent<LayoutElement>();
        textBlockSize.flexibleWidth = 1f;

        VerticalLayoutGroup textLayout = textBlock.AddComponent<VerticalLayoutGroup>();
        textLayout.spacing = 2f;
        textLayout.childAlignment = TextAnchor.MiddleLeft;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;

        TextMeshProUGUI nameLabel = CreateLabel(textBlock.transform, "Nombre", def.Title.ToUpperInvariant(), 24f, TextColor, TextAlignmentOptions.Left);
        AddHeight(nameLabel, 30f);

        TextMeshProUGUI descriptionLabel = CreateLabel(textBlock.transform, "Descripcion", def.Description, 16f, LockedColor, TextAlignmentOptions.Left);
        descriptionLabel.color = new Color(0.72f, 0.73f, 0.7f, 1f);
        AddHeight(descriptionLabel, 22f);

        TextMeshProUGUI statusLabel = CreateLabel(rowObject.transform, "Estado_Texto", "", 16f, LockedColor, TextAlignmentOptions.Right);
        LayoutElement statusSize = statusLabel.gameObject.AddComponent<LayoutElement>();
        statusSize.preferredWidth = 200f;

        return new Row
        {
            Def = def,
            Background = background,
            Indicator = indicator,
            Name = nameLabel,
            Status = statusLabel
        };
    }

    private void AddHeight(TextMeshProUGUI label, float height)
    {
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height * fontScale;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string objectName, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size * fontScale;
        label.alignment = alignment;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (font != null)
            label.font = font;

        return label;
    }
}
