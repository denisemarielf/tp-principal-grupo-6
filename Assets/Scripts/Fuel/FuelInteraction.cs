using Unity.Netcode;
using UnityEngine;

// Utilidades compartidas por los autos y el generador: entrada con E, validación en el server y textos en pantalla.
public static class FuelInteraction
{
    private const float ServerDistanceTolerance = 1f;

    private static string message;
    private static float messageUntil;
    private static int lastMessageFrame = -1;
    private static GUIStyle style;
    private static GUIStyle barStyle;

    // Lo llama Character al apretar E. Devuelve true si había un auto o el generador al alcance.
    public static bool TryInteract(Vector3 from)
    {
        FuelSource source = FuelSource.FindNearest(from);
        FuelGenerator generator = FuelGenerator.FindNearest(from);

        if (source == null && generator == null)
            return false;

        float sourceDistance = source != null ? Vector3.Distance(from, source.transform.position) : float.MaxValue;
        float generatorDistance = generator != null ? Vector3.Distance(from, generator.transform.position) : float.MaxValue;

        if (generatorDistance <= sourceDistance)
            generator.Interact();
        else
            source.Interact();

        return true;
    }

    public static PlayerInventory LocalInventory()
    {
        NetworkObject player = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        return player != null ? player.GetComponent<PlayerInventory>() : null;
    }

    // Slot del inventario local con el ítem EN LA MANO (seleccionado y equipado), o -1.
    public static int HeldSlotOf(ItemDefinition item)
    {
        PlayerInventory inventory = LocalInventory();
        if (inventory == null || item == null) return -1;

        int slot = inventory.SelectedSlot;
        if (slot < 0 || slot >= inventory.LoadedSlotCount) return -1;

        InventorySlot content = inventory.GetSlot(slot);
        if (content.IsEmpty || content.itemId != item.id) return -1;

        HeldItemDisplay held = inventory.GetComponent<HeldItemDisplay>();
        if (held != null && held.HeldItemId != item.id) return -1;

        return slot;
    }

    public static bool LocalPlayerWithin(Vector3 position, float distance)
    {
        NetworkObject player = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClient?.PlayerObject : null;
        if (player == null) return false;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return false;

        return Vector3.Distance(player.transform.position, position) <= distance;
    }

    // Solo server. Devuelve false si el jugador no existe, está muerto o está lejos de 'target'.
    public static bool TryGetServerPlayer(ulong clientId, Vector3 target, float interactDistance, out PlayerInventory inventory)
    {
        inventory = null;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
            return false;

        PlayerHealth health = client.PlayerObject.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead)
            return false;

        if (Vector3.Distance(client.PlayerObject.transform.position, target) > interactDistance + ServerDistanceTolerance)
            return false;

        inventory = client.PlayerObject.GetComponent<PlayerInventory>();
        return inventory != null;
    }

    // ------------------------------------------------------------ texto en pantalla

    public static void ShowMessage(string text, float seconds = 3f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    // Se llama desde OnGUI de cada objeto; solo dibuja una vez por frame.
    public static void DrawMessage()
    {
        if (lastMessageFrame == Time.frameCount || string.IsNullOrEmpty(message) || Time.time > messageUntil)
            return;

        lastMessageFrame = Time.frameCount;
        DrawLabel(new Rect(Screen.width / 2f - 300f, Screen.height - 170f, 600f, 40f), message, new Color(1f, 0.85f, 0.3f));
    }

    public static void DrawMarker(Vector3 world, string text, Color color)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f) return;

        DrawLabel(new Rect(screen.x - 160f, Screen.height - screen.y - 40f, 320f, 100f), text, color);
    }

    // Barra de progreso sobre el marcador. fraction va de 0 a 1.
    public static void DrawBar(Vector3 world, float fraction, Color fill, string label)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f) return;

        const float width = 180f;
        const float height = 16f;
        float top = Screen.height - screen.y - 62f;
        Rect back = new Rect(screen.x - width / 2f, top, width, height);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(back.x - 2f, back.y - 2f, back.width + 4f, back.height + 4f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.15f);
        GUI.DrawTexture(back, Texture2D.whiteTexture);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(back.x, back.y, back.width * Mathf.Clamp01(fraction), back.height), Texture2D.whiteTexture);
        GUI.color = old;

        if (barStyle == null)
            barStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };

        barStyle.normal.textColor = Color.white;
        GUI.Label(back, label, barStyle);
    }

    private static void DrawLabel(Rect rect, string text, Color color)
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }
}
