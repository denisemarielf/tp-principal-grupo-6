using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace ToonTown
{
    [CustomEditor(typeof(MasterClock))]

    public class MasterClockEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            MasterClock myPrefabMaker = (MasterClock)target;

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("PrevHour", GUILayout.Width(100), GUILayout.Height(60))) myPrefabMaker.NextHour(-1);
            if (GUILayout.Button("NextHour", GUILayout.Width(100), GUILayout.Height(60))) myPrefabMaker.NextHour(1);
            GUILayout.EndHorizontal();            
            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("Dawn", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.ChangeHour(0);
            if (GUILayout.Button("Day", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.ChangeHour(1);
            if (GUILayout.Button("Sunset", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.ChangeHour(2);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("Night", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.ChangeHour(3);
            if (GUILayout.Button("LateNight", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.ChangeHour(4);
            if (GUILayout.Button("DarkNight", GUILayout.Width(80), GUILayout.Height(40))) myPrefabMaker.ChangeHour(5);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            if (GUILayout.Button("City ONOFF", GUILayout.Width(100), GUILayout.Height(40))) myPrefabMaker.CityONOFF();


            GUILayout.BeginHorizontal("box");
            //if (GUILayout.Button("Reset all", GUILayout.Width(100), GUILayout.Height(40))) myPrefabMaker.ResetAll();
            if (GUILayout.Button("Randomize", GUILayout.Width(100), GUILayout.Height(40))) myPrefabMaker.RandomizeAll();
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical("box");
            if (GUILayout.Button("Add floors", GUILayout.Width(100), GUILayout.Height(40))) myPrefabMaker.FloorN(1);
            if (GUILayout.Button("Remove floors", GUILayout.Width(100), GUILayout.Height(40))) myPrefabMaker.FloorN(-1);
            GUILayout.EndVertical();

            

            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button("-", GUILayout.Width(40), GUILayout.Height(40))) myPrefabMaker.ChangeFogFarRange(-1);
            EditorGUILayout.LabelField("   Fog far range  " , GUILayout.Width(100), GUILayout.Height(40));
            if (GUILayout.Button("+", GUILayout.Width(40), GUILayout.Height(40))) myPrefabMaker.ChangeFogFarRange(1);


            GUILayout.EndHorizontal();


            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(myPrefabMaker);
        }
    }
}
