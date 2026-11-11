using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// Generador de energía. Cada bidón lleno que se vierte suma carga; al completarla se abren las puertas.
// Al verter, todos los clientes ven la animación del bidón sobre el generador.
[RequireComponent(typeof(NetworkObject))]
public class FuelGenerator : NetworkBehaviour
{
    private static readonly List<FuelGenerator> all = new List<FuelGenerator>();

    [Header("Ítems")]
    [SerializeField] private ItemDefinition emptyCan;
    [SerializeField] private ItemDefinition fullCan;

    [Header("Al encenderse")]
    [Tooltip("Puertas que abre este generador por sí solo. Si hay varios generadores, usá un FuelGeneratorGroup y dejalo vacío.")]
    [SerializeField] private Door[] doors;
    [Tooltip("Opcional: grupo de generadores, para mostrar cuántos hay encendidos.")]
    [SerializeField] private FuelGeneratorGroup group;
    [Tooltip("Se prenden al encender el generador (luces, chispas, cartel).")]
    [SerializeField] private GameObject[] enableWhenPowered;
    [Tooltip("Se apagan al encender el generador (luces de emergencia, cartel de apagado).")]
    [SerializeField] private GameObject[] disableWhenPowered;

    [Header("Generador")]
    [Tooltip("Bidones llenos que hay que verter para encenderlo.")]
    [SerializeField] private int cansRequired = 3;
    [SerializeField] private float interactDistance = 3f;

    [Header("Sonido")]
    [Tooltip("Sonido de arranque, suena una vez al encenderse.")]
    [SerializeField] private AudioClip poweredClip;
    [Tooltip("Sonido del motor, en loop mientras el generador está encendido.")]
    [SerializeField] private AudioClip engineClip;
    [Tooltip("AudioSource del motor. Si lo dejás vacío, se crea solo.")]
    [SerializeField] private AudioSource engineLoop;
    [Tooltip("Qué parte del arranque debe pasar antes de que entre el motor (0 = a la vez, 1 = al terminar).")]
    [Range(0f, 1f)][SerializeField] private float engineStartPoint = 0.6f;
    [SerializeField] private float engineFadeIn = 1.5f;
    [SerializeField] private float engineMaxDistance = 25f;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    public UnityEvent onPowered;

    private Coroutine engineRoutine;

    [Header("Animación de verter")]
    [Tooltip("Modelo del bidón apoyado sobre el generador. Arranca oculto y se muestra solo mientras vierte.")]
    [SerializeField] private GameObject pourCan;
    [SerializeField] private Animator pourAnimator;
    [SerializeField] private string pourTrigger = "Pour";
    [Tooltip("Debe durar igual que el clip de la animación.")]
    [SerializeField] private float pourDuration = 3f;
    [Tooltip("Chorro de nafta. Opcional.")]
    [SerializeField] private ParticleSystem fuelStream;
    [Range(0f, 1f)] [SerializeField] private float streamStart = 0.3f;
    [Range(0f, 1f)] [SerializeField] private float streamEnd = 0.8f;
    [SerializeField] private AudioClip pourClip;


    [Header("Marcador")]
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private float markerDistance = 60f;
    [SerializeField] private Color markerColor = new Color(1f, 0.8f, 0.1f);
    [SerializeField] private Color poweredColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private string markerText = "OBJETIVO: Cargar el generador";
    [SerializeField] private string promptText = "[E] Verter nafta";
    [SerializeField] private string needFuelText = "Seleccioná el bidón con nafta en tu inventario";
    [SerializeField] private string poweredText = "Generador encendido";
    [SerializeField] private ObjectiveHighlight highlight;

    private readonly NetworkVariable<int> delivered = new NetworkVariable<int>(0);
    private readonly NetworkVariable<bool> powered = new NetworkVariable<bool>(false);

    [Header("Barra de progreso")]
    [SerializeField] private Color barColor = new Color(1f, 0.8f, 0.1f);

    private bool serverBusy;
    private Coroutine pourRoutine;
    private bool pourPending;      // local: se está viendo verter un bidón que todavía no suma
    private float displayedProgress;

    public bool IsPowered => powered.Value;

