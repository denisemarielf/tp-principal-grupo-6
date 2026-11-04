using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

// Arma sola todo lo del mini jefe a partir de los FBX de Assets/Models/Enemy/Miniboss:
//  - deja el rig en Humanoid y los clips "en el lugar" (igual que EnemyLocomotionImporter),
//  - crea el AnimatorController (idle/walk/run/attack/jump con sus variantes),
//  - crea Assets/Prefabs/Enemy/MiniBoss.prefab (prefab propio, no toca Enemy.prefab),
//  - asigna el modelo, el Avatar (huesos), el controller, la muerte y las duraciones de los clips,
//  - registra el prefab en la lista de NetworkPrefabs.
// Los clips se buscan por prefijo del nombre: idle*, walk*, run*, attack*, jump*, taunt*, vomit*, grab*, throw*, dying*.
// Para agregar o sacar una variante alcanza con dejar o borrar el FBX. Menu: Tools > MiniBoss > Reconstruir.
[InitializeOnLoad]
public static class MiniBossBuilder
{
    private const string Dir = "Assets/Models/Enemy/Miniboss";
    private const string PrefabPath = "Assets/Prefabs/Enemy/MiniBoss.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy/Enemy.prefab";
    private const string ControllerPath = Dir + "/MiniBoss.controller";
    private const string ModelPrefix = "Parasite";
    private const string DeathPrefix = "dying";

    private const float ModelScale = 1.5f;
    private const float DefaultHealth = 1500f;

    private class Group
    {
        public string name;
        public int stateValue;
        public string indexParam;   // null si hay un solo clip
        public bool loop;
        public bool optional;
        public string prefix;
    }

    private static readonly Group[] Groups =
    {
        new Group { name = "Idle",   stateValue = 0, indexParam = "idleIndex",   loop = true,  prefix = "idle" },
        new Group { name = "Walk",   stateValue = 1, indexParam = null,          loop = true,  prefix = "walk" },
        new Group { name = "Run",    stateValue = 2, indexParam = null,          loop = true,  prefix = "run" },
        new Group { name = "Attack", stateValue = 3, indexParam = "attackIndex", loop = false, prefix = "attack" },
        new Group { name = "Jump",   stateValue = 4, indexParam = "jumpIndex",   loop = false, optional = true, prefix = "jump" },
        new Group { name = "Taunt",  stateValue = 5, indexParam = null,          loop = false, optional = true, prefix = "taunt" },
        new Group { name = "Vomit",  stateValue = 6, indexParam = null,          loop = false, optional = true, prefix = "vomit" },
        new Group { name = "Grab",   stateValue = 7, indexParam = null,          loop = false, optional = true, prefix = "grab" },
        new Group { name = "Throw",  stateValue = 8, indexParam = null,          loop = false, optional = true, prefix = "throw" }
    };

    static MiniBossBuilder()
    {
        EditorApplication.delayCall += Setup;
    }

    [MenuItem("Tools/MiniBoss/Reconstruir")]
    private static void Rebuild()
    {
        Setup();
    }

    public static bool IsMiniBossPath(string path)
    {
        return path != null && path.StartsWith(Dir, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Busqueda de archivos ----------

    private static List<string> AllFbx()
    {
        List<string> result = new List<string>();
        if (!AssetDatabase.IsValidFolder(Dir))
            return result;

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsMiniBossPath(path))
                result.Add(path);
        }

        return result;
    }

