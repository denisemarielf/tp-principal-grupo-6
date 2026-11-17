using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Importa Hit Reaction como humanoide y lo mete en los 4 controllers del personaje.
// El clip se retargetea: el FBX trae su propio prefijo mixamorigN y los modelos usan mixamorig.
[InitializeOnLoad]
public class HitReactionSetup : AssetPostprocessor
{
    // Verificá que el nombre coincida exactamente con el FBX en el Project.
    private const string FbxPath = "Assets/Models/PlayerModel/Hit Reaction.fbx";
    private const string ClipName = "Hit";
    private const string StateName = "HitReaction";
    private const string TriggerName = "Hit";
    private const string FallbackBonePrefix = "mixamorig";

    private static readonly string[] Controllers =
    {
        "Assets/Models/PlayerModel/PoliceGasmask/animatorControllerCharacter.controller",
        "Assets/Models/PlayerModel/NormalSwat/animatorControllerCharacter.controller",
        "Assets/Models/PlayerModel/SwatGuy/animatorControllerCharacter.controller",
        "Assets/Models/PlayerModel/WorkerGuy/animatorControllerCharacter.controller",
    };

    private static readonly string[] CharacterPrefabs =
    {
        "Assets/Prefabs/Player/PlayerModels/CharacterModel_0.prefab",
        "Assets/Prefabs/Player/PlayerModels/CharacterModel_01.prefab",
        "Assets/Prefabs/Player/PlayerModels/CharacterModel_02.prefab",
        "Assets/Prefabs/Player/PlayerModels/CharacterModel_03.prefab",
    };

    static HitReactionSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    private static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            return;

        bool ready = importer.animationType == ModelImporterAnimationType.Human
            && importer.clipAnimations != null
            && importer.clipAnimations.Length == 1
            && importer.clipAnimations[0].name == ClipName;

        if (!ready)
        {
            importer.SaveAndReimport();
            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        }

        AnimationClip clip = LoadClip();
        if (clip == null || clip.length < 0.05f)
        {
            Debug.LogError("Hit reaction: el clip no se importó. Revisá el FBX en " + FbxPath);
            return;
        }

        foreach (string path in Controllers)
            Wire(path, clip);

        foreach (string path in CharacterPrefabs)
        {
            WireControllerOfPrefab(path, clip);
            SyncTriggerParameter(path, TriggerName);
        }

