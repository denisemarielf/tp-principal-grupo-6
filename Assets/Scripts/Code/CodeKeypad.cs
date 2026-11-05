using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CodeKeypad : NetworkBehaviour
{
    public static event Action<bool> LocalMinigameChanged;

    private static readonly List<CodeKeypad> all = new List<CodeKeypad>();

    [SerializeField] private KeypadUI ui;
    [SerializeField] private Door door;
    [SerializeField] private float interactRange = 2f;

    [Header("Resaltado")]
    [Tooltip("Componente Outline del objeto (Quick Outline u otro). Se prende cuando el jugador está cerca.")]
    [SerializeField] private Behaviour outline;

    [Header("Sonido")]
    [SerializeField] private AudioClip unlockedClip;   // beep de acceso concedido
    [SerializeField, Range(0f, 1f)] private float unlockedVolume = 1f;
    [Header("Resaltado")]
[Tooltip("Componente Outline (Quick Outline) del objeto. Se prende cuando el jugador está cerca.")]
//[SerializeField] private Outline outline;
[SerializeField] private Color outlineColor = new Color(1f, 0.5f, 0f); // naranja
[SerializeField] private float outlineWidth = 4f;

    private readonly NetworkVariable<bool> solved = new NetworkVariable<bool>(false);

   private void Awake()
{
    /*if (outline == null) return;

    outline.OutlineMode = Outline.Mode.OutlineAll;
    outline.OutlineColor = outlineColor;
    outline.OutlineWidth = outlineWidth;
    outline.enabled = false;*/
}

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public static CodeKeypad FindNearestUnsolved(Vector3 pos)
    {
        CodeKeypad best = null;
        float bestDist = float.MaxValue;

        foreach (var k in all)
        {
            if (k.solved.Value) continue;
            float d = Vector3.Distance(pos, k.transform.position);
            if (d <= k.interactRange && d < bestDist)
            {
                bestDist = d;
                best = k;
            }
        }
        return best;
    }

    public override void OnNetworkSpawn()
    {
        solved.OnValueChanged += OnSolvedChanged;

        // Jugador que entra tarde: la puerta ya aparece abierta
        if (solved.Value) door.Open(true);
    }

    public override void OnNetworkDespawn()
    {
        solved.OnValueChanged -= OnSolvedChanged;
    }

    private void OnSolvedChanged(bool previous, bool now)
    {
        if (!now) return;

        if (outline != null) outline.enabled = false;

        if (unlockedClip != null)
            AudioSource.PlayClipAtPoint(unlockedClip, transform.position, unlockedVolume);

        door.Open(false);
        if (ui.IsOpenFor(this)) ui.Close();
    }

    private void Update()
    {
        if (outline == null) return;

        bool show = false;

        var nm = NetworkManager.Singleton;
        if (!solved.Value && nm != null && nm.LocalClient != null
            && nm.LocalClient.PlayerObject != null && !ui.IsOpenFor(this))
        {
            float d = Vector3.Distance(nm.LocalClient.PlayerObject.transform.position, transform.position);
            show = d <= interactRange;
        }

        if (outline.enabled != show) outline.enabled = show;
    }

    // Lo llama Character.OnInteraction
    public void Interact()
    {
        if (solved.Value) return;
        ui.Open(this);
        LocalMinigameChanged?.Invoke(true);
    }

    public void CloseLocal() => LocalMinigameChanged?.Invoke(false);

    public void SubmitCode(string code) => SubmitCodeRpc(code);

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SubmitCodeRpc(string code, RpcParams rpcParams = default)
    {
        if (solved.Value) return;

        if (code == CodeManager.Instance.CorrectCode)
            solved.Value = true;
        else
            WrongCodeRpc(RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void WrongCodeRpc(RpcParams rpcParams = default)
    {
        ui.ShowWrong();
    }
}