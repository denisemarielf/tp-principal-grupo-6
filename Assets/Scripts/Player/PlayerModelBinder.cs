using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
#endif

public class PlayerModelBinder : NetworkBehaviour
{
    [Serializable]
    public class Binding
    {
        public Component target;              // componente cuyo campo se rellena
        public string field;                  // nombre del campo (publico o privado)
        public UnityEngine.Object[] values;   // un valor por modelo
    }

    [Header("Modelos (el orden define el indice)")]
    [SerializeField] private GameObject[] models;

    [Header("Campos a rellenar (se generan con el menu del componente)")]
    [SerializeField] private List<Binding> bindings = new List<Binding>();

    [Header("Solo para refrescar si algo llega tarde")]
    [SerializeField] private WeaponSwitcher switcher;
    [SerializeField] private WeaponLoadout loadout;
    [SerializeField] private WeaponVisuals visuals;
    [SerializeField] private WeaponPresentation presentation;
    [SerializeField] private PlayerCameraSetup cameraSetup;

    private readonly NetworkVariable<int> modelIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private AnimatorCullingMode[][] originalCulling;

    public int CurrentIndex { get; private set; } = -1;
    public GameObject ActiveModel =>
        models != null && CurrentIndex >= 0 && CurrentIndex < models.Length ? models[CurrentIndex] : null;

    // ---------- Red ----------

    public override void OnNetworkSpawn()
    {
        if (IsServer) modelIndex.Value = PickFreeIndex();

        var all = NetworkObject.GetComponentsInChildren<NetworkBehaviour>(true);
        bool first = all.Length > 0 && all[0] == this;

        ApplyModel(modelIndex.Value);

        if (!first)
        {
            Debug.LogWarning("[Binder] Ponlo como PRIMER componente de la raiz para que asigne antes que el resto. Refrescando tras el spawn.", this);
            StartCoroutine(RefreshAfterSpawn());
        }

        modelIndex.OnValueChanged += OnIndexChanged;
    }

    public override void OnNetworkDespawn()
    {
        modelIndex.OnValueChanged -= OnIndexChanged;
    }

    private void OnIndexChanged(int oldIndex, int newIndex)
    {
        ApplyModel(newIndex);
        RefreshDependents();
    }

    private int PickFreeIndex()
    {
        var used = new HashSet<int>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var po = client.PlayerObject;
            if (po != null && po != NetworkObject && po.TryGetComponent(out PlayerModelBinder other))
                used.Add(other.modelIndex.Value);
        }
        for (int i = 0; i < models.Length; i++)
            if (!used.Contains(i)) return i;

        return (int)(OwnerClientId % (ulong)models.Length);
    }

    // ---------- Aplicar modelo ----------

    private void ApplyModel(int index)
    {
        if (models == null || models.Length == 0) return;
        index = Mathf.Clamp(index, 0, models.Length - 1);

        CacheOriginals();
        for (int i = 0; i < models.Length; i++)
            SetModelActive(i, i == index);

        foreach (var b in bindings)
            Assign(b, index);

        CurrentIndex = index;
    }

    private void CacheOriginals()
    {
        if (originalCulling != null) return;
        originalCulling = new AnimatorCullingMode[models.Length][];
        for (int i = 0; i < models.Length; i++)
        {
            var anims = models[i] != null
                ? models[i].GetComponentsInChildren<Animator>(true) : new Animator[0];
            originalCulling[i] = new AnimatorCullingMode[anims.Length];
            for (int a = 0; a < anims.Length; a++)
                originalCulling[i][a] = anims[a].cullingMode;
        }
    }

    private void SetModelActive(int i, bool active)
    {
        if (models[i] == null) return;

        foreach (var r in models[i].GetComponentsInChildren<Renderer>(true))
            r.forceRenderingOff = !active;

        var anims = models[i].GetComponentsInChildren<Animator>(true);
        for (int a = 0; a < anims.Length; a++)
            anims[a].cullingMode = active ? originalCulling[i][a] : AnimatorCullingMode.CullCompletely;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (; type != null; type = type.BaseType)
        {
            var f = type.GetField(name, flags);
            if (f != null) return f;
        }
        return null;
    }

    private void Assign(Binding b, int index)
    {
        if (b == null || b.target == null || string.IsNullOrEmpty(b.field) ||
            b.values == null || index >= b.values.Length) return;

        FieldInfo f = FindField(b.target.GetType(), b.field);
        if (f == null)
        {
            Debug.LogWarning($"[Binder] No existe el campo '{b.field}' en {b.target.GetType().Name}.", this);
            return;
        }

        UnityEngine.Object v = b.values[index];
        if (v != null && !f.FieldType.IsInstanceOfType(v))
        {
            Debug.LogWarning($"[Binder] Tipo incompatible en {b.target.GetType().Name}.{b.field}.", this);
            return;
        }
        f.SetValue(b.target, v);
    }

    // ---------- Refresco (solo si el binder no corrio primero o el indice cambia luego) ----------

    private IEnumerator RefreshAfterSpawn()
    {
        yield return null;
        yield return null;
        RefreshDependents();
    }

    private void RefreshDependents()
    {
        if (cameraSetup != null) cameraSetup.ApplyLayerSetup();
        if (switcher == null || loadout == null || visuals == null) return;

        int idx = switcher.CurrentWeaponIndex;
        visuals.SetupCatalog(IsOwner);                   // oculta los worldmodels del modelo nuevo
        visuals.UpdateWeaponVisuals(loadout.Weapons, idx); // muestra el del arma actual

        var shoot = switcher.GetShootAt(idx);
        if (shoot != null && presentation != null)
            presentation.ApplyGripsForWeapon(shoot.gameObject);
    }