    // True si el inventario tiene un bidón (vacío o lleno).
    public bool HasCan(PlayerInventory inventory)
    {
        if (inventory == null) return false;

        return (emptyCan != null && inventory.Has(emptyCan.id)) || (fullCan != null && inventory.Has(fullCan.id));
    }

    // Se dispara en cada cliente al quedar encendido. 'instant' es true si ya estaba encendido al conectarse.
    public event Action<FuelGenerator, bool> PoweredApplied;

    private void Awake()
    {
        if (pourCan != null)
            pourCan.SetActive(false);

        SetupEngineSource();
    }
    private void SetupEngineSource()
    {
        if (engineLoop == null)
            engineLoop = gameObject.AddComponent<AudioSource>();

        if (engineClip != null)
            engineLoop.clip = engineClip;

        engineLoop.loop = true;
        engineLoop.playOnAwake = false;   // que no suene antes de encenderse
        engineLoop.spatialBlend = 1f;     // 3D: se oye más fuerte cerca del generador
        engineLoop.rolloffMode = AudioRolloffMode.Linear;
        engineLoop.minDistance = 2f;
        engineLoop.maxDistance = engineMaxDistance;
        engineLoop.volume = 0f;
        engineLoop.Stop();
    }

    public override void OnNetworkSpawn()
    {
        all.Add(this);
        powered.OnValueChanged += OnPoweredChanged;
        delivered.OnValueChanged += OnDeliveredChanged;

        if (powered.Value)
            ApplyPowered(true);

        RefreshHighlight();
    }

    public override void OnNetworkDespawn()
    {
        all.Remove(this);
        powered.OnValueChanged -= OnPoweredChanged;
        delivered.OnValueChanged -= OnDeliveredChanged;

        if (engineRoutine != null) StopCoroutine(engineRoutine);
        if (engineLoop != null) engineLoop.Stop();
    }

    public static FuelGenerator FindNearest(Vector3 from)
    {
        FuelGenerator best = null;
        float bestDistance = float.MaxValue;

        foreach (FuelGenerator generator in all)
        {
            if (generator == null || !generator.IsSpawned || generator.powered.Value) continue;

            float distance = Vector3.Distance(from, generator.transform.position);
            if (distance <= generator.interactDistance && distance < bestDistance)
            {
                best = generator;
                bestDistance = distance;
            }
        }

        return best;
    }

    public void Interact()
    {
        if (!IsSpawned || powered.Value) return;

        int slot = FuelInteraction.HeldSlotOf(fullCan);
        if (slot < 0)
        {
            FuelInteraction.ShowMessage(needFuelText);
            return;
        }

        RequestPourServerRpc(slot);
    }

    // ---------------------------------------------------------------- server

    [ServerRpc(RequireOwnership = false)]
    private void RequestPourServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (powered.Value || serverBusy) return;

