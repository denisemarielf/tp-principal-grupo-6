using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Columna de armas: ícono y número de tecla. La equipada se ve más grande y más clara.
public class WeaponSlotHUD : MonoBehaviour
{
    private static readonly Color Red = new Color(0.95f, 0.22f, 0.16f, 1f);
    private static readonly Color BoneDim = new Color(0.93f, 0.94f, 0.92f, 0.45f);

    // Columna del número de tecla, pegada al borde derecho; el dibujo va a su izquierda.
    private const float KeyColumnWidth = 40f;
    private const float KeyGap = 4f;

    private WeaponSwitcher switcher;
    private WeaponLoadout loadout;
    private Image icon2;
    private Image icon3;
    private TMP_Text key2;
    private TMP_Text key3;
    private TMP_Text fallback2;
    private TMP_Text fallback3;
    private RectTransform row2;
    private RectTransform row3;

    private void Start()
    {
        switcher = GetComponent<WeaponSwitcher>();
        loadout = GetComponent<WeaponLoadout>();
        if (switcher == null || !switcher.IsOwner)
        {
            enabled = false;
            return;
        }

        StartCoroutine(BuildWhenHudReady());
    }

    private IEnumerator BuildWhenHudReady()
    {
        // El HUD (y su AmmoUI) cuelga de la raiz del jugador, no de este objeto.
        AmmoUI ammo = null;
        while (ammo == null)
        {
            ammo = transform.root.GetComponentInChildren<AmmoUI>(true);
            if (ammo == null)
                yield return null;
        }

        Canvas canvas = ammo.GetComponentInParent<Canvas>();
        Build(canvas != null ? canvas.transform : ammo.transform.root, AmmoIconSize(ammo));
    }

    // Mismo recuadro que el dibujo del arma en el panel de municion (ImageArm).
    private static Vector2 AmmoIconSize(AmmoUI ammo)
    {
        foreach (Image image in ammo.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == "ImageArm")
                return image.rectTransform.rect.size;
        }

