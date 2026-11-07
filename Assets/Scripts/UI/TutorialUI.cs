using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    private static readonly Color AmberColor = new Color(0.95f, 0.78f, 0.28f, 1f);
    private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.92f, 1f);
    private static readonly Color HeaderColor = new Color(0.95f, 0.25f, 0.15f, 1f);

    private const string IconsFolder = "Tutorial/";

    private const float ColumnWidth = 680f;
    private const float KeyAreaWidth = 240f;
    private const float RowHeight = 52f;
    private const float HeaderHeight = 52f;
    private const float IconSize = 54f;

    // Cada tecla es un sprite de Resources/Tutorial. "@mouse" y "@mouseL" son el mouse
    // (mirar / boton izquierdo), "~TEXTO_CON_GUIONES" es texto suelto y "/" separa alternativas.
    private static readonly string[][] LeftColumn =
    {
        new[] { "#MOVIMIENTO" },
        new[] { "W A S D", "Moverse" },
        new[] { "@mouse", "Mirar alrededor" },
        new[] { "ESPACIO", "Saltar" },
        new[] { "SHIFT", "Correr" },
        new[] { "CTRL", "Agacharse" },
        new[] { "#COMBATE" },
        new[] { "@mouseL ~CLIC_IZQ.", "Disparar / atacar" },
        new[] { "R", "Recargar" },
        new[] { "2 / 3", "Elegir arma" },
        new[] { "1", "Guardar el arma" },
    };

    private static readonly string[][] RightColumn =
    {
        new[] { "#OBJETOS" },
        new[] { "E", "Interactuar" },
        new[] { "Z / X", "Cambiar objeto" },
        new[] { "H", "Usar objeto" },
        new[] { "G", "Tirar objeto" },
        new[] { "#JUEGO" },
        new[] { "V", "Cambiar cámara" },
        new[] { "TAB", "Ver objetivo" },
        new[] { "ESC", "Menú de pausa" },
    };

    private TMP_FontAsset font;
    private float fontScale = 1f;

    public void Build(TMP_FontAsset fontAsset, float scale)
    {
        font = fontAsset;
        fontScale = scale;

        BuildColumn("Columna_Izquierda", LeftColumn, 40f);
        BuildColumn("Columna_Derecha", RightColumn, 40f + ColumnWidth + 60f);
    }

    private void BuildColumn(string objectName, string[][] entries, float x)
    {
        GameObject column = new GameObject(objectName, typeof(RectTransform));
        column.transform.SetParent(transform, false);

        RectTransform columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = new Vector2(0f, 1f);
        columnRect.anchorMax = new Vector2(0f, 1f);
        columnRect.pivot = new Vector2(0f, 1f);
        columnRect.anchoredPosition = new Vector2(x, -22f);
        columnRect.sizeDelta = new Vector2(ColumnWidth, 560f);

        float y = 0f;

        foreach (string[] entry in entries)
        {
            if (entry[0].StartsWith("#"))
            {
                TextMeshProUGUI header = CreateLabel(columnRect, "Seccion", entry[0].Substring(1), 28f, HeaderColor, TextAlignmentOptions.BottomLeft, 4f);
                Place(header.rectTransform, 0f, y, ColumnWidth, HeaderHeight - 6f);
                y += HeaderHeight;
            }
            else
            {
                BuildRow(columnRect, entry[0], entry[1], y);
                y += RowHeight;
            }
        }
    }

    private void BuildRow(RectTransform parent, string keys, string description, float y)
    {
        float x = 0f;

        foreach (string token in keys.Split(' '))
        {
            if (token == "/")
            {
                TextMeshProUGUI slash = CreateLabel(parent, "Separador", "/", 22f, AmberColor, TextAlignmentOptions.Center, 0f);
                Place(slash.rectTransform, x, y, 20f, RowHeight);
                x += 20f + 4f;
            }
            else if (token.StartsWith("~"))
            {
                x += BuildText(parent, token.Substring(1).Replace('_', ' '), x, y);
            }
            else
            {
                Sprite sprite = Resources.Load<Sprite>(IconsFolder + SpriteName(token));

                if (sprite == null)
                {
                    x += BuildText(parent, token, x, y);
                    continue;
                }

                BuildImage(parent, token, sprite, x, y + (RowHeight - IconSize) * 0.5f);
                x += IconSize + 2f;
            }
        }

        TextMeshProUGUI descriptionLabel = CreateLabel(parent, "Accion", description, 21f, TextColor, TextAlignmentOptions.MidlineLeft, 1f);
        Place(descriptionLabel.rectTransform, KeyAreaWidth + 20f, y, ColumnWidth - KeyAreaWidth - 20f, RowHeight);
    }

    private static string SpriteName(string token)
    {
        switch (token)
        {
            case "@mouse": return "mouse_move";
            case "@mouseL": return "mouse_left";
            case "ESPACIO": return "keyboard_space";
            case "SHIFT": return "keyboard_shift";
            case "CTRL": return "keyboard_ctrl";
            case "TAB": return "keyboard_tab";
            case "ESC": return "keyboard_escape";
            default: return "keyboard_" + token.ToLowerInvariant();
        }
    }

    private float BuildText(RectTransform parent, string text, float x, float y)
    {
        float width = text.Length * 16f;
        TextMeshProUGUI label = CreateLabel(parent, "Texto", text, 21f, AmberColor, TextAlignmentOptions.MidlineLeft, 2f);
        Place(label.rectTransform, x, y, width, RowHeight);
        return width + 8f;
    }

    private static void BuildImage(Transform parent, string objectName, Sprite sprite, float x, float y)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;

        Place(imageObject.GetComponent<RectTransform>(), x, y, IconSize, IconSize);
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string objectName, string text, float size, Color color, TextAlignmentOptions alignment, float letterSpacing)
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
}
