using Unity.Netcode;
using UnityEngine;

// Muestra el modelo que corresponde a la variante. El host elige antes de spawnear
// y el valor viaja con la NetworkVariable para que los clientes vean el mismo cuerpo.
public class EnemyAppearance : NetworkBehaviour
{
    [SerializeField] private GameObject fragil;
    [SerializeField] private GameObject normal;
    [SerializeField] private GameObject resistente;

    private EnemyVariant chosen = EnemyVariant.Fragil;

    private readonly NetworkVariable<EnemyVariant> syncedVariant = new NetworkVariable<EnemyVariant>(
        EnemyVariant.Fragil,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public void Configure(EnemyVariant variant)
    {
        chosen = variant;
    }

    public override void OnNetworkSpawn()
    {
        syncedVariant.OnValueChanged += OnVariantChanged;

        if (IsServer)
            syncedVariant.Value = chosen;

        Show(IsServer ? chosen : syncedVariant.Value);
    }

    public override void OnNetworkDespawn()
    {
        syncedVariant.OnValueChanged -= OnVariantChanged;
    }

    private void OnVariantChanged(EnemyVariant previous, EnemyVariant next)
    {
        Show(next);
    }

    private void Show(EnemyVariant variant)
    {
        GameObject selected = fragil;
        if (variant == EnemyVariant.Normal)
            selected = normal;
        else if (variant == EnemyVariant.Resistente)
            selected = resistente;

        SetActive(fragil, fragil == selected);
        SetActive(normal, normal == selected);
        SetActive(resistente, resistente == selected);
    }

    private static void SetActive(GameObject model, bool active)
    {
        if (model != null && model.activeSelf != active)
            model.SetActive(active);
    }
}