        AssetDatabase.SaveAssets();
    }

    private void OnPreprocessModel()
    {
        if (assetPath.Replace('\\', '/') != FbxPath)
            return;

        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
        ModelImporterClipAnimation clip = defaults != null && defaults.Length > 0
            ? defaults[0]
            : new ModelImporterClipAnimation { takeName = "mixamo.com", firstFrame = 0, lastFrame = 30 };

        clip.name = ClipName;
        clip.loopTime = false;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        importer.clipAnimations = new[] { clip };

        HumanDescription description = importer.humanDescription;
        description.human = BuildBones(DetectPrefix(importer));
        importer.humanDescription = description;
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (assetPath.Replace('\\', '/') != FbxPath)
            return;

        Animator animator = root.GetComponentInChildren<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            Debug.LogError("Hit reaction: Unity no armó un avatar humanoide válido para este FBX.", root);
    }

    // Busca "mixamorigN:Hips" en la jerarquía del FBX y devuelve "mixamorigN".
    private static string DetectPrefix(ModelImporter importer)
    {
        string[] paths = importer.transformPaths;
        if (paths != null)
        {
            foreach (string path in paths)
            {
                string leaf = path.Substring(path.LastIndexOf('/') + 1);
                int colon = leaf.IndexOf(':');
                if (colon > 0 && leaf.EndsWith(":Hips"))
                    return leaf.Substring(0, colon);
            }
        }

        return FallbackBonePrefix;
    }

    private static AnimationClip LoadClip()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                return clip;
        }

        return null;
    }

    private static void Wire(string path, AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            Debug.LogError("Hit reaction: no está el controller " + path);
            return;
        }

        if (!HasParameter(controller, TriggerName))
            controller.AddParameter(TriggerName, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState walk = FindState(machine, "Walk");
        AnimatorState run = FindState(machine, "Run");
        AnimatorState hit = FindState(machine, StateName);
        if (walk == null || run == null)
        {
            Debug.LogError("Hit reaction: a " + path + " le faltan los estados Walk o Run.");
            return;
        }

        if (hit == null)
            hit = machine.AddState(StateName, new Vector3(560f, 160f, 0f));

        hit.motion = clip;
        EnsureTrigger(walk, hit);
        EnsureTrigger(run, hit);
        EnsureExit(hit, walk, false);
        EnsureExit(hit, run, true);
        EditorUtility.SetDirty(controller);
    }

    // Engancha el controller que el prefab realmente usa, aunque no esté en la lista de arriba.
    private static void WireControllerOfPrefab(string prefabPath, AnimationClip clip)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (root == null)
        {
            Debug.LogError("Hit reaction: no está el prefab " + prefabPath);
            return;
        }

        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
            if (runtime is AnimatorOverrideController overrideController)
                runtime = overrideController.runtimeAnimatorController;

            string controllerPath = runtime != null ? AssetDatabase.GetAssetPath(runtime) : null;
            if (string.IsNullOrEmpty(controllerPath))
            {
                Debug.LogWarning("Hit reaction: el Animator de " + prefabPath + " no tiene controller.");
                continue;
            }

            Wire(controllerPath, clip);
        }
    }

    // NetworkAnimator arma la lista al validar el prefab. Si el controller todavía no tenía el parámetro, lo borra.
    private static void SyncTriggerParameter(string prefabPath, string parameterName)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (root == null)
            return;

        MonoBehaviour networkAnimator = null;
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.GetType().Name == "NetworkAnimator")
            {
                networkAnimator = behaviour;
                break;
            }
        }

        if (networkAnimator == null)
            return;

        var serialized = new SerializedObject(networkAnimator);
        SerializedProperty list = serialized.FindProperty("AnimatorParameterEntries.ParameterEntries");
        if (list == null || !list.isArray)
            return;

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == parameterName)
                return;
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        SerializedProperty entry = list.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("name").stringValue = parameterName;
        entry.FindPropertyRelative("NameHash").intValue = Animator.StringToHash(parameterName);
        entry.FindPropertyRelative("Synchronize").boolValue = true;
        entry.FindPropertyRelative("ParameterType").intValue = (int)AnimatorControllerParameterType.Trigger;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(networkAnimator);
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

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == name)
                return child.state;
        }

        return null;
    }

    private static void EnsureTrigger(AnimatorState from, AnimatorState hit)
    {
        foreach (AnimatorStateTransition transition in from.transitions)
        {
            if (transition.destinationState == hit)
                return;
        }

        AnimatorStateTransition created = from.AddTransition(hit);
        created.AddCondition(AnimatorConditionMode.If, 0f, TriggerName);
        created.hasExitTime = false;
        created.duration = 0.05f;
        created.hasFixedDuration = true;
    }

    private static void EnsureExit(AnimatorState hit, AnimatorState destination, bool sprinting)
    {
        foreach (AnimatorStateTransition transition in hit.transitions)
        {
            if (transition.destinationState == destination)
                return;
        }

        AnimatorStateTransition created = hit.AddTransition(destination);
        created.AddCondition(sprinting ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "isSprinting");
        created.hasExitTime = true;
        created.exitTime = 0.85f;
        created.duration = 0.1f;
        created.hasFixedDuration = true;
    }

    private static HumanBone[] BuildBones(string p)
    {
        return new[]
        {
            Bone("Hips", p + ":Hips"),
            Bone("LeftUpperLeg", p + ":LeftUpLeg"),
            Bone("RightUpperLeg", p + ":RightUpLeg"),
            Bone("LeftLowerLeg", p + ":LeftLeg"),
            Bone("RightLowerLeg", p + ":RightLeg"),
            Bone("LeftFoot", p + ":LeftFoot"),
            Bone("RightFoot", p + ":RightFoot"),
            Bone("Spine", p + ":Spine"),
            Bone("Chest", p + ":Spine1"),
            Bone("UpperChest", p + ":Spine2"),
            Bone("Neck", p + ":Neck"),
            Bone("Head", p + ":Head"),
            Bone("LeftShoulder", p + ":LeftShoulder"),
            Bone("RightShoulder", p + ":RightShoulder"),
            Bone("LeftUpperArm", p + ":LeftArm"),
            Bone("RightUpperArm", p + ":RightArm"),
            Bone("LeftLowerArm", p + ":LeftForeArm"),
            Bone("RightLowerArm", p + ":RightForeArm"),
            Bone("LeftHand", p + ":LeftHand"),
            Bone("RightHand", p + ":RightHand"),
            Bone("LeftToes", p + ":LeftToeBase"),
            Bone("RightToes", p + ":RightToeBase"),
        };
    }

    private static HumanBone Bone(string humanName, string boneName)
    {
        var bone = new HumanBone();
        bone.humanName = humanName;
        bone.boneName = boneName;
        var limit = new HumanLimit();
        limit.useDefaultValues = true;
        bone.limit = limit;
        return bone;
    }
}
