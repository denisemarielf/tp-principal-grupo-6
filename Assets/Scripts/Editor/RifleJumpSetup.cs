using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Importa Rifle Jump como humanoide y lo mete en los 4 controllers del personaje.
// El clip se retargetea: el FBX nuevo usa mixamorig5 y los modelos usan mixamorig.
[InitializeOnLoad]
public class RifleJumpSetup : AssetPostprocessor
{
    private const string FbxPath = "Assets/Models/PlayerModel/rifle jump.fbx";

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

    static RifleJumpSetup()
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
            && importer.clipAnimations[0].name == "Jump";

        if (!ready)
        {
            importer.SaveAndReimport();
            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        }

        AnimationClip clip = LoadClip();
        if (clip == null || clip.length < 0.05f)
        {
            Debug.LogError("Rifle jump: el clip no se importó. Revisá el FBX en Assets/Models/PlayerModel.");
            return;
        }

        foreach (string path in Controllers)
            Wire(path, clip);

        foreach (string path in CharacterPrefabs)
            SyncJumpParameter(path);

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
            : new ModelImporterClipAnimation { takeName = "mixamo.com", firstFrame = 0, lastFrame = 18 };

        clip.name = "Jump";
        clip.loopTime = false;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        importer.clipAnimations = new[] { clip };

        HumanDescription description = importer.humanDescription;
        description.human = BuildBones();
        importer.humanDescription = description;
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (assetPath.Replace('\\', '/') != FbxPath)
            return;

        Animator animator = root.GetComponentInChildren<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            Debug.LogError("Rifle jump: Unity no armó un avatar humanoide válido para este FBX.", root);
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
            Debug.LogError("Rifle jump: no está el controller " + path);
            return;
        }

        if (!HasParameter(controller, "Jump"))
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState walk = FindState(machine, "Walk");
        AnimatorState run = FindState(machine, "Run");
        AnimatorState jump = FindState(machine, "Jump");
        if (walk == null || run == null)
        {
            Debug.LogError("Rifle jump: a " + path + " le faltan los estados Walk o Run.");
            return;
        }

        if (jump == null)
            jump = machine.AddState("Jump", new Vector3(560f, 60f, 0f));

        jump.motion = clip;
        EnsureTrigger(walk, jump);
        EnsureTrigger(run, jump);
        EnsureExit(jump, walk, false);
        EnsureExit(jump, run, true);
        EditorUtility.SetDirty(controller);
    }

    // NetworkAnimator arma la lista al validar el prefab. Si el controller todavía no tenía Jump, la borra.
    private static void SyncJumpParameter(string prefabPath)
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
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "Jump")
                return;
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        SerializedProperty entry = list.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("name").stringValue = "Jump";
        entry.FindPropertyRelative("NameHash").intValue = Animator.StringToHash("Jump");
        entry.FindPropertyRelative("Synchronize").boolValue = true;
        entry.FindPropertyRelative("ParameterType").intValue = (int)AnimatorControllerParameterType.Trigger;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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

    private static void EnsureTrigger(AnimatorState from, AnimatorState jump)
    {
        foreach (AnimatorStateTransition transition in from.transitions)
        {
            if (transition.destinationState == jump)
                return;
        }

        AnimatorStateTransition created = from.AddTransition(jump);
        created.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        created.hasExitTime = false;
        created.duration = 0.08f;
        created.hasFixedDuration = true;
    }

    private static void EnsureExit(AnimatorState jump, AnimatorState destination, bool sprinting)
    {
        foreach (AnimatorStateTransition transition in jump.transitions)
        {
            if (transition.destinationState == destination)
                return;
        }

        AnimatorStateTransition created = jump.AddTransition(destination);
        created.AddCondition(sprinting ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "isSprinting");
        created.hasExitTime = true;
        created.exitTime = 0.9f;
        created.duration = 0.12f;
        created.hasFixedDuration = true;
    }

    private static HumanBone[] BuildBones()
    {
        return new[]
        {
            Bone("Hips", "mixamorig5:Hips"),
            Bone("LeftUpperLeg", "mixamorig5:LeftUpLeg"),
            Bone("RightUpperLeg", "mixamorig5:RightUpLeg"),
            Bone("LeftLowerLeg", "mixamorig5:LeftLeg"),
            Bone("RightLowerLeg", "mixamorig5:RightLeg"),
            Bone("LeftFoot", "mixamorig5:LeftFoot"),
            Bone("RightFoot", "mixamorig5:RightFoot"),
            Bone("Spine", "mixamorig5:Spine"),
            Bone("Chest", "mixamorig5:Spine1"),
            Bone("UpperChest", "mixamorig5:Spine2"),
            Bone("Neck", "mixamorig5:Neck"),
            Bone("Head", "mixamorig5:Head"),
            Bone("LeftShoulder", "mixamorig5:LeftShoulder"),
            Bone("RightShoulder", "mixamorig5:RightShoulder"),
            Bone("LeftUpperArm", "mixamorig5:LeftArm"),
            Bone("RightUpperArm", "mixamorig5:RightArm"),
            Bone("LeftLowerArm", "mixamorig5:LeftForeArm"),
            Bone("RightLowerArm", "mixamorig5:RightForeArm"),
            Bone("LeftHand", "mixamorig5:LeftHand"),
            Bone("RightHand", "mixamorig5:RightHand"),
            Bone("LeftToes", "mixamorig5:LeftToeBase"),
            Bone("RightToes", "mixamorig5:RightToeBase"),
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
