using UnityEditor;
using UnityEngine;

// Deja en el prefab Enemy un modelo por variante y apaga la capsula visible.
[InitializeOnLoad]
public class EnemyModelAttacher : AssetPostprocessor
{
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string FragilPath = "Assets/Models/Enemy/Zombiefragil.fbx";
    private const string NormalPath = "Assets/Models/Enemy/Zombiegirl.fbx";
    private const string DuroPath = "Assets/Models/Enemy/Zombieduro.fbx";

    private static bool ready;

    static EnemyModelAttacher()
    {
        EditorApplication.delayCall += AttachIfMissing;
    }

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path == FragilPath || path == NormalPath || path == DuroPath || path == EnemyPrefabPath)
            {
                ready = false;
                EditorApplication.delayCall += AttachIfMissing;
                return;
            }
        }
    }

    private static void AttachIfMissing()
    {
        if (ready || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) == null)
            return;

        GameObject fragilAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FragilPath);
        GameObject normalAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NormalPath);
        GameObject duroAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DuroPath);
        if (fragilAsset == null && normalAsset == null && duroAsset == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            bool dirty = false;

            Transform legacy = root.transform.Find("Ch10_nonPBR");
            if (legacy != null && root.transform.Find("Fragil") == null)
            {
                legacy.name = "Fragil";
                dirty = true;
            }

            GameObject fragil = EnsureChild(root.transform, "Fragil", fragilAsset, FragilPath, ref dirty);
            GameObject normal = EnsureChild(root.transform, "Normal", normalAsset, NormalPath, ref dirty);
            GameObject resistente = EnsureChild(root.transform, "Resistente", duroAsset, DuroPath, ref dirty);

            MeshRenderer capsule = root.GetComponent<MeshRenderer>();
            if (capsule != null && capsule.enabled)
            {
                capsule.enabled = false;
                dirty = true;
            }

            EnemyAppearance appearance = root.GetComponent<EnemyAppearance>();
            if (appearance == null)
            {
                appearance = root.AddComponent<EnemyAppearance>();
                dirty = true;
            }

            SerializedObject serialized = new SerializedObject(appearance);
            dirty |= Assign(serialized.FindProperty("fragil"), fragil);
            dirty |= Assign(serialized.FindProperty("normal"), normal);
            dirty |= Assign(serialized.FindProperty("resistente"), resistente);
            if (dirty)
                serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!dirty)
            {
                ready = fragil != null && normal != null && resistente != null;
                return;
            }

            if (fragil != null)
                fragil.SetActive(true);
            if (normal != null)
                normal.SetActive(false);
            if (resistente != null)
                resistente.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            ready = fragil != null && normal != null && resistente != null;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject EnsureChild(Transform parent, string childName, GameObject asset, string assetPath, ref bool dirty)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            if (asset == null)
                return existing.gameObject;

            Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(existing.gameObject);
            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (sourcePath == assetPath)
                return existing.gameObject;

            Object.DestroyImmediate(existing.gameObject);
            dirty = true;
        }

        if (asset == null)
            return null;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.name = childName;
        instance.transform.localPosition = new Vector3(0f, -1f, 0f);
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = childName == "Resistente" ? Vector3.one * 1.15f : Vector3.one;
        dirty = true;
        return instance;
    }

    private static bool Assign(SerializedProperty property, GameObject model)
    {
        if (property.objectReferenceValue == model)
            return false;

        property.objectReferenceValue = model;
        return true;
    }
}
