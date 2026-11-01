using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Deja Idle, Walk, Run, Attack y Hit en el lugar y arma el controller de los enemigos.
// Los grupos con mas de un clip (Walk, Attack, Hit) tienen un parametro "xxxIndex" para elegir la variante.
[InitializeOnLoad]
public static class EnemyLocomotionImporter
{
    private const string PrefabPath = "Assets/Prefabs/Enemy/Enemy.prefab";
    private const string ControllerPath = "Assets/Models/Enemy/EnemyLocomotion.controller";
    private const string Dir = "Assets/Models/Enemy/";

    // Momento del golpe en los clips de ataque que no tengan el evento (0 a 1 del clip). Ajustalo por clip si hace falta.
    private const string AttackEventName = "OnAttackHit";
    private const float AttackEventTime = 0.5f;

    private class Group
    {
        public string name;          // Prefijo del nombre de estado: Attack, Attack2, Attack3...
        public int stateValue;       // Valor del parametro "state" que activa el grupo
        public string indexParam;    // Parametro Int que elige la variante (null si hay un solo clip)
        public bool loop;            // Los golpes no hacen loop
        public bool optional;        // Si no hay archivo, el grupo se omite en vez de cancelar todo
        public string[] prefixes;    // Todo FBX de Assets/Models/Enemy cuyo nombre empiece asi entra al grupo
    }

    // Los clips se buscan por prefijo del nombre: para agregar o sacar una variante alcanza con
    // dejar o borrar el FBX en la carpeta, sin tocar este script.
    private static readonly Group[] Groups =
    {
        new Group { name = "Idle", stateValue = 0, indexParam = null, loop = true,
            prefixes = new[] { "Zombie Idle" } },

        new Group { name = "Walk", stateValue = 1, indexParam = "walkIndex", loop = true,
            prefixes = new[] { "Zombie Walk" } },

        new Group { name = "Run", stateValue = 2, indexParam = null, loop = true,
            prefixes = new[] { "Zombie Run" } },

        new Group { name = "Attack", stateValue = 3, indexParam = "attackIndex", loop = true,
            prefixes = new[] { "Zombie Attack", "ZombieAttack", "Zombie Kicking Attack" } },

        new Group { name = "Bite", stateValue = 5, indexParam = null, loop = true, optional = true,
            prefixes = new[] { "Zombie Neck Bite", "Zombie Bite" } },

        new Group { name = "Hit", stateValue = 4, indexParam = "hitIndex", loop = false,
            prefixes = new[] { "Zombie Reaction Hit" } }
    };

    private static bool Matches(Group group, string path)
    {
        if (path == null || !path.StartsWith(Dir) || !path.EndsWith(".fbx"))
            return false;

        string file = System.IO.Path.GetFileNameWithoutExtension(path);
        foreach (string prefix in group.prefixes)
        {
            if (file.StartsWith(prefix))
                return true;
        }

        return false;
    }

