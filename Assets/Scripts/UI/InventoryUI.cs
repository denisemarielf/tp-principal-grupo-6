using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Dibuja los slots del inventario del jugador local. Genérico: sirve para cualquier ítem.
public class InventoryUI : MonoBehaviour
{
    // El marco del slot (placeholder) se pasa a gris para sacarle el verde.
    private static readonly Color SlotTint = new Color(0.85f, 0.86f, 0.88f, 1f);
    // El marco de selección es naranja; teñido con rojo queda en el color del HUD.
    private static readonly Color SelectedTint = new Color(1f, 0.25f, 0.2f, 1f);
    private static readonly Dictionary<Sprite, Sprite> grayCache = new Dictionary<Sprite, Sprite>();

    [System.Serializable]
    public class SlotView
    {
        public Image icon;           // se muestra solo si el slot tiene un ítem
        public TMP_Text quantityText; // se muestra solo si la cantidad es mayor a 1
        public GameObject selectionFrame;
    }

    [Tooltip("Si queda vacío, lo busca en el prefab del Player (padre).")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private SlotView[] slotViews;

    private void OnEnable()
    {
        ApplyQuietStyle();

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
        if (inventory == null) return;

        inventory.InventoryChanged += Refresh;
        inventory.SelectedSlotChanged += OnSelectedChanged;
        Refresh();
    }

    private void OnDisable()
    {
        inventory.InventoryChanged -= Refresh;
        inventory.SelectedSlotChanged -= OnSelectedChanged;
        if (inventory != null)
            inventory.InventoryChanged -= Refresh;
    }

    // Marcos de los slots en gris (sin verde) y selección en rojo.
    private void ApplyQuietStyle()
    {
        if (slotViews == null) return;

        foreach (SlotView view in slotViews)
        {
            if (view.selectionFrame == null) continue;

            Image frame = view.selectionFrame.GetComponent<Image>();
            if (frame != null)
                frame.color = SelectedTint;

            Transform slotRoot = view.selectionFrame.transform.parent;
            Image slot = slotRoot != null ? slotRoot.GetComponent<Image>() : null;
            if (slot != null && slot.sprite != null)
            {
                slot.sprite = Grayscale(slot.sprite);
                slot.color = SlotTint;
            }
        }
    }

    // Copia en gris del sprite (se cachea para no repetirla por cada slot).
    private static Sprite Grayscale(Sprite source)
    {
        if (grayCache.TryGetValue(source, out Sprite cached))
            return cached;

        Texture2D texture = source.texture;
        Rect rect = source.textureRect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, target);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;

        Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(rect, 0, 0);
        copy.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Color[] pixels = copy.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float l = pixels[i].r * 0.3f + pixels[i].g * 0.59f + pixels[i].b * 0.11f;
            pixels[i] = new Color(l, l, l, pixels[i].a);
        }
        copy.SetPixels(pixels);
        copy.Apply();
        copy.wrapMode = TextureWrapMode.Clamp;
        copy.filterMode = texture.filterMode;

        Sprite result = Sprite.Create(copy, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        grayCache[source] = result;
        return result;
    }

    private void OnSelectedChanged(int _) => UpdateSelection();

    private void UpdateSelection()
    {
        if (inventory == null) return;
        for (int i = 0; i < slotViews.Length; i++)
            if (slotViews[i].selectionFrame != null)
                slotViews[i].selectionFrame.SetActive(i == inventory.SelectedSlot);
    }
    private void Refresh()
    {
        if (inventory == null) return;

        for (int i = 0; i < slotViews.Length; i++)
        {
            SlotView view = slotViews[i];
            ItemDefinition def = null;
            int quantity = 0;

            if (inventory.IsSpawned && i < inventory.LoadedSlotCount)
            {
                InventorySlot slot = inventory.GetSlot(i);
                if (!slot.IsEmpty)
                {
                    def = inventory.GetDefinition(slot.itemId);
                    quantity = slot.quantity;
                }
            }

            if (view.icon != null)
            {
                view.icon.sprite = def != null ? def.icon : null;
                view.icon.enabled = def != null && def.icon != null;
            }

            if (view.quantityText != null)
            {
                view.quantityText.gameObject.SetActive(quantity > 1);
                view.quantityText.text = quantity.ToString();
            }
        }
        UpdateSelection();
    }
    
}