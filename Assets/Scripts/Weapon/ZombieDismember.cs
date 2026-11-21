using System.Collections.Generic;
using UnityEngine;

// Desmembramiento por hueso escalado a cero. Va en el zombie (raiz o junto al Animator).
// Pooling: el zombi se recicla, asi que al desactivarse (OnDisable) se restauran los huesos escalados,
// se esconden los munones y se limpian las banderas. Los gibs que salen volando tambien se reciclan.
public class ZombieDismember : MonoBehaviour
{
    [Header("Deteccion (servidor)")]
    [SerializeField] private float headRadius = 0.3f;     // distancia al hueso de la cabeza para contar como headshot
    [SerializeField] private float closeRange = 6f;       // "de cerca" para la escopeta
    [SerializeField, Range(0f, 1f)] private float closeRangeChance = 0.6f;

    [Header("Pieza que sale volando")]
    [SerializeField] private GameObject gibPrefab;        // opcional: si esta vacio usa una esfera/capsula
    [SerializeField] private float gibSize = 0.22f;
    [SerializeField] private float gibLifetime = 8f;

    private Animator animator;
    private bool headGone, leftArmGone, rightArmGone;

    // Se resuelven de forma perezosa: la variante visible puede cambiar entre usos del mismo objeto.
    private readonly Dictionary<HumanBodyBones, Transform> boneCache = new Dictionary<HumanBodyBones, Transform>();
    private readonly List<KeyValuePair<Transform, Vector3>> hiddenBones = new List<KeyValuePair<Transform, Vector3>>();
    private readonly Dictionary<HumanBodyBones, GameObject> stumps = new Dictionary<HumanBodyBones, GameObject>();

