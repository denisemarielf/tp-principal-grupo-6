using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Dibuja los slots del inventario del jugador local. Genérico: sirve para cualquier ítem.
public class InventoryUI : MonoBehaviour
{
    [System.Serializable]
    public class SlotView
    {
        public Image icon;           // se muestra solo si el slot tiene un ítem
        public TMP_Text quantityText; // se muestra solo si la cantidad es mayor a 1
    }

    [Tooltip("Si queda vacío, lo busca en el prefab del Player (padre).")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private SlotView[] slotViews;

    private void OnEnable()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
        if (inventory == null) return;

        inventory.InventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryChanged -= Refresh;
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
    }
}