        if (emptyCan == null || fullCan == null)
        {
            Debug.LogError("FuelGenerator: faltan los ítems del bidón en el Inspector.", this);
            return;
        }

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!FuelInteraction.TryGetServerPlayer(sender, transform.position, interactDistance, out PlayerInventory inventory))
            return;

        if (!inventory.ServerReplaceItemInSlot(slotIndex, fullCan.id, emptyCan.id))   // el bidón queda vacío
        {
            ShowMessageClientRpc(needFuelText, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } }
            });
            return;
        }

        serverBusy = true;
        PourClientRpc(sender);
        StartCoroutine(ServerFinishPour());
    }

    private IEnumerator ServerFinishPour()
    {
        yield return new WaitForSeconds(pourDuration);

        delivered.Value++;
        serverBusy = false;

        if (delivered.Value >= cansRequired)
            powered.Value = true;
    }

    // ---------------------------------------------------------------- clientes

    [ClientRpc]
    private void PourClientRpc(ulong pouringClientId)
    {
        // el bidón que se ve verter es el del generador: el de la mano de quien lo usa se oculta
        NetworkObject pouringPlayer = NetworkManager.SpawnManager.GetPlayerNetworkObject(pouringClientId);
        if (pouringPlayer != null && pouringPlayer.TryGetComponent(out HeldItemDisplay held))
            held.HideFor(pourDuration);

        if (pourRoutine != null)
            StopCoroutine(pourRoutine);

        pourRoutine = StartCoroutine(PourRoutine());
    }

    [ClientRpc]
    private void ShowMessageClientRpc(string text, ClientRpcParams rpcParams = default)
    {
        FuelInteraction.ShowMessage(text);
    }

    private IEnumerator PourRoutine()
    {
        pourPending = true;

        if (pourCan != null)
            pourCan.SetActive(true);

        if (pourAnimator != null)
            pourAnimator.SetTrigger(pourTrigger);

        if (pourClip != null)
            AudioSource.PlayClipAtPoint(pourClip, transform.position, volume);

        yield return new WaitForSeconds(pourDuration * streamStart);

        if (fuelStream != null)
            fuelStream.Play();

        yield return new WaitForSeconds(pourDuration * Mathf.Max(0f, streamEnd - streamStart));

        if (fuelStream != null)
            fuelStream.Stop();

        yield return new WaitForSeconds(pourDuration * Mathf.Max(0f, 1f - streamEnd));

        if (pourCan != null)
            pourCan.SetActive(false);

        pourPending = false;
        pourRoutine = null;
    }

    private void Update()
    {
        if (!IsSpawned) return;

        float target = powered.Value ? 1f : Mathf.Clamp01((delivered.Value + (pourPending ? 1 : 0)) / (float)Mathf.Max(1, cansRequired));
        float speed = 1f / Mathf.Max(0.1f, pourDuration * Mathf.Max(1, cansRequired));
        displayedProgress = Mathf.MoveTowards(displayedProgress, target, speed * Time.deltaTime);
    }

    private void OnDeliveredChanged(int previous, int current) => RefreshHighlight();

    private void OnPoweredChanged(bool previous, bool current)
    {
        if (current)
            ApplyPowered(false);

        RefreshHighlight();
    }

    private void ApplyPowered(bool instant)
    {
        foreach (Door door in doors)
            if (door != null) door.Open(instant);

        foreach (GameObject target in enableWhenPowered)
            if (target != null) target.SetActive(true);

        foreach (GameObject target in disableWhenPowered)
            if (target != null) target.SetActive(false);

        if (!instant && poweredClip != null)
            AudioSource.PlayClipAtPoint(poweredClip, transform.position, volume);

        if (engineRoutine != null)
            StopCoroutine(engineRoutine);
        engineRoutine = StartCoroutine(EngineRoutine(instant));
    }
    private IEnumerator EngineRoutine(bool instant)
    {
        if (engineLoop == null || engineLoop.clip == null) yield break;

        // Si el cliente se conecta con el generador ya encendido, el motor entra directo, sin arranque.
        if (!instant && poweredClip != null)
            yield return new WaitForSeconds(poweredClip.length * engineStartPoint);

        engineLoop.volume = 0f;
        engineLoop.Play();

        float fade = instant ? 0f : Mathf.Max(0.01f, engineFadeIn);
        float t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            engineLoop.volume = Mathf.Lerp(0f, volume, t / fade);
            yield return null;
        }

        engineLoop.volume = volume;
        engineRoutine = null;
    }

    private void RefreshHighlight()
    {
        if (highlight != null)
            highlight.SetHighlighted(!powered.Value);
    }

    private void OnGUI()
    {
        if (!IsSpawned) return;

        FuelInteraction.DrawMessage();

        if (!FuelInteraction.LocalPlayerWithin(transform.position, markerDistance)) return;

        Vector3 world = transform.position + markerOffset;

        if (powered.Value)
        {
            string done = group != null && group.Total > 1 ? $"{poweredText} ({group.PoweredCount}/{group.Total})" : poweredText;
            FuelInteraction.DrawMarker(world, done, poweredColor);
            FuelInteraction.DrawBar(world, displayedProgress, poweredColor, $"{cansRequired} / {cansRequired}");
            return;
        }

        Camera cam = Camera.main;
        float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
        string text = $"◆\n{markerText}\n{distance:0} m";

        if (FuelInteraction.LocalPlayerWithin(transform.position, interactDistance))
        {
            text += "\n" + (FuelInteraction.HeldSlotOf(fullCan) >= 0 ? promptText : needFuelText);
        }

        FuelInteraction.DrawMarker(world, text, markerColor);
        FuelInteraction.DrawBar(world, displayedProgress, barColor, $"{delivered.Value} / {cansRequired}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}
