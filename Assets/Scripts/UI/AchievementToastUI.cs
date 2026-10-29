using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementToastUI : MonoBehaviour
{
    private const float VisibleSeconds = 4f;
    private const float FadeSeconds = 0.4f;

    private static AchievementToastUI instance;

    private readonly Queue<AchievementDef> pending = new Queue<AchievementDef>();
    private CanvasGroup group;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI descriptionLabel;
    private bool showing;

    public static void Show(AchievementDef def)
    {
        if (instance == null)
        {
            GameObject host = new GameObject("AchievementToast");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<AchievementToastUI>();
            instance.BuildUi();
        }

        instance.pending.Enqueue(def);
        if (!instance.showing)
            instance.StartCoroutine(instance.ShowQueue());
    }

    private IEnumerator ShowQueue()
    {
        showing = true;

        while (pending.Count > 0)
        {
            AchievementDef def = pending.Dequeue();
            nameLabel.text = def.Title;
            descriptionLabel.text = def.Description;

            yield return Fade(0f, 1f);
            yield return new WaitForSecondsRealtime(VisibleSeconds);
            yield return Fade(1f, 0f);
        }

        showing = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
            yield return null;
        }

        group.alpha = to;
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // por debajo del menu de pausa (100)

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-30f, -120f);
        panelRect.sizeDelta = new Vector2(460f, 110f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);
        background.raycastTarget = false;

        group = panel.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject accent = new GameObject("Acento", typeof(RectTransform));
        accent.transform.SetParent(panel.transform, false);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.95f, 0.75f, 0.2f);
        accentImage.raycastTarget = false;
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(8f, 0f);

        GameObject content = new GameObject("Contenido", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 16, 12, 12);
        layout.spacing = 2;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(content.transform, "¡LOGRO DESBLOQUEADO!", 16, new Color(0.95f, 0.75f, 0.2f), 22);
        nameLabel = CreateLabel(content.transform, "", 28, new Color(0.95f, 0.95f, 0.9f), 36);
        descriptionLabel = CreateLabel(content.transform, "", 18, new Color(0.75f, 0.75f, 0.72f), 24);
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize, Color color, float height)
    {
        GameObject labelObject = new GameObject("Texto", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Left;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        return label;
    }
}
