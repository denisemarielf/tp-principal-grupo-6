using Unity.Netcode;
using UnityEngine;

// Muestra en la mano el ítem del slot seleccionado del inventario (por ahora, los bidones).
// Al seleccionar un ítem con modelo se enfunda el arma; con el arma equipada el ítem no se ve.
public class HeldItemDisplay : NetworkBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private WeaponSwitcher switcher;
    [SerializeField] private PlayerModelBinder binder;
    [SerializeField] private CameraControllerFPS view;

    [Header("En la mano (hueso RightHand, en tercera persona y para los demás)")]
    [SerializeField] private Vector3 handPosition = new Vector3(0f, 0.29f, 0f);
    [SerializeField] private Vector3 handEuler = new Vector3(0f, 90f, 180f);
    [SerializeField] private float handScale = 0.65f;

    [Header("Primera persona (se cuelga de la cámara)")]
    [SerializeField] private Vector3 firstPersonPosition = new Vector3(0.32f, -0.42f, 0.65f);
    [SerializeField] private Vector3 firstPersonEuler = new Vector3(0f, -20f, 0f);
    [SerializeField] private float firstPersonScale = 1.3f;

    private readonly NetworkVariable<int> heldItemId = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private GameObject held;
    private int visualItemId = -1;
    private Transform attachedTo;
    private bool attachedFirstPerson;
    private float hiddenUntil;

    public int HeldItemId => heldItemId.Value;

    // Oculta el ítem de la mano un rato (por ejemplo mientras se ve la animación de verter).
    public void HideFor(float seconds)
    {
        hiddenUntil = Time.time + seconds;
        Clear();
    }

    public override void OnNetworkSpawn()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (switcher == null) switcher = GetComponentInChildren<WeaponSwitcher>(true);
        if (binder == null) binder = GetComponent<PlayerModelBinder>();
        if (view == null) view = GetComponentInChildren<CameraControllerFPS>(true);

        if (IsOwner && inventory != null)
            inventory.SelectedSlotChanged += OnSelectedSlotChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && inventory != null)
            inventory.SelectedSlotChanged -= OnSelectedSlotChanged;

        Clear();
    }

    private void Update()
    {
        if (!IsSpawned) return;

        // Muerto: el bidón no debe colgarse de la cámara de espectador.
        if (IsOwner && OwnerIsDead())
        {
            Clear();
            return;
        }

        if (IsOwner)
            EvaluateOwner();

        RefreshVisual();
    }

    private bool OwnerIsDead()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        return health != null && health.IsDead;
    }

    // Al elegir un slot con un ítem que se sostiene, el arma se guarda.
    private void OnSelectedSlotChanged(int slot)
    {
        if (switcher == null || ItemInSlot(slot) == null || switcher.CurrentWeaponIndex < 0) return;

        switcher.SelectWeapon(-1);
    }

    private ItemDefinition ItemInSlot(int slot)
    {
        if (inventory == null || slot < 0 || slot >= inventory.LoadedSlotCount) return null;

        InventorySlot content = inventory.GetSlot(slot);
        if (content.IsEmpty) return null;

        ItemDefinition definition = inventory.GetDefinition(content.itemId);
        return definition != null && definition.heldModel != null ? definition : null;
    }

    private void EvaluateOwner()
    {
        bool weaponInHand = switcher != null && switcher.CurrentWeaponIndex >= 0;
        ItemDefinition item = weaponInHand ? null : ItemInSlot(inventory.SelectedSlot);
        int desired = item != null ? item.id : -1;

        if (heldItemId.Value != desired)
            heldItemId.Value = desired;
    }

    private void RefreshVisual()
    {
        int id = heldItemId.Value;
        if (id < 0 || Time.time < hiddenUntil)
        {
            Clear();
            return;
        }

        bool firstPerson = IsOwner && view != null && !view.IsThirdPerson;
        Transform anchor = firstPerson ? FirstPersonAnchor() : RightHand();
        if (anchor == null) return;

        if (held == null || visualItemId != id)
        {
            Clear();

            ItemDefinition definition = inventory.GetDefinition(id);
            if (definition == null || definition.heldModel == null) return;

            held = Instantiate(definition.heldModel);
            visualItemId = id;
            attachedTo = null;
        }

        if (attachedTo == anchor && attachedFirstPerson == firstPerson) return;

        held.transform.SetParent(anchor, false);
        held.transform.localPosition = firstPerson ? firstPersonPosition : handPosition;
        held.transform.localRotation = Quaternion.Euler(firstPerson ? firstPersonEuler : handEuler);
        held.transform.localScale = Vector3.one * (firstPerson ? firstPersonScale : handScale);

        attachedTo = anchor;
        attachedFirstPerson = firstPerson;
    }

    private Transform FirstPersonAnchor()
    {
        Camera cam = GetComponentInChildren<Camera>(true);
        return cam != null ? cam.transform : null;
    }

    private Transform RightHand()
    {
        GameObject model = binder != null ? binder.ActiveModel : null;
        if (model == null) return null;

        Animator animator = model.GetComponentInChildren<Animator>(true);
        return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
    }

    private void Clear()
    {
        if (held != null)
            Destroy(held);

        held = null;
        visualItemId = -1;
        attachedTo = null;
    }
}