    // ---------- Pool de gibs (compartido por todos los zombis) ----------
    private static readonly Dictionary<object, Stack<GameObject>> gibPool = new Dictionary<object, Stack<GameObject>>();
    private static readonly List<Component> componentBuffer = new List<Component>();
    private static Transform gibRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        gibPool.Clear();
        gibRoot = null;
    }

    private void OnDisable()
    {
        RestoreBody();
    }

    // Deja al zombi entero para el proximo uso del objeto.
    private void RestoreBody()
    {
        foreach (KeyValuePair<Transform, Vector3> hidden in hiddenBones)
        {
            if (hidden.Key != null)
                hidden.Key.localScale = hidden.Value;
        }
        hiddenBones.Clear();

        foreach (GameObject stump in stumps.Values)
        {
            if (stump != null)
                stump.SetActive(false);
        }

        headGone = false;
        leftArmGone = false;
        rightArmGone = false;

        // La proxima vez se vuelve a buscar el Animator (puede ser otra variante de modelo).
        animator = null;
        boneCache.Clear();
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
        SpawnStump(bone, id);

        // Se guarda la escala original para devolverla al reciclar el zombi.
        hiddenBones.Add(new KeyValuePair<Transform, Vector3>(bone, bone.localScale));
        bone.localScale = Vector3.zero;
    }

    private void SpawnGib(Transform bone, HumanBodyBones id, Vector3 force)
    {
        GameObject gib = AcquireGib(id, bone.position, bone.rotation, out object key);

        if (!gib.TryGetComponent(out Rigidbody rb))
            rb = gib.AddComponent<Rigidbody>();
        rb.mass = 2f;
        ClearVelocity(rb);
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(Random.onUnitSphere * 3f, ForceMode.Impulse);

        // Si la pieza ya tuvo una herida en un uso anterior, se saca para no acumular otra encima.
        RemoveOldWound(gib);

        // Gotea mientras vuela y deja manchas en el piso.
        BloodWound.Begin(gib.transform, gib.transform.position, Vector3.up, 4f);

        if (!gib.TryGetComponent(out PooledGib pooled))
            pooled = gib.AddComponent<PooledGib>();
        pooled.Begin(key, gibLifetime);
    }

    private GameObject AcquireGib(HumanBodyBones id, Vector3 position, Quaternion rotation, out object key)
    {
        bool isHead = id == HumanBodyBones.Head;
        key = gibPrefab != null ? (object)gibPrefab : (isHead ? "head" : "limb");

        if (!gibPool.TryGetValue(key, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            gibPool[key] = stack;
        }

        GameObject gib = null;
        while (stack.Count > 0 && gib == null)
            gib = stack.Pop();   // si la escena se descargo, el objeto es null y se descarta

        if (gib == null)
        {
            if (gibPrefab != null)
            {
                gib = Instantiate(gibPrefab);
            }
            else
            {
                gib = GameObject.CreatePrimitive(isHead ? PrimitiveType.Sphere : PrimitiveType.Capsule);
                gib.transform.localScale = isHead
                    ? Vector3.one * gibSize
                    : new Vector3(0.12f, 0.25f, 0.12f);
                gib.GetComponent<Renderer>().sharedMaterial = BloodMaterials.Solid(new Color(0.45f, 0.08f, 0.06f));
            }

            gib.transform.SetParent(GetGibRoot(), false);
        }

        gib.transform.SetPositionAndRotation(position, rotation);
        gib.SetActive(true);
        return gib;
    }

    private static Transform GetGibRoot()
    {
        if (gibRoot == null)
        {
            var root = new GameObject("GibPool");
            DontDestroyOnLoad(root);
            gibRoot = root.transform;
        }
        return gibRoot;
    }

    internal static void ReleaseGib(object key, GameObject gib)
    {
        if (gib == null)
            return;

        gib.SetActive(false);

        if (key != null && gibPool.TryGetValue(key, out Stack<GameObject> stack))
            stack.Push(gib);
        else
            Destroy(gib);
    }

    private static void ClearVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    // BloodWound no se vio: por si Begin agrega un componente a la pieza, se busca por nombre (compila igual).
    private static void RemoveOldWound(GameObject gib)
    {
        gib.GetComponents(componentBuffer);
        foreach (Component c in componentBuffer)
        {
            if (c != null && c.GetType().Name == "BloodWound")
                Destroy(c);
        }
        componentBuffer.Clear();
    }

    // Esferita roja que tapa el agujero que deja el hueso colapsado. Se crea una vez por hueso y se reutiliza.
    private void SpawnStump(Transform bone, HumanBodyBones id)
    {
        Transform parent = bone.parent;

        if (!stumps.TryGetValue(id, out GameObject stump) || stump == null)
        {
            stump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(stump.GetComponent<Collider>());
            stump.GetComponent<Renderer>().sharedMaterial = BloodMaterials.Solid(new Color(0.35f, 0.02f, 0.02f));
            stumps[id] = stump;
        }

        stump.transform.SetParent(parent, false);
        stump.transform.position = bone.position;
        stump.transform.localScale = Vector3.one * (0.13f / Mathf.Max(0.0001f, parent.lossyScale.x));
        stump.SetActive(true);
    }

    private Transform GetBone(HumanBodyBones id)
    {
        // La variante visible puede haber cambiado desde la ultima vez (objeto reciclado).
        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            animator = GetComponentInChildren<Animator>();
            boneCache.Clear();
        }

        if (boneCache.TryGetValue(id, out Transform cached) && cached != null)
            return cached;

        Transform found = FindBone(id);
        if (found != null)
            boneCache[id] = found;
        return found;
    }

    private Transform FindBone(HumanBodyBones id)
    {
        if (animator != null && animator.isHuman)
        {
            Transform t = animator.GetBoneTransform(id);
            if (t != null) return t;
        }

        // Fallback para rigs Generic de Mixamo ("mixamorig:Head", etc.)
        string key = id == HumanBodyBones.Head ? "Head"
                   : id == HumanBodyBones.LeftUpperArm ? "LeftArm" : "RightArm";
        Transform root = animator != null ? animator.transform : transform;
        foreach (Transform c in root.GetComponentsInChildren<Transform>())
        {
            if (c.name == key || c.name.EndsWith(":" + key))
                return c;
        }
        return null;
    }
}

// Devuelve el gib al pool cuando se cumple su tiempo de vida (reemplaza a Destroy(gib, 8f)).
public class PooledGib : MonoBehaviour
{
    private object key;
    private float timer;

    public void Begin(object poolKey, float lifetime)
    {
        key = poolKey;
        timer = lifetime;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
            ZombieDismember.ReleaseGib(key, gameObject);
    }
}
