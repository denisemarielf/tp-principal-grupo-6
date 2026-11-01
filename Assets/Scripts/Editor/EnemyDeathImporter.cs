using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Asigna las animaciones de muerte al prefab Enemy. Al morir, cada enemigo elige una al azar.
// La primera va en deathClip y las demas en extraDeathClips de EnemyHealth.
[InitializeOnLoad]
public static class EnemyDeathImporter
{
    public static readonly string[] ClipPaths =
    {
        "Assets/Models/Enemy/Zombie Death.fbx",
        "Assets/Models/Enemy/Zombie Death2.fbx"
    };

    private const string PrefabPath = "Assets/Prefabs/Enemy/Enemy.prefab";

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

        List<AnimationClip> clips = new List<AnimationClip>();
        foreach (string path in ClipPaths)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));

            if (clip != null)
                clips.Add(clip);
        }

        if (clips.Count == 0)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            EnemyHealth health = root.GetComponent<EnemyHealth>();
            if (health == null)
                return;

            SerializedObject serialized = new SerializedObject(health);
            SerializedProperty main = serialized.FindProperty("deathClip");
            SerializedProperty extras = serialized.FindProperty("extraDeathClips");
            if (main == null || extras == null)
                return;

            bool dirty = false;
            if (main.objectReferenceValue != clips[0])
            {
                main.objectReferenceValue = clips[0];
                dirty = true;
            }

            int extraCount = clips.Count - 1;
            if (extras.arraySize != extraCount)
            {
                extras.arraySize = extraCount;
                dirty = true;
            }

            for (int i = 0; i < extraCount; i++)
            {
                SerializedProperty element = extras.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != clips[i + 1])
                {
                    element.objectReferenceValue = clips[i + 1];
                    dirty = true;
                }
            }

            if (!dirty)
                return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("EnemyDeathImporter: " + clips.Count + " animaciones de muerte asignadas a los enemigos.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static bool IsDeathPath(string path)
    {
        return path != null && ClipPaths.Contains(path);
    }

    // La caída tiene que quedar como root motion. Si Unity la hornea en la pose, el cuerpo no baja.
    private static bool EnsureFallReachesGround()
    {
        bool reimported = false;
        foreach (string path in ClipPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                continue;

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
                continue;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            reimported = true;
        }

        return reimported;
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
            if (EnemyDeathImporter.IsDeathPath(path))
            {
                EditorApplication.delayCall += EnemyDeathImporter.Setup;
                return;
            }
        }
    }
}