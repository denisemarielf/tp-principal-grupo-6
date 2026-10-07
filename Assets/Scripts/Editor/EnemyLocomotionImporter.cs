using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Deja Idle, Walk, Run y Attack en el lugar y arma el controller de los enemigos.
[InitializeOnLoad]
public static class EnemyLocomotionImporter
{
    private const string PrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string ControllerPath = "Assets/Models/Enemy/EnemyLocomotion.controller";

    private static readonly string[] ClipPaths =
    {
        "Assets/Models/Enemy/Zombie Idle.fbx",
        "Assets/Models/Enemy/Zombie Walk.fbx",
        "Assets/Models/Enemy/Zombie Run.fbx",
        "Assets/Models/Enemy/Zombie Attack.fbx"
    };

    static EnemyLocomotionImporter()
    {
        EditorApplication.delayCall += Setup;
    }

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        if (BakeClipsInPlace())
            return;

        AnimationClip idle = LoadClip(ClipPaths[0]);
        AnimationClip walk = LoadClip(ClipPaths[1]);
        AnimationClip run = LoadClip(ClipPaths[2]);
        AnimationClip attack = LoadClip(ClipPaths[3]);
        if (idle == null || walk == null || run == null || attack == null)
            return;

        AnimatorController controller = EnsureController(idle, walk, run, attack);
        AssignController(controller);
    }

    private static bool BakeClipsInPlace()
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
                if (clip.keepOriginalPositionY && clip.keepOriginalPositionXZ && clip.keepOriginalOrientation
                    && clip.lockRootHeightY && clip.lockRootPositionXZ && clip.lockRootRotation
                    && clip.loopTime && !clip.heightFromFeet)
                    continue;

                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.lockRootRotation = true;
                clip.loopTime = true;
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

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
    }

    private static AnimatorController EnsureController(
        AnimationClip idle,
        AnimationClip walk,
        AnimationClip run,
        AnimationClip attack)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        if (!HasParameter(controller, "state"))
            controller.AddParameter("state", AnimatorControllerParameterType.Int);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = FindOrAdd(machine, "Idle", idle, true);
        AnimatorState walkState = FindOrAdd(machine, "Walk", walk, false);
        AnimatorState runState = FindOrAdd(machine, "Run", run, false);
        AnimatorState attackState = FindOrAdd(machine, "Attack", attack, false);

        EnsureAnyState(machine, idleState, 0);
        EnsureAnyState(machine, walkState, 1);
        EnsureAnyState(machine, runState, 2);
        EnsureAnyState(machine, attackState, 3);
        RemoveExtraStates(machine);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static bool HasParameter(AnimatorController controller, string name)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name)
                return true;
        }

        return false;
    }

    private static AnimatorState FindOrAdd(AnimatorStateMachine machine, string name, Motion motion, bool defaultState)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name != name)
                continue;

            if (child.state.motion != motion)
                child.state.motion = motion;
            if (defaultState)
                machine.defaultState = child.state;
            return child.state;
        }

        AnimatorState state = machine.AddState(name);
        state.motion = motion;
        if (defaultState)
            machine.defaultState = state;
        return state;
    }

    private static void EnsureAnyState(AnimatorStateMachine machine, AnimatorState state, int value)
    {
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (transition.destinationState == state)
                return;
        }

        AnimatorStateTransition created = machine.AddAnyStateTransition(state);
        created.AddCondition(AnimatorConditionMode.Equals, value, "state");
        created.duration = 0.1f;
        created.hasExitTime = false;
        created.canTransitionToSelf = false;
    }

    private static void RemoveExtraStates(AnimatorStateMachine machine)
    {
        List<AnimatorState> extras = new List<AnimatorState>();
        foreach (ChildAnimatorState child in machine.states)
        {
            string name = child.state.name;
            if (name != "Idle" && name != "Walk" && name != "Run" && name != "Attack")
                extras.Add(child.state);
        }

        foreach (AnimatorState extra in extras)
            machine.RemoveState(extra);
    }

    private static void AssignController(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            EnemyAnimation animation = root.GetComponent<EnemyAnimation>();
            if (animation == null)
                animation = root.AddComponent<EnemyAnimation>();

            SerializedObject serialized = new SerializedObject(animation);
            SerializedProperty property = serialized.FindProperty("locomotion");
            if (property == null || property.objectReferenceValue == controller)
                return;

            property.objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("EnemyLocomotionImporter: controller asignado a los enemigos.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

public class EnemyLocomotionPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path != null && path.StartsWith("Assets/Models/Enemy/Zombie ") && path.EndsWith(".fbx") && !path.EndsWith("Zombie Death.fbx"))
            {
                EditorApplication.delayCall += EnemyLocomotionImporter.Setup;
                return;
            }
        }
    }
}
