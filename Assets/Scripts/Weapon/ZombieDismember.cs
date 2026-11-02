using UnityEngine;

// Desmembramiento por hueso escalado a cero. Va en el zombie (raiz o junto al Animator).
public class ZombieDismember : MonoBehaviour
{
    [Header("Deteccion (servidor)")]
    [SerializeField] private float headRadius = 0.3f;     // distancia al hueso de la cabeza para contar como headshot
    [SerializeField] private float closeRange = 6f;       // "de cerca" para la escopeta
    [SerializeField, Range(0f, 1f)] private float closeRangeChance = 0.6f;

    [Header("Pieza que sale volando")]
    [SerializeField] private GameObject gibPrefab;        // opcional: si esta vacio usa una esfera/capsula
    [SerializeField] private float gibSize = 0.22f;

    private Animator animator;
    private bool headGone, leftArmGone, rightArmGone;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    // SERVIDOR: decide si este disparo desmiembra y que hueso.
    public bool TryChooseBone(Vector3 hitPoint, float distance, bool allowCloseRange, out HumanBodyBones bone)
    {
        bone = HumanBodyBones.Head;

        Transform head = GetBone(HumanBodyBones.Head);
        if (head != null && !headGone && Vector3.Distance(hitPoint, head.position) <= headRadius)
            return true;

        float chance = Mathf.Lerp(1f, closeRangeChance, Mathf.Clamp01(distance / closeRange));
        if (allowCloseRange && distance <= closeRange && Random.value < chance)
        {
            bool left = Random.value < 0.5f;
            bone = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            return left ? !leftArmGone : !rightArmGone;
        }
        return false;
    }

    // CLIENTES: efecto visual.
    public void Dismember(HumanBodyBones id, Vector3 force)
    {
        Transform bone = GetBone(id);
        if (bone == null || bone.parent == null || bone.localScale == Vector3.zero) return;

        if (id == HumanBodyBones.Head) headGone = true;
        else if (id == HumanBodyBones.LeftUpperArm) leftArmGone = true;
        else rightArmGone = true;

        Vector3 outward = bone.position - bone.parent.position;
        if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
        outward.Normalize();

        // Sangre: chorro inicial + herida que sigue sangrando en el hueso padre
        // (en el propio hueso no, porque queda con escala 0 y no se veria).
        BloodSplatter.Play(bone.position, outward, bone.parent);

        SpawnGib(bone, id, force);
        SpawnStump(bone);

        bone.localScale = Vector3.zero;
    }

    private void SpawnGib(Transform bone, HumanBodyBones id, Vector3 force)
    {
        GameObject gib;
        if (gibPrefab != null)
        {
            gib = Instantiate(gibPrefab, bone.position, bone.rotation);
        }
        else
        {
            bool isHead = id == HumanBodyBones.Head;
            gib = GameObject.CreatePrimitive(isHead ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            gib.transform.SetPositionAndRotation(bone.position, bone.rotation);
            gib.transform.localScale = isHead
                ? Vector3.one * gibSize
                : new Vector3(0.12f, 0.25f, 0.12f);
            gib.GetComponent<Renderer>().material.color = new Color(0.45f, 0.08f, 0.06f);
        }

        if (!gib.TryGetComponent(out Rigidbody rb))
            rb = gib.AddComponent<Rigidbody>();
        rb.mass = 2f;
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(Random.onUnitSphere * 3f, ForceMode.Impulse);

        // Gotea mientras vuela y deja manchas en el piso.
        BloodWound.Begin(gib.transform, gib.transform.position, Vector3.up, 4f);
        Destroy(gib, 8f);
    }

    // Esferita roja que tapa el agujero que deja el hueso colapsado.
    private void SpawnStump(Transform bone)
    {
        Transform parent = bone.parent;
        GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(stump.GetComponent<Collider>());
        stump.transform.SetParent(parent, false);
        stump.transform.position = bone.position;
        stump.transform.localScale = Vector3.one * (0.13f / Mathf.Max(0.0001f, parent.lossyScale.x));
        stump.GetComponent<Renderer>().material.color = new Color(0.35f, 0.02f, 0.02f);
    }

    private Transform GetBone(HumanBodyBones id)
    {
        if (animator != null && animator.isHuman)
        {
            Transform t = animator.GetBoneTransform(id);
            if (t != null) return t;
        }

        // Fallback para rigs Generic de Mixamo ("mixamorig:Head", etc.)
        string key = id == HumanBodyBones.Head ? "Head"
                   : id == HumanBodyBones.LeftUpperArm ? "LeftArm" : "RightArm";
        foreach (Transform c in GetComponentsInChildren<Transform>())
        {
            if (c.name == key || c.name.EndsWith(":" + key))
                return c;
        }
        return null;
    }
}