using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Item")]
public class ItemDefinition : ScriptableObject
{
    public int id;                 // único y ESTABLE (viaja por red)
    public string displayName;
    public Sprite icon;
    public int maxStack = 1;       // por slot
    public int maxCarried = 0;     // límite total (0 = sin límite). Botiquín = 1
    public GameObject worldPrefab; // prefab con ItemPickup (para soltarlo)
    public ItemEffect useEffect;   // null = no usable
    public bool consumeOnUse = true;
}