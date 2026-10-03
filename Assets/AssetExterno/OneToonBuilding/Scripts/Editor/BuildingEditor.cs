using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace ToonTown
{
    [CustomEditor(typeof(Building))]


    public class BuildingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            Building myPrefabMaker = (Building)target;

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("Randomize", GUILayout.Width(100), GUILayout.Height(60))) myPrefabMaker.Randomize();
            if (GUILayout.Button("Random\nmaterials", GUILayout.Width(100), GUILayout.Height(60))) myPrefabMaker.RandomMaterials();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("Placeholder", GUILayout.Width(100), GUILayout.Height(60))) myPrefabMaker.Placeholder();
            if (GUILayout.Button("Reset", GUILayout.Width(100), GUILayout.Height(60))) myPrefabMaker.FreshBuild();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("StandAloneONOFF", GUILayout.Width(100), GUILayout.Height(60))) myPrefabMaker.StandAloneONOFF();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");
            if (GUILayout.Button("Add\nFloor", GUILayout.Width(60), GUILayout.Height(60))) myPrefabMaker.AddFloor();
            EditorGUILayout.LabelField("   FLOORS  " + (myPrefabMaker.BuildingID[0]), GUILayout.Width(60), GUILayout.Height(40));
            if (GUILayout.Button("Remove\nFloor", GUILayout.Width(60), GUILayout.Height(60))) myPrefabMaker.RemoveFloor();
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("BacksON", GUILayout.Width(80), GUILayout.Height(80))) myPrefabMaker.BackYard(1);
            if (GUILayout.Button("BacksOFF", GUILayout.Width(80), GUILayout.Height(80))) myPrefabMaker.BackYard(0);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("Dawn", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.DayHour(myPrefabMaker.transform, 0, false);
            if (GUILayout.Button("Day", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.DayHour(myPrefabMaker.transform, 1, false);
            if (GUILayout.Button("Sunset", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.DayHour(myPrefabMaker.transform, 2, false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("Night", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.DayHour(myPrefabMaker.transform, 3, false);
            if (GUILayout.Button("LateNight", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.DayHour(myPrefabMaker.transform, 4, false);
            if (GUILayout.Button("DarkNight", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.DayHour(myPrefabMaker.transform, 5, false);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("OpenShops", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.OpenShops();
            if (GUILayout.Button("CloseShops", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.CloseShops();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("road OFF", GUILayout.Width(80), GUILayout.Height(30))) myPrefabMaker.Roads(0);
            if (GUILayout.Button("road ON", GUILayout.Width(80), GUILayout.Height(30))) myPrefabMaker.Roads(1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("simple top", GUILayout.Width(80), GUILayout.Height(30))) myPrefabMaker.SimplifyTop();
            if (GUILayout.Button("simplify", GUILayout.Width(80), GUILayout.Height(30))) myPrefabMaker.Simplify();
            GUILayout.EndHorizontal();


            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(myPrefabMaker);
        }
    }
}
        
