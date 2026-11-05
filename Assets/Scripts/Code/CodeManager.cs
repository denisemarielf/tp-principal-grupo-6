using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CodeManager : NetworkBehaviour
{
    public static CodeManager Instance { get; private set; }

    [SerializeField] private string[] codeCollection;   // "4821", "1093", ...
    [SerializeField] private CodeSign[] signs;

    private readonly NetworkVariable<int> seed = new NetworkVariable<int>();

    public string CorrectCode { get; private set; }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer)
            seed.Value = Random.Range(int.MinValue, int.MaxValue);

        // La semilla ya llega con el spawn, así que todos calculan lo mismo
        Apply(seed.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    private void Apply(int s)
    {
        var rng = new System.Random(s);

        int correctIdx = rng.Next(codeCollection.Length);
        CorrectCode = codeCollection[correctIdx];

        // Señuelos mezclados (Fisher-Yates con el mismo rng)
        var decoys = new List<string>();
        for (int i = 0; i < codeCollection.Length; i++)
            if (i != correctIdx) decoys.Add(codeCollection[i]);

        for (int i = decoys.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (decoys[i], decoys[j]) = (decoys[j], decoys[i]);
        }

        int correctSign = rng.Next(signs.Length);
        int d = 0;

        for (int i = 0; i < signs.Length; i++)
        {
            if (i == correctSign)
                signs[i].Setup(CorrectCode, true);
            else
                signs[i].Setup(decoys[d++ % decoys.Count], false);
        }
    }
}