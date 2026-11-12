using UnityEngine;
using UnityEngine.Events;

// Abre las puertas cuando todos los generadores del grupo están encendidos. Cada cliente lo resuelve solo.
public class FuelGeneratorGroup : MonoBehaviour
{
    [SerializeField] private FuelGenerator[] generators;
    [SerializeField] private Door[] doors;
    public UnityEvent onAllPowered;

    private bool opened;

    public int Total => generators != null ? generators.Length : 0;

    public int PoweredCount
    {
        get
        {
            int count = 0;
            if (generators == null) return 0;

            foreach (FuelGenerator generator in generators)
                if (generator != null && generator.IsPowered) count++;

            return count;
        }
    }

    // True si el jugador local lleva algún bidón (para el objetivo "encontrar los bidones").
    public bool LocalPlayerHasCan()
    {
        PlayerInventory inventory = FuelInteraction.LocalInventory();
        if (inventory == null || generators == null) return false;

        foreach (FuelGenerator generator in generators)
            if (generator != null && generator.HasCan(inventory)) return true;

        return false;
    }

    private void OnEnable()
    {
        if (generators == null) return;

        foreach (FuelGenerator generator in generators)
            if (generator != null) generator.PoweredApplied += OnGeneratorPowered;
    }

    private void OnDisable()
    {
        if (generators == null) return;

        foreach (FuelGenerator generator in generators)
            if (generator != null) generator.PoweredApplied -= OnGeneratorPowered;
    }

    private void OnGeneratorPowered(FuelGenerator generator, bool instant)
    {
        if (opened || PoweredCount < Total) return;

        opened = true;

        if (doors != null)
            foreach (Door door in doors)
                if (door != null) door.Open(instant);

        onAllPowered?.Invoke();
        Debug.Log($"Group: {PoweredCount}/{Total} opened={opened} instant={instant}");
    }
}