        return new Vector2(60f, 65f);
    }

    private void Build(Transform parent, Vector2 iconSize)
    {
        GameObject root = new GameObject("WeaponSlots", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        // Abajo a la derecha, arriba del panel de municion.
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 130f);
        rect.sizeDelta = new Vector2(250f, 160f);

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = GeneradorMenuPrincipal.FuenteTextoActual;

        row2 = CreateRow(root.transform, font, iconSize, 0f, out icon2, out key2, out fallback2);
        row3 = CreateRow(root.transform, font, iconSize, -82f, out icon3, out key3, out fallback3);
        key2.text = "2";
        key3.text = "3";
    }

    private static RectTransform CreateRow(Transform parent, TMP_FontAsset font, Vector2 iconSize, float y, out Image icon, out TMP_Text key, out TMP_Text fallback)
    {
        GameObject row = new GameObject("Fila", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        // Fila alineada a la derecha: el número queda en el borde derecho y el dibujo/nombre a su izquierda.
        rowRect.anchorMin = new Vector2(1f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(1f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, y);
        rowRect.sizeDelta = new Vector2(240f, 72f);

        GameObject iconGo = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(row.transform, false);
        icon = iconGo.GetComponent<Image>();
        icon.raycastTarget = false;
        // Igual que ImageArm: el dibujo llena el recuadro, sin letterbox.
        icon.preserveAspect = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(1f, 0.5f);
        iconRect.anchorMax = new Vector2(1f, 0.5f);
        iconRect.pivot = new Vector2(1f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-KeyColumnWidth - KeyGap, 0f);
        iconRect.sizeDelta = iconSize;

        GameObject fallbackGo = new GameObject("Nombre", typeof(RectTransform));
        fallbackGo.transform.SetParent(row.transform, false);
        fallback = fallbackGo.AddComponent<TextMeshProUGUI>();
        fallback.fontSize = 24f;
        fallback.alignment = TextAlignmentOptions.MidlineRight;
        fallback.raycastTarget = false;
        if (font != null)
            fallback.font = font;
        RectTransform fallbackRect = fallback.rectTransform;
        fallbackRect.anchorMin = new Vector2(1f, 0f);
        fallbackRect.anchorMax = new Vector2(1f, 1f);
        fallbackRect.pivot = new Vector2(1f, 0.5f);
        fallbackRect.anchoredPosition = new Vector2(-KeyColumnWidth - KeyGap, 0f);
        fallbackRect.sizeDelta = new Vector2(150f, 0f);

        GameObject keyGo = new GameObject("Tecla", typeof(RectTransform));
        keyGo.transform.SetParent(row.transform, false);
        key = keyGo.AddComponent<TextMeshProUGUI>();
        key.fontSize = 20f;
        key.alignment = TextAlignmentOptions.Center;
        key.raycastTarget = false;
        if (font != null)
            key.font = font;
        RectTransform keyRect = key.rectTransform;
        keyRect.anchorMin = new Vector2(1f, 0f);
        keyRect.anchorMax = new Vector2(1f, 1f);
        keyRect.pivot = new Vector2(1f, 0.5f);
        keyRect.anchoredPosition = Vector2.zero;
        keyRect.sizeDelta = new Vector2(KeyColumnWidth, 0f);

        return rowRect;
    }

    private void Update()
    {
        if (icon2 == null || switcher == null || loadout == null)
            return;

        int current = switcher.CurrentWeaponIndex;
        ApplyRow(0, icon2, key2, fallback2, row2, current == 0);
        ApplyRow(1, icon3, key3, fallback3, row3, current == 1);
    }

    private void ApplyRow(int index, Image icon, TMP_Text key, TMP_Text fallback, RectTransform row, bool active)
    {
        // Las armas cuerpo a cuerpo siempre muestran el texto "Melee", aunque tengan ícono.
        bool melee = IsMeleeAt(index);
        Sprite sprite = melee ? null : IconAt(index);
        bool hasIcon = sprite != null;
        Color tint = active ? Red : BoneDim;

        icon.enabled = hasIcon;
        icon.sprite = sprite;
        icon.color = tint;

        fallback.gameObject.SetActive(!hasIcon);
        fallback.text = hasIcon ? string.Empty : SlotName(index);
        fallback.color = tint;

        key.color = tint;
        key.fontSize = active ? 26f : 20f;

        // El dibujo no se escala: queda del mismo tamaño que el del panel de municion.
        if (row != null)
            row.localScale = Vector3.one;
    }

    private Sprite IconAt(int index)
    {
        GameObject weapon = WeaponAt(index);
        if (weapon == null)
            return null;

        EquippableWeapon equippable = weapon.GetComponent<EquippableWeapon>();
        return equippable != null ? equippable.HudIcon : null;
    }

    private string SlotName(int index)
    {
        GameObject weapon = WeaponAt(index);
        if (weapon == null)
            return "—";

        if (IsMeleeAt(index))
            return "Melee";

        return PrettyName(weapon.name);
    }

    private bool IsMeleeAt(int index)
    {
        GameObject weapon = WeaponAt(index);
        return weapon != null && weapon.GetComponent<MeleeLogic>() != null;
    }

    private GameObject WeaponAt(int index)
    {
        if (loadout.Weapons == null || index < 0 || index >= loadout.Weapons.Length)
            return null;

        return loadout.Weapons[index];
    }

    private static string PrettyName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "—";

        switch (raw)
        {
            case "rifleAKH": return "AK";
            case "PistolKrosH": return "Pistola";
            case "ShotgunH": return "Escopeta";
            case "RequiemH": return "Requiem";
            case "P90H": return "P90";
            case "DesertH": return "Desert";
            case "CrowbarH":
            case "CrowbarS": return "Palanca";
            case "AxeH": return "Hacha";
        }

        return raw;
    }
}