    // Los FBX del grupo, el de nombre mas corto primero (Zombie Attack antes que Zombie Attack 1).
    private static List<string> PathsOf(Group group)
    {
        List<string> result = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Dir.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Matches(group, path))
                result.Add(path);
        }

        return result
            .OrderBy(path => path.Length)
            .ThenBy(path => path, System.StringComparer.Ordinal)
            .ToList();
    }

    static EnemyLocomotionImporter()
    {
        EditorApplication.delayCall += Setup;
    }

    public static bool IsLocomotionPath(string path)
    {
        foreach (Group group in Groups)
        {
            if (Matches(group, path))
                return true;
        }

        return false;
    }

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        bool reimported = false;
        foreach (Group group in Groups)
        {
            foreach (string path in PathsOf(group))
                reimported |= BakeClipInPlace(path, group.loop, group.name == "Attack" || group.name == "Bite");
        }

        if (reimported)
            return;

        // Se usan los clips que existan; el primero de cada grupo es obligatorio.
        Dictionary<Group, List<AnimationClip>> found = new Dictionary<Group, List<AnimationClip>>();
        foreach (Group group in Groups)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            foreach (string path in PathsOf(group))
            {
                AnimationClip clip = LoadClip(path);
                if (clip != null)
                    clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                if (group.optional)
                    continue;
                return;
            }

            found[group] = clips;
        }

        AnimatorController controller = EnsureController(found);
        AssignController(controller, found);
    }

    private static bool BakeClipInPlace(string path, bool loop, bool needsAttackEvent)
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
            // El avance en XZ no se hornea en la pose: si queda adentro, el bucle teletransporta el cuerpo.
            if (clip.keepOriginalPositionY && !clip.keepOriginalPositionXZ && clip.keepOriginalOrientation
                && clip.lockRootHeightY && !clip.lockRootPositionXZ && clip.lockRootRotation
                && clip.loopTime == loop && !clip.heightFromFeet
                && (!needsAttackEvent || HasAttackEvent(clip)))
                continue;

            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = false;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = false;
            clip.lockRootRotation = true;
            clip.loopTime = loop;
            clip.heightFromFeet = false;

            // EnemyAttackEventRelay.OnAttackHit hace el dano y el sonido. Si el clip no lo trae, se agrega.
            if (needsAttackEvent && !HasAttackEvent(clip))
            {
                List<AnimationEvent> events = new List<AnimationEvent>(clip.events ?? new AnimationEvent[0]);
                events.Add(new AnimationEvent { functionName = AttackEventName, time = AttackEventTime });
                clip.events = events.ToArray();
            }

            clips[i] = clip;
            changed = true;
        }

        if (!changed)
            return false;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }

    private static bool HasAttackEvent(ModelImporterClipAnimation clip)
    {
        if (clip.events == null)
            return false;

        foreach (AnimationEvent e in clip.events)
        {
            if (e.functionName == AttackEventName)
                return true;
        }

        return false;
    }

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
    }

    // Idle, Walk, Walk2, Run, Attack, Attack2... Hit, Hit2
    private static string StateName(Group group, int index)
    {
        return index == 0 ? group.name : group.name + (index + 1);
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

                bool isDefault = group.stateValue == 0 && i == 0;
                AnimatorState state = FindOrAdd(machine, stateName, clips[i], isDefault);

                int variantIndex = clips.Count > 1 && group.indexParam != null ? i : -1;
                AddAnyStateTransition(machine, state, group.stateValue, group.indexParam, variantIndex);
            }
        }

        RemoveExtraStates(machine, expected);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
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

    private static void AddAnyStateTransition(
        AnimatorStateMachine machine,
        AnimatorState state,
        int stateValue,
        string indexParam,
        int variantIndex)
    {
        AnimatorStateTransition created = machine.AddAnyStateTransition(state);
        created.AddCondition(AnimatorConditionMode.Equals, stateValue, "state");
        if (variantIndex >= 0)
            created.AddCondition(AnimatorConditionMode.Equals, variantIndex, indexParam);

        created.duration = 0.1f;
        created.hasExitTime = false;
        created.canTransitionToSelf = false;
    }

    private static void RemoveExtraStates(AnimatorStateMachine machine, HashSet<string> expected)
    {
        List<AnimatorState> extras = new List<AnimatorState>();
        foreach (ChildAnimatorState child in machine.states)
        {
            if (!expected.Contains(child.state.name))
                extras.Add(child.state);
        }

        foreach (AnimatorState extra in extras)
            machine.RemoveState(extra);
    }

    private static void AssignController(AnimatorController controller, Dictionary<Group, List<AnimationClip>> found)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            EnemyAnimation animation = root.GetComponent<EnemyAnimation>();
            if (animation == null)
                animation = root.AddComponent<EnemyAnimation>();

            SerializedObject serialized = new SerializedObject(animation);
            bool dirty = false;

            SerializedProperty locomotion = serialized.FindProperty("locomotion");
            if (locomotion != null && locomotion.objectReferenceValue != controller)
            {
                locomotion.objectReferenceValue = controller;
                dirty = true;
            }

            foreach (Group group in Groups)
            {
                if (group.indexParam == null)
                    continue;

                // walkIndex -> walkVariants, attackIndex -> attackVariants, hitIndex -> hitVariants
                string field = group.name.ToLowerInvariant() + "Variants";
                SerializedProperty variants = serialized.FindProperty(field);
                int count = found[group].Count;

                // Duracion de cada clip (attackLengths, hitLengths): el server la usa para no cortar animaciones.
                SerializedProperty lengths = serialized.FindProperty(group.name.ToLowerInvariant() + "Lengths");
                if (lengths != null)
                {
                    List<AnimationClip> groupClips = found[group];
                    if (lengths.arraySize != groupClips.Count)
                    {
                        lengths.arraySize = groupClips.Count;
                        dirty = true;
                    }

                    for (int i = 0; i < groupClips.Count; i++)
                    {
                        SerializedProperty element = lengths.GetArrayElementAtIndex(i);
                        if (!Mathf.Approximately(element.floatValue, groupClips[i].length))
                        {
                            element.floatValue = groupClips[i].length;
                            dirty = true;
                        }
                    }
                }
                if (variants != null && variants.intValue != count)
                {
                    variants.intValue = count;
                    dirty = true;
                }
            }

            // Duracion de la mordida (0 si no hay clip: el zombie nunca muerde).
            float biteLength = 0f;
            foreach (Group group in Groups)
            {
                if (group.name == "Bite" && found.TryGetValue(group, out List<AnimationClip> biteClips) && biteClips.Count > 0)
                    biteLength = biteClips[0].length;
            }

            SerializedProperty biteProperty = serialized.FindProperty("biteLength");
            if (biteProperty != null && !Mathf.Approximately(biteProperty.floatValue, biteLength))
            {
                biteProperty.floatValue = biteLength;
                dirty = true;
            }

            if (!dirty)
                return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("EnemyLocomotionImporter: controller y variantes asignados a los enemigos.");
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
        // Tambien al borrar o mover un clip: asi el controller y las variantes se actualizan solos.
        foreach (string path in importedAssets)
        {
            if (path != null && EnemyLocomotionImporter.IsLocomotionPath(path))
            {
                EditorApplication.delayCall += EnemyLocomotionImporter.Setup;
                return;
            }
        }

        foreach (string path in deletedAssets)
        {
            if (path != null && EnemyLocomotionImporter.IsLocomotionPath(path))
            {
                EditorApplication.delayCall += EnemyLocomotionImporter.Setup;
                return;
            }
        }

        foreach (string path in movedFromAssetPaths)
        {
            if (path != null && EnemyLocomotionImporter.IsLocomotionPath(path))
            {
                EditorApplication.delayCall += EnemyLocomotionImporter.Setup;
                return;
            }
        }
    }
}