    private static List<string> PathsWithPrefix(string prefix)
    {
        return AllFbx()
            .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path.Length)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsClipFile(string path)
    {
        string file = Path.GetFileNameWithoutExtension(path);
        if (file.StartsWith(DeathPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        return Groups.Any(group => file.StartsWith(group.prefix, StringComparison.OrdinalIgnoreCase));
    }

    // El modelo es el FBX "Parasite..." o, si no hay, el primero con malla que no sea un clip.
    private static string FindModelPath()
    {
        List<string> all = AllFbx();

        foreach (string path in all)
        {
            if (Path.GetFileNameWithoutExtension(path).StartsWith(ModelPrefix, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        foreach (string path in all)
        {
            if (IsClipFile(path))
                continue;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                return path;
        }

        return null;
    }

    // ---------- Importacion de FBX ----------

    // Rig Humanoid: asi los clips se retargetean al modelo y los huesos quedan asignados por el Avatar.
    private static bool EnsureHumanoid(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null || importer.animationType == ModelImporterAnimationType.Human)
            return false;

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();
        return true;
    }

    // Idle/Walk/Run/Attack/Jump quedan en el lugar: el avance en XZ no se hornea en la pose.
    private static bool BakeInPlace(string path, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
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
            if (clip.keepOriginalPositionY && !clip.keepOriginalPositionXZ && clip.keepOriginalOrientation
                && clip.lockRootHeightY && !clip.lockRootPositionXZ && clip.lockRootRotation
                && clip.loopTime == loop && !clip.heightFromFeet)
                continue;

            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = false;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = false;
            clip.lockRootRotation = true;
            clip.loopTime = loop;
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

    // La caida tiene que quedar como root motion (igual que EnemyDeathImporter) para que el cuerpo baje al piso.
    private static bool BakeDeath(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
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

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
    }

    // ---------- Flujo principal ----------

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        string modelPath = FindModelPath();
        if (modelPath == null)
            return;

        bool reimported = false;
        foreach (string path in AllFbx())
            reimported |= EnsureHumanoid(path);

        foreach (Group group in Groups)
        {
            foreach (string path in PathsWithPrefix(group.prefix))
                reimported |= BakeInPlace(path, group.loop);
        }

        foreach (string path in PathsWithPrefix(DeathPrefix))
            reimported |= BakeDeath(path);

        // Al reimportar, el postprocessor vuelve a llamar a Setup cuando los clips ya estan listos.
        if (reimported)
            return;

        Dictionary<Group, List<AnimationClip>> found = new Dictionary<Group, List<AnimationClip>>();
        foreach (Group group in Groups)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            foreach (string path in PathsWithPrefix(group.prefix))
            {
                AnimationClip clip = LoadClip(path);
                if (clip != null)
                    clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                if (group.optional)
                    continue;

                Debug.LogWarning("MiniBossBuilder: falta el clip '" + group.prefix + "*' en " + Dir);
                return;
            }

            found[group] = clips;
        }

        string deathPath = PathsWithPrefix(DeathPrefix).FirstOrDefault();
        AnimationClip death = deathPath != null ? LoadClip(deathPath) : null;
        if (death == null)
            Debug.LogWarning("MiniBossBuilder: no hay clip 'dying*': el jefe va a desaparecer sin animacion de muerte.");

        AnimatorController controller = EnsureController(found);
        EnsurePrefab(controller, found, death, modelPath);
        RegisterNetworkPrefab();
    }

    // ---------- Controller ----------

    private static string StateName(Group group, int index)
    {
        return index == 0 ? group.name : group.name + (index + 1);
    }

    private static bool HasParameter(AnimatorController controller, string name)
    {
        return controller.parameters.Any(parameter => parameter.name == name);
    }

    private static AnimatorController EnsureController(Dictionary<Group, List<AnimationClip>> found)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        if (!HasParameter(controller, "state"))
            controller.AddParameter("state", AnimatorControllerParameterType.Int);

        foreach (Group group in Groups)
        {
            if (group.indexParam != null && !HasParameter(controller, group.indexParam))
                controller.AddParameter(group.indexParam, AnimatorControllerParameterType.Int);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        HashSet<string> expected = new HashSet<string>();

        // Las transiciones se rehacen siempre: las condiciones dependen de cuantas variantes hay.
        foreach (AnimatorStateTransition old in machine.anyStateTransitions.ToArray())
            machine.RemoveAnyStateTransition(old);

        foreach (Group group in Groups)
        {
            if (!found.ContainsKey(group))
                continue;

            List<AnimationClip> clips = found[group];
            for (int i = 0; i < clips.Count; i++)
            {
                string stateName = StateName(group, i);
                expected.Add(stateName);

                AnimatorState state = FindOrAdd(machine, stateName, clips[i], group.stateValue == 0 && i == 0);

                AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
                transition.AddCondition(AnimatorConditionMode.Equals, group.stateValue, "state");
                if (clips.Count > 1 && group.indexParam != null)
                    transition.AddCondition(AnimatorConditionMode.Equals, i, group.indexParam);

                transition.duration = group.stateValue >= 3 ? 0.08f : 0.15f;
                transition.hasExitTime = false;
                transition.canTransitionToSelf = false;
            }
        }

        foreach (AnimatorState extra in machine.states
            .Select(child => child.state)
            .Where(state => !expected.Contains(state.name))
            .ToList())
            machine.RemoveState(extra);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
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

    // ---------- Prefab ----------

    private static void EnsurePrefab(
        AnimatorController controller,
        Dictionary<Group, List<AnimationClip>> found,
        AnimationClip death,
        string modelPath)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null)
            return;

        bool created = false;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            GameObject bare = new GameObject("MiniBoss");
            PrefabUtility.SaveAsPrefabAsset(bare, PrefabPath);
            UnityEngine.Object.DestroyImmediate(bare);
            created = true;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool dirty = created || root.GetComponent<EnemyVoice>() == null;

            if (created)
                CopyLayerAndTag(root);

            // Componentes de red y fisica.
            Ensure<NetworkObject>(root);
            Ensure<NetworkTransform>(root);

            NavMeshAgent agent = Ensure<NavMeshAgent>(root);
            CapsuleCollider capsule = Ensure<CapsuleCollider>(root);
            AudioSource audio = Ensure<AudioSource>(root);
            if (created)
            {
                agent.speed = 3.5f;
                agent.acceleration = 14f;
                agent.angularSpeed = 240f;
                agent.radius = 0.7f;
                agent.height = 3f;
                agent.stoppingDistance = 0f;
                agent.baseOffset = 0f;

                capsule.radius = 0.9f;
                capsule.height = 3f;
                capsule.center = new Vector3(0f, 1.5f, 0f);

                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.minDistance = 4f;
                audio.maxDistance = 45f;
            }

            EnemyHealth health = Ensure<EnemyHealth>(root);
            MiniBossAnimation animation = Ensure<MiniBossAnimation>(root);
            Ensure<MiniBossBrain>(root);
            Ensure<MiniBossHealthBar>(root);
            Ensure<EnemyVoice>(root);

            // Modelo: hijo "Model" con su Animator, Avatar (huesos) y controller.
            Transform modelTransform = root.transform.Find("Model");
            if (modelTransform != null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(modelTransform.gameObject) != modelAsset)
            {
                UnityEngine.Object.DestroyImmediate(modelTransform.gameObject);
                modelTransform = null;
            }

            if (modelTransform == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
                instance.name = "Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * ModelScale;
                modelTransform = instance.transform;
                dirty = true;
            }

            Animator animator = modelTransform.GetComponent<Animator>();
            if (animator == null)
            {
                animator = modelTransform.gameObject.AddComponent<Animator>();
                dirty = true;
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar != null && animator.avatar != avatar)
            {
                animator.avatar = avatar;
                dirty = true;
            }

            if (animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                dirty = true;
            }

            if (animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                dirty = true;
            }

            // Campos serializados.
            SerializedObject animationData = new SerializedObject(animation);
            dirty |= SetObject(animationData, "locomotion", controller);
            dirty |= SetObject(animationData, "audioSource", audio);
            dirty |= SetInt(animationData, "idleVariants", CountOf(found, "Idle"));
            dirty |= SetInt(animationData, "attackVariants", CountOf(found, "Attack"));
            dirty |= SetInt(animationData, "jumpVariants", CountOf(found, "Jump"));
            dirty |= SetLengths(animationData, "attackLengths", ClipsOf(found, "Attack"));
            dirty |= SetLengths(animationData, "jumpLengths", ClipsOf(found, "Jump"));
            dirty |= SetFloat(animationData, "tauntLength", FirstLength(found, "Taunt"));
            dirty |= SetFloat(animationData, "vomitLength", FirstLength(found, "Vomit"));
            dirty |= SetFloat(animationData, "grabLength", FirstLength(found, "Grab"));
            dirty |= SetFloat(animationData, "throwLength", FirstLength(found, "Throw"));
            animationData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject healthData = new SerializedObject(health);
            dirty |= SetObject(healthData, "deathClip", death);
            if (created)
                dirty |= SetFloat(healthData, "healthOverride", DefaultHealth);
            healthData.ApplyModifiedPropertiesWithoutUndo();

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("MiniBossBuilder: prefab del mini jefe actualizado en " + PrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    // Mismo layer y tag que el Enemy normal: las armas y las mascaras de fisica ya saben pegarle.
    private static void CopyLayerAndTag(GameObject root)
    {
        GameObject enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (enemy == null)
            return;

        root.layer = enemy.layer;
        try { root.tag = enemy.tag; } catch (UnityException) { }
    }

    private static List<AnimationClip> ClipsOf(Dictionary<Group, List<AnimationClip>> found, string groupName)
    {
        foreach (KeyValuePair<Group, List<AnimationClip>> pair in found)
        {
            if (pair.Key.name == groupName)
                return pair.Value;
        }

        return new List<AnimationClip>();
    }

    private static float FirstLength(Dictionary<Group, List<AnimationClip>> found, string groupName)
    {
        List<AnimationClip> clips = ClipsOf(found, groupName);
        return clips.Count > 0 ? clips[0].length : 0f;
    }

    private static int CountOf(Dictionary<Group, List<AnimationClip>> found, string groupName)
    {
        return ClipsOf(found, groupName).Count;
    }

    private static bool SetObject(SerializedObject data, string field, UnityEngine.Object value)
    {
        SerializedProperty property = data.FindProperty(field);
        if (property == null || property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetInt(SerializedObject data, string field, int value)
    {
        SerializedProperty property = data.FindProperty(field);
        if (property == null || property.intValue == value)
            return false;

        property.intValue = value;
        return true;
    }

    private static bool SetFloat(SerializedObject data, string field, float value)
    {
        SerializedProperty property = data.FindProperty(field);
        if (property == null || Mathf.Approximately(property.floatValue, value))
            return false;

        property.floatValue = value;
        return true;
    }

    private static bool SetLengths(SerializedObject data, string field, List<AnimationClip> clips)
    {
        SerializedProperty property = data.FindProperty(field);
        if (property == null)
            return false;

        bool dirty = false;
        if (property.arraySize != clips.Count)
        {
            property.arraySize = clips.Count;
            dirty = true;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (!Mathf.Approximately(element.floatValue, clips[i].length))
            {
                element.floatValue = clips[i].length;
                dirty = true;
            }
        }

        return dirty;
    }

    // ---------- Red ----------

    // Agrega el prefab a la lista DefaultNetworkPrefabs si todavia no esta en ninguna.
    private static void RegisterNetworkPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            return;

        NetworkPrefabsList defaultList = null;
        foreach (string guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(guid));
            if (list == null)
                continue;
            if (list.Contains(prefab))
                return;
            if (list.name == "DefaultNetworkPrefabs")
                defaultList = list;
        }

        if (defaultList == null)
        {
            Debug.LogWarning("MiniBossBuilder: agrega " + PrefabPath + " a tu lista de NetworkPrefabs a mano.");
            return;
        }

        defaultList.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(defaultList);
        AssetDatabase.SaveAssets();
        Debug.Log("MiniBossBuilder: MiniBoss agregado a DefaultNetworkPrefabs.");
    }
}

public class MiniBossPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        // Tambien al borrar o mover un clip: el controller y las variantes se actualizan solos.
        foreach (string path in importedAssets.Concat(deletedAssets).Concat(movedFromAssetPaths).Concat(movedAssets))
        {
            if (MiniBossBuilder.IsMiniBossPath(path))
            {
                EditorApplication.delayCall += MiniBossBuilder.Setup;
                return;
            }
        }
    }
}
