using System.Linq;
using UnityEditor;
using UnityEngine;

// Asigna Zombie Death al prefab Enemy. Las tres variantes lo usan al morir.
[InitializeOnLoad]
public static class EnemyDeathImporter
{
    public const string ClipPath = "Assets/Models/Enemy/Zombie Death.fbx";
    private const string PrefabPath = "Assets/Prefabs/Enemy.prefab";

    static EnemyDeathImporter()
    {
        EditorApplication.delayCall += Setup;
    }

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        if (EnsureFallReachesGround())
            return;

        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));

        if (clip == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            EnemyHealth health = root.GetComponent<EnemyHealth>();
            if (health == null)
                return;

            SerializedObject serialized = new SerializedObject(health);
            SerializedProperty property = serialized.FindProperty("deathClip");
            if (property == null || property.objectReferenceValue == clip)
                return;

            property.objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("EnemyDeathImporter: clip '" + clip.name + "' asignado a los enemigos.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // La caída tiene que quedar como root motion. Si Unity la hornea en la pose, el cuerpo no baja.
    private static bool EnsureFallReachesGround()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
        if (importer == null)
            return false;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return false;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            if (!clip.keepOriginalPositionY && !clip.keepOriginalPositionXZ && !clip.keepOriginalOrientation
                && !clip.lockRootHeightY && !clip.lockRootPositionXZ && !clip.lockRootRotation
                && !clip.loopTime && !clip.heightFromFeet)
                continue;

            clip.keepOriginalPositionY = false;
            clip.keepOriginalPositionXZ = false;
            clip.keepOriginalOrientation = false;
            clip.lockRootHeightY = false;
            clip.lockRootPositionXZ = false;
            clip.lockRootRotation = false;
            clip.loopTime = false;
            clip.heightFromFeet = false;
            clips[i] = clip;
            changed = true;
        }

        if (!changed)
            return false;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }
}

public class EnemyDeathPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path == EnemyDeathImporter.ClipPath)
            {
                EditorApplication.delayCall += EnemyDeathImporter.Setup;
                return;
            }
        }
    }
}
