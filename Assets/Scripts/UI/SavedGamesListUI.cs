using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SavedGamesListUI : MonoBehaviour
{
    private static readonly Color GreenColor = new Color(0.2f, 0.85f, 0.3f, 1f);
    private static readonly Color AmberColor = new Color(0.95f, 0.78f, 0.28f, 1f);
    private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.92f, 1f);
    private static readonly Color DescriptionColor = new Color(0.68f, 0.71f, 0.70f, 1f);
    private static readonly Color MutedColor = new Color(0.52f, 0.55f, 0.54f, 1f);
    private static readonly Color RedHoverColor = new Color(0.95f, 0.25f, 0.15f, 1f);

    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color RowHoverColor = new Color(0.95f, 0.25f, 0.15f, 0.40f);
    private static readonly Color RowSelectedColor = new Color(0.95f, 0.25f, 0.15f, 0.28f);
    private static readonly Color BarColor = new Color(1f, 1f, 1f, 0.28f);

    private const float RowHeight = 82f;

    private static Sprite fadeSprite;

    private TMP_FontAsset font;
    private float fontScale = 1f;
    private Func<string, string> mapNameOf;
    private Action<SavedGameInfo> onSelect;
    private Action onHover;
    private RectTransform content;
    private TextMeshProUGUI summary;
    private readonly List<RowHover> rows = new List<RowHover>();

    // mapNameResolver devuelve el nombre del mapa de una escena, o null si ese mapa ya no existe.
    // selectCallback recibe la partida elegida, o null cuando no hay ninguna elegida.
    public void Build(TMP_FontAsset fontAsset, float scale, Func<string, string> mapNameResolver, Action<SavedGameInfo> selectCallback, Action hoverCallback)
    {
        font = fontAsset;
        fontScale = scale;
        mapNameOf = mapNameResolver;
        onSelect = selectCallback;
        onHover = hoverCallback;

        summary = CreateLabel(transform, "Resumen", "", 22f, GreenColor, TextAlignmentOptions.MidlineLeft, 3f);
        RectTransform summaryRect = summary.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 1f);
        summaryRect.anchorMax = new Vector2(1f, 1f);
        summaryRect.pivot = new Vector2(0f, 1f);
        summaryRect.anchoredPosition = new Vector2(32f, -20f);
        summaryRect.sizeDelta = new Vector2(-64f, 34f);

        content = BuildScroll();
        Refresh();
    }

    private void OnEnable()
    {
        if (content != null)
            Refresh();
    }

    public void Refresh(string message = null)
    {
        rows.Clear();

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject old = content.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        List<SavedGameInfo> saves = SaveSystem.ListSaves();
        int valid = 0;

        foreach (SavedGameInfo info in saves)
        {
            if (info.IsValid) valid++;
            BuildRow(info);
        }

        int damaged = saves.Count - valid;

        if (!string.IsNullOrEmpty(message))
        {
            summary.text = message;
            summary.color = AmberColor;
        }
        else if (saves.Count == 0)
        {
            summary.text = "NO HAY PARTIDAS GUARDADAS";
            summary.color = AmberColor;
        }
        else if (damaged > 0)
        {
            summary.text = $"PARTIDAS GUARDADAS: {valid}   |   ARCHIVOS DAÑADOS: {damaged}";
            summary.color = AmberColor;
        }
        else
        {
            summary.text = $"PARTIDAS GUARDADAS: {valid}";
            summary.color = GreenColor;
        }

        onSelect?.Invoke(null);
    }

    private void Select(RowHover chosen)
    {
        foreach (RowHover row in rows)
            row.SetSelected(row == chosen);

        onSelect?.Invoke(chosen.Info);
    }

    private RectTransform BuildScroll()
    {
        GameObject viewport = new GameObject("Viewport_Partidas", typeof(RectTransform));
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

        GameObject contentObject = new GameObject("Contenido_Partidas", typeof(RectTransform));
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

    private Scrollbar BuildScrollbar()
    {
        GameObject barObject = new GameObject("Barra_Partidas", typeof(RectTransform));
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

    private void BuildRow(SavedGameInfo info)
    {
        string mapName = info.IsValid ? mapNameOf(info.Data.sceneName) : null;
        bool selectable = mapName != null;

        string title;
        string detail;
        string status;

        if (!info.IsValid)
        {
            title = "ARCHIVO DAÑADO";
            detail = Path.GetFileName(info.Path);
            status = "NO SE PUEDE CARGAR";
        }
        else
        {
            SaveSystem.TryGetSavedAtLocal(info.Data, out DateTime date);
            List<string> names = new List<string>();
            foreach (PlayerSaveData player in info.Data.players)
                names.Add(player.playerName);

            title = selectable
                ? $"{mapName}  |  {SaveSystem.ModeLabel(info.Data.mode)}"
                : "MAPA NO DISPONIBLE";
            string nextName = info.Data.levelCompleted
                ? mapNameOf(PlayerCarryOver.NextSceneAfter(info.Data.sceneName))
                : null;

            detail = (nextName != null ? $"NIVEL COMPLETADO  >  CONTINÚA EN: {nextName}   |   " : "")
                + "JUGADORES: " + string.Join(", ", names).ToUpperInvariant();
            status = date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                + "\n" + date.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        GameObject rowObject = new GameObject("Partida", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        rowObject.transform.SetParent(content, false);

        Image background = rowObject.GetComponent<Image>();
        background.sprite = GetFadeSprite();
        background.color = RowColor;
        background.canvasRenderer.cullTransparentMesh = false;

        LayoutElement rowSize = rowObject.GetComponent<LayoutElement>();
        rowSize.minHeight = RowHeight;
        rowSize.preferredHeight = RowHeight;

        GameObject accentObject = new GameObject("Barra_Acento", typeof(RectTransform), typeof(Image));
        accentObject.transform.SetParent(rowObject.transform, false);
        Image accent = accentObject.GetComponent<Image>();
        accent.color = BarColor;
        accent.raycastTarget = false;

        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(3f, -14f);

        TextMeshProUGUI titleLabel = CreateLabel(rowObject.transform, "Titulo", title, 26f, selectable ? TextColor : MutedColor, TextAlignmentOptions.MidlineLeft, 2f);
        RectTransform titleRect = titleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(28f, titleRect.offsetMin.y);
        titleRect.offsetMax = new Vector2(-230f, titleRect.offsetMax.y);
        titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, -10f);
        titleRect.sizeDelta = new Vector2(titleRect.sizeDelta.x, 32f);

        TextMeshProUGUI detailLabel = CreateLabel(rowObject.transform, "Detalle", detail, 18f, DescriptionColor, TextAlignmentOptions.MidlineLeft, 0.5f);
        detailLabel.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform detailRect = detailLabel.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 1f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.pivot = new Vector2(0f, 1f);
        detailRect.offsetMin = new Vector2(28f, detailRect.offsetMin.y);
        detailRect.offsetMax = new Vector2(-230f, detailRect.offsetMax.y);
        detailRect.anchoredPosition = new Vector2(detailRect.anchoredPosition.x, -46f);
        detailRect.sizeDelta = new Vector2(detailRect.sizeDelta.x, 24f);

        TextMeshProUGUI statusLabel = CreateLabel(rowObject.transform, "Estado_Texto", status, 18f, selectable ? AmberColor : MutedColor, TextAlignmentOptions.MidlineRight, 3f);
        RectTransform statusRect = statusLabel.rectTransform;
        statusRect.anchorMin = new Vector2(1f, 0f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(1f, 0.5f);
        statusRect.anchoredPosition = new Vector2(-20f, 0f);
        statusRect.sizeDelta = new Vector2(200f, 0f);

        Button button = rowObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.interactable = selectable;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        RowHover hover = rowObject.AddComponent<RowHover>();
        hover.Configure(info, button, background, accent, onHover);
        rows.Add(hover);

        button.onClick.AddListener(() => Select(hover));
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

    private sealed class RowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Button button;
        private Image background;
        private Image accent;
        private Action onEnter;
        private bool selected;

        public SavedGameInfo Info { get; private set; }

        public void Configure(SavedGameInfo info, Button rowButton, Image rowBackground, Image rowAccent, Action enterCallback)
        {
            Info = info;
            button = rowButton;
            background = rowBackground;
            accent = rowAccent;
            onEnter = enterCallback;
        }

        public void SetSelected(bool value)
        {
            selected = value;
            Rest();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || !button.interactable)
                return;

            background.color = RowHoverColor;
            accent.color = RedHoverColor;
            onEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Rest();
        }

        private void OnDisable()
        {
            Rest();
        }

        private void Rest()
        {
            if (background != null) background.color = selected ? RowSelectedColor : RowColor;
            if (accent != null) accent.color = selected ? RedHoverColor : BarColor;
        }
    }
}