#if UNITY_EDITOR
    // ================= HERRAMIENTAS DE EDITOR =================

    private static readonly Regex Prefix = new Regex(@"^mixamorig\d*:");
    private static string Norm(string n) => Prefix.Replace(n, "");

    private int ModelOf(Transform t)
    {
        for (int i = 0; i < models.Length; i++)
            if (models[i] != null && t.IsChildOf(models[i].transform)) return i;
        return -1;
    }

    private static string PathFrom(Transform root, Transform t)
    {
        var parts = new List<string>();
        for (var c = t; c != root; c = c.parent) parts.Add(Norm(c.name));
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static Transform Resolve(Transform root, string path)
    {
        var cur = root;
        if (path.Length == 0) return cur;
        foreach (var seg in path.Split('/'))
        {
            Transform next = null;
            foreach (Transform c in cur)
                if (Norm(c.name) == seg) { next = c; break; }
            if (next == null) return null;
            cur = next;
        }
        return cur;
    }

    private static UnityEngine.Object Equivalent(UnityEngine.Object original, Transform fromRoot, Transform toRoot)
    {
        Transform t = original is Component c ? c.transform : ((GameObject)original).transform;
        Transform eq = Resolve(toRoot, PathFrom(fromRoot, t));
        if (eq == null) return null;
        if (original is GameObject) return eq.gameObject;
        if (original is Transform) return eq;
        return eq.GetComponent(original.GetType());
    }

    private static Dictionary<string, UnityEngine.Object> ReferenceProps(Component c)
    {
        var dict = new Dictionary<string, UnityEngine.Object>();
        var so = new SerializedObject(c);
        var it = so.GetIterator();
        while (it.NextVisible(true))
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.propertyPath != "m_Script")
                dict[it.propertyPath] = it.objectReferenceValue;
        return dict;
    }

    [ContextMenu("Binder/1. Escanear referencias hacia los modelos")]
    private void ScanReferences()
    {
        Undo.RecordObject(this, "Escanear bindings");
        var found = new List<Binding>();

        foreach (var comp in GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp == this || comp is Transform) continue;
            if (ModelOf(comp.transform) >= 0) continue;   // solo lo que esta FUERA de los modelos

            var so = new SerializedObject(comp);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var val = it.objectReferenceValue;
                if (val == null) continue;

                Transform vt = val is Component vc ? vc.transform : val is GameObject vg ? vg.transform : null;
                if (vt == null) continue;

                int src = ModelOf(vt);
                if (src < 0) continue;

                if (it.propertyPath.Contains(".") || it.propertyPath.Contains("["))
                {
                    Debug.LogWarning($"[Binder] Campo anidado no soportado: {comp.GetType().Name}.{it.propertyPath} en '{comp.name}'. Hay que resolverlo a mano.");
                    continue;
                }

                var b = new Binding { target = comp, field = it.name, values = new UnityEngine.Object[models.Length] };
                for (int k = 0; k < models.Length; k++)
                {
                    b.values[k] = k == src ? val : Equivalent(val, models[src].transform, models[k].transform);
                    if (b.values[k] == null)
                        Debug.LogWarning($"[Binder] {comp.GetType().Name}.{it.name} ('{comp.name}'): sin equivalente en {models[k].name}");
                }
                found.Add(b);
            }
        }

        bindings = found;
        EditorUtility.SetDirty(this);
        Debug.Log($"[Binder] {found.Count} bindings generados.");
    }

    [ContextMenu("Binder/2. Validar referencias de los modelos")]
    private void Validate()
    {
        if (models == null || models.Length < 2) return;
        int warnings = 0;
        Transform refRoot = models[0].transform;

        foreach (var comp in models[0].GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp is Transform) continue;

            string path = PathFrom(refRoot, comp.transform);
            Type type = comp.GetType();
            int idx = Array.IndexOf(comp.GetComponents(type), comp);
            var refs = ReferenceProps(comp);

            for (int k = 1; k < models.Length; k++)
            {
                Transform t = Resolve(models[k].transform, path);
                Component[] others = t != null ? t.GetComponents(type) : null;
                if (others == null || idx >= others.Length)
                {
                    Debug.LogWarning($"[Binder] {models[k].name}: falta {type.Name} en '{path}'");
                    warnings++;
                    continue;
                }
                foreach (var kv in ReferenceProps(others[idx]))
                    if (kv.Value == null && refs.TryGetValue(kv.Key, out var orig) && orig != null)
                    {
                        Debug.LogWarning($"[Binder] {models[k].name}: {type.Name}.{kv.Key} esta vacio en '{path}' (en {models[0].name} si tiene valor)");
                        warnings++;
                    }
            }
        }
        Debug.Log($"[Binder] Validacion terminada: {warnings} avisos.");
    }
#endif
}