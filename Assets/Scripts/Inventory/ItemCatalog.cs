using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Catalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private ItemDefinition[] items;
    private Dictionary<int, ItemDefinition> map;

    public ItemDefinition Get(int id)
    {
        if (map == null)
        {
            map = new Dictionary<int, ItemDefinition>();
            foreach (var i in items) if (i != null) map[i.id] = i;
        }
        return map.TryGetValue(id, out var def) ? def : null;
    }
}