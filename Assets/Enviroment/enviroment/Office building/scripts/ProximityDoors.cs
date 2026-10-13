using System.Collections.Generic;
using UnityEngine;

// Abre y cierra solas las puertas del edificio cuando un jugador se acerca.
// El asset original las abre con la tecla E (DoorAction), pero ese script solo vive en el jugador del asset y
// el jugador real del juego no lo tiene. Esto reutiliza Door.ActionDoor sin tocar el prefab del jugador.
// Cada cliente decide con la posicion de todos los jugadores (ya sincronizada), asi que las puertas
// se ven igual en todas las pantallas. Los zombis no las abren.
public class ProximityDoors : MonoBehaviour
{
    private class Leaf
    {
        public Door door;      // al que se le llama ActionDoor (mueve toda su lista UseDoors)
        public Door target;    // la hoja cuyo estado se mira (puede ser la misma)
        public Door.DoorGet state;
        public Bounds bounds;
    }

    [SerializeField] private float openDistance = 3.5f;
    [Tooltip("Mayor que la de apertura, para que la puerta no titile cuando el jugador queda justo en el borde.")]
    [SerializeField] private float closeDistance = 5.5f;
    [SerializeField] private float checkInterval = 0.2f;
    [Tooltip("Solo se automatizan las puertas cuyo nombre empieza asi. Quedan afuera ventanas y cabinas de bano.")]
    [SerializeField] private string[] namePrefixes = { "Door", "Glass", "Entry door", "Back entry", "Enery room door" };

    private readonly List<Leaf> leaves = new List<Leaf>();
    private float nextCheck;

    private void Start()
    {
        foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Exclude))
        {
            if (!door.CompareTag("door") || !HasAllowedName(door.name)) continue;

            // Cada hoja guarda su estado abierto/cerrado en su propia entrada de UseDoors. Algunos objetos son solo
            // controladores (listan a otras hojas pero no a si mismos): en ese caso se mira el estado de la primera hoja que controlan.
            Door target = door;
            Door.DoorGet state = door.UseDoors.Find(entry => entry.Door == door.gameObject);
            if (state == null)
            {
                foreach (Door.DoorGet entry in door.UseDoors)
                {
                    target = entry.Door != null ? entry.Door.GetComponent<Door>() : null;
                    if (target == null) continue;

                    state = target.UseDoors.Find(e => e.Door == target.gameObject);
                    if (state != null) break;
                }
            }
            if (state == null) continue;

            Bounds bounds = default;
            bool hasBounds = false;
            AddBounds(door.gameObject, ref bounds, ref hasBounds);
            foreach (Door.DoorGet entry in door.UseDoors)
                if (entry.Door != null) AddBounds(entry.Door, ref bounds, ref hasBounds);
            if (!hasBounds) continue;

            leaves.Add(new Leaf { door = door, target = target, state = state, bounds = bounds });
        }
    }

    private static void AddBounds(GameObject source, ref Bounds bounds, ref bool hasBounds)
    {
        foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
    }

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        Character[] players = FindObjectsByType<Character>(FindObjectsInactive.Exclude);
        if (players.Length == 0) return;

        float openSqr = openDistance * openDistance;
        float closeSqr = closeDistance * closeDistance;

        foreach (Leaf leaf in leaves)
        {
            if (leaf.door == null || leaf.target == null || leaf.door.door_in_use || leaf.target.door_in_use) continue;

            float nearest = float.MaxValue;
            foreach (Character player in players)
                nearest = Mathf.Min(nearest, leaf.bounds.SqrDistance(player.transform.position));

            if (!leaf.state.isDoorOpen && nearest < openSqr)
                leaf.door.ActionDoor();
            else if (leaf.state.isDoorOpen && nearest > closeSqr)
                leaf.door.ActionDoor();
        }
    }

    private bool HasAllowedName(string doorName)
    {
        foreach (string prefix in namePrefixes)
            if (doorName.StartsWith(prefix)) return true;
        return false;
    }
}
