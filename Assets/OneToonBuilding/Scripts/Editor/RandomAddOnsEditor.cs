using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ToonTown
{

    [CustomEditor(typeof(RandomAddOns))]
    [ExecuteInEditMode]

    public class RandomAddOnsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RandomAddOns myPrefabMaker = (RandomAddOns)target;
            if (GUILayout.Button("RANDOM", GUILayout.Width(200), GUILayout.Height(200))) myPrefabMaker.RandomElements();
            if (GUILayout.Button("RANDOM TOP", GUILayout.Width(200), GUILayout.Height(50))) myPrefabMaker.RandomTop();
            if (GUILayout.Button("RANDOM FRONT", GUILayout.Width(200), GUILayout.Height(50))) myPrefabMaker.RandomFront();
            if (GUILayout.Button("RANDOM BACK", GUILayout.Width(200), GUILayout.Height(50))) myPrefabMaker.RandomBack();

            if (GUILayout.Button("CLEAN", GUILayout.Width(200), GUILayout.Height(40))) myPrefabMaker.CleanElements();
            if (GUILayout.Button("CLEAN TOP", GUILayout.Width(200), GUILayout.Height(50))) myPrefabMaker.CleanTop();
            if (GUILayout.Button("CLEAN FRONT", GUILayout.Width(200), GUILayout.Height(50))) myPrefabMaker.CleanFront();
            if (GUILayout.Button("CLEAN BACK", GUILayout.Width(200), GUILayout.Height(50))) myPrefabMaker.CleanBack();

            if (GUILayout.Button("DISPLAY ALL", GUILayout.Width(200), GUILayout.Height(50))) myPrefabMaker.UnhideALL();

        }
    }
}