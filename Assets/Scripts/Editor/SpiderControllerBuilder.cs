#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SpiderControllerBuilder
{
    private const string Folder = "Assets/Spiders/Animations";

    [MenuItem("Tools/Enemigos/Armar Spider Controller")]
    public static void Build()
    {
        AnimationClip idle = Find("Idle");
        AnimationClip walk = Find("Walk");
        AnimationClip attack1 = Find("Attack1");
        AnimationClip attack2 = Find("Attack2");
        AnimationClip hit = Find("TakeDamage");

        if (idle == null || walk == null || attack1 == null || attack2 == null || hit == null)
        {
            Debug.LogError("[SpiderControllerBuilder] Falta algun clip en " + Folder);
            return;
        }

        string path = Folder + "/SpiderLocomotion.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Mismos parametros que escribe EnemyAnimation.
        c.AddParameter("state", AnimatorControllerParameterType.Int);
        c.AddParameter("walkIndex", AnimatorControllerParameterType.Int);
        c.AddParameter("attackIndex", AnimatorControllerParameterType.Int);
        c.AddParameter("hitIndex", AnimatorControllerParameterType.Int);

        AnimatorStateMachine sm = c.layers[0].stateMachine;

        AnimatorState sIdle = sm.AddState("Idle"); sIdle.motion = idle;
        AnimatorState sWalk = sm.AddState("Walk"); sWalk.motion = walk;
        AnimatorState sAtk1 = sm.AddState("Attack1"); sAtk1.motion = attack1;
        AnimatorState sAtk2 = sm.AddState("Attack2"); sAtk2.motion = attack2;
        AnimatorState sHit = sm.AddState("TakeDamage"); sHit.motion = hit;
        sm.defaultState = sIdle;

        // state: 0 Idle, 1 Walk, 2 Run, 3 Attack, 4 Hit, 5 Bite
        FromAny(sm, sIdle, ("state", AnimatorConditionMode.Equals, 0));
        FromAny(sm, sWalk, ("state", AnimatorConditionMode.Greater, 0), ("state", AnimatorConditionMode.Less, 3));
        FromAny(sm, sAtk1, ("state", AnimatorConditionMode.Equals, 3), ("attackIndex", AnimatorConditionMode.Equals, 0));
        FromAny(sm, sAtk2, ("state", AnimatorConditionMode.Equals, 3), ("attackIndex", AnimatorConditionMode.Equals, 1));
        FromAny(sm, sHit, ("state", AnimatorConditionMode.Equals, 4));

        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();

        Debug.Log("[SpiderControllerBuilder] Listo. Copia estos valores en EnemyAnimation:\n" +
                  "attackLengths = [" + attack1.length.ToString("F2") + ", " + attack2.length.ToString("F2") + "]\n" +
                  "hitLengths = [" + hit.length.ToString("F2") + "]\n" +
                  "Death dura " + Find("Death")?.length.ToString("F2") + " s");
    }

    private static void FromAny(AnimatorStateMachine sm, AnimatorState to,
        params (string param, AnimatorConditionMode mode, float value)[] conditions)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
        foreach (var cond in conditions)
            t.AddCondition(cond.mode, cond.value, cond.param);
    }

    private static AnimationClip Find(string clipName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Folder }))
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            {
                if (o is AnimationClip clip && clip.name == clipName)
                    return clip;
            }
        }
        return null;
    }
}
#endif