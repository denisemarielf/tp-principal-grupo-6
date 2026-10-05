using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



namespace ToonTown
{
    //[SelectionBase]
    public class Building : MonoBehaviour
    {
        //[HideInInspector] public int FloorN;
        public int FloorLimit;
        [Header("   -GameObjects-")]
        public GameObject[] FloorsPrefabs;
        public float[] FloorsHight;
        //public GameObject FloorsStandAlone;
        public float StandAloneFloorHight;
        public GameObject Road;
        public GameObject SARoad;
        //public GameObject[] ExtraRoads;
        GameObject GORoad;
        GameObject[] Fs;
        //GameObject GroundFloorActive;
        GameObject AddOns;
        [Header("   -Materials-")]
        public Material[] MatWalls;
        public Material[] MatConcretes;
        public Material[] MatDoorsWindows;
        public Material[] MatDoorsWindowsNight;
        public Material[] MatDoors;
        public Material[] MatDoorsNight;
        //[HideInInspector]
        public Material[] DayShops;
        public Material[] NightShops;
        public Material[] ClosedShops;
        public Material[] DayShops2;
        public Material[] NightShops2;
        public Material[] ClosedShops2;
        public Material[] DayMaterials;
        public Material[] NightMaterials;
        public Material[] RandomGenericMaterials;

        //bool Editor = true;
        [HideInInspector]
        public int[] BuildingID;
        // 0 FloorN             BuildingID[0]
        // 1 BackON             BuildingID[1]
        // 2 Hour DayTime       BuildingID[2]
        // 3 StandAlone NEW     BuildingID[3]
        // 4 RoadON     NEW     0 no, 1 road
        // 5 Placeholder        0 no, 1 placeholder
        // 6 Populated          0 no, 1 yes
        // 7 Traffic            0 no, 1 yes

        [HideInInspector]
        public int[] BBP;
        // 0 floor 0
        // 1 floor 1
        // 2 floor 2
        // 3 floor 3
        // 4 floor 4
        // 5 floor 1big
        // 6 floor 3big
        // 7 floor 0 standalone
        // >10 empty

        [HideInInspector]
        public int[] MatsID;
        // 0 material wall index
        // 1 material concrete index
        // 2 material doorwindows index
        // 3 material shop/restaurant
        // 4 material shop2


        // tools
        void GetReady()
        {
            if (transform.childCount > 1)
            {

                if (Fs == null)
                {
                    //Debug.Log("Get ready");
                    Fs = new GameObject[BuildingID[0]];
                    for (int forAUX = 0; forAUX < BuildingID[0]; forAUX++)
                    {
                        Fs[forAUX] = transform.GetChild(forAUX + 2).gameObject;
                    }
                    transform.GetChild(0).gameObject.SetActive(false);
                }

                AddOns = transform.GetChild(1).gameObject;
                if (transform.GetChild(1).childCount > 0)
                {
                    GORoad = transform.GetChild(1).transform.GetChild(0).gameObject;
                    GORoad.transform.localScale = transform.lossyScale;
                }
            }
            else FreshBuild();
        }

        void CleanChildren(int leftbehind)
        {
            while (transform.childCount > leftbehind) DestroyImmediate(transform.GetChild(leftbehind).gameObject);
        }

        void FixAddOns()
        {
            for (int forAUX = 0; forAUX < BuildingID[0]; forAUX++)
            {
                if (Fs[forAUX].TryGetComponent(out RandomAddOns comp))
                {
                    comp.RandomFront();
                    if (BuildingID[3] == 0)
                    {
                        if (BuildingID[1] == 0)
                            comp.CleanBack();
                        else comp.RandomBack();
                    }
                    else comp.CleanBack();
                }
            }
        }
        void MakeBluePrint()
        {
            BBP = new int[BuildingID[0]];
            //Debug.Log("altura " + BBP.Length);
            for (int forAUX = 0; forAUX < BuildingID[0] - 1; forAUX++)
                BBP[forAUX] = 1;
            for (int forAUX = (int)Mathf.Floor(BuildingID[0] * 0.5f); forAUX < BuildingID[0] - 1; forAUX++)
                BBP[forAUX] = 3;
            BBP[BuildingID[0] - 1] = 4;
            BBP[(int)Mathf.Floor(BuildingID[0] * 0.5f)] = 2;
            if (BuildingID[3] == 0) BBP[0] = 0;
            else BBP[0] = 7;

            if (BuildingID[0] == 3) { BBP[1] = 1; BBP[2] = 2; }
            if (BuildingID[0] == 4) { BBP[1] = 1; BBP[2] = 1; BBP[3] = 2; }


            //optimization
            if (BuildingID[0] > 9)
            {
                int newfloorsA = (int)((Mathf.Floor(BuildingID[0] - 2) * 0.5f / 4f));
                int newfloorsB = (int)((Mathf.Floor(BuildingID[0] - 3) * 0.5f) / 4f);

                if (newfloorsA > 0)
                    for (int forAUX = 0; forAUX < newfloorsA; forAUX++)
                    {
                        BBP[1 + (forAUX * 4)] = 5;
                        BBP[2 + (forAUX * 4)] = 50;
                        BBP[3 + (forAUX * 4)] = 50;
                        BBP[4 + (forAUX * 4)] = 50;
                    }

                if (newfloorsB > 0)
                    for (int forAUX = 0; forAUX < newfloorsB; forAUX++)
                    {
                        BBP[(int)(Mathf.Floor(BuildingID[0] * 0.5f) + 1 + (forAUX * 4))] = 6;
                        BBP[(int)(Mathf.Floor(BuildingID[0] * 0.5f) + 2 + (forAUX * 4))] = 60;
                        BBP[(int)(Mathf.Floor(BuildingID[0] * 0.5f) + 3 + (forAUX * 4))] = 60;
                        BBP[(int)(Mathf.Floor(BuildingID[0] * 0.5f) + 4 + (forAUX * 4))] = 60;
                    }
            }
        }

        // models
        public void FreshBuild()
        {
            CleanChildren(1);
            transform.GetChild(0).gameObject.SetActive(false);
            AddOns = new GameObject("AddOns");
            AddOns.transform.position = transform.position;
            AddOns.transform.rotation = transform.rotation;
            AddOns.transform.parent = transform;
            AddOns.transform.localScale = transform.localScale;
            MatsID = new int[5] { 0, 0, 0, 0, 0 };
            BuildingID[0] = Mathf.Clamp(BuildingID[0], 1, FloorLimit);
            BuildBbuilding();
            FixAddOns();
        }
        public void BuildBbuilding()
        {
            MakeBluePrint();
            Fs = new GameObject[BuildingID[0]];
            int init = 0;
            if (transform.childCount > 2)
                if (transform.GetChild(2).TryGetComponent(out RandomAddOns com))
                {
                    CleanChildren(3);
                    init = 1;
                    Fs[0] = transform.GetChild(2).gameObject;
                    if (BuildingID[0] > 1)
                        if (Fs[0].TryGetComponent(out RandomAddOns comp))
                            comp.CleanTop();
                }
                else CleanChildren(2);
            for (int forAUX = init; forAUX < BuildingID[0]; forAUX++)
            {
                if (BBP[forAUX] < 10)
                {
                    if (Application.isPlaying)
                        Fs[forAUX] = Instantiate(FloorsPrefabs[BBP[forAUX]], transform);
                    else
#if UNITY_EDITOR
                        Fs[forAUX] = (GameObject)PrefabUtility.InstantiatePrefab(FloorsPrefabs[BBP[forAUX]], transform);
#endif
                    Fs[forAUX].transform.parent = transform;
                    Fs[forAUX].transform.position = transform.position;
                    Fs[forAUX].name = NoClone(Fs[forAUX].name);
                    if (Fs[forAUX].GetComponent<RandomAddOns>() != null)
                    {
                        Fs[forAUX].GetComponent<RandomAddOns>().RandomElements();
                        Fs[forAUX].GetComponent<RandomAddOns>().CleanTop();
                    }
                }
                else
                {
                    Fs[forAUX] = new GameObject("emptyfloor");
                    Fs[forAUX].transform.parent = transform;
                    Fs[forAUX].transform.position = transform.position;
                }
            }
            if (Fs[BBP.Length - 1].GetComponent<RandomAddOns>() != null)
                Fs[BBP.Length - 1].GetComponent<RandomAddOns>().RandomTop();

            float[] tempheights = new float[8] { FloorsHight[0], FloorsHight[1], FloorsHight[2], FloorsHight[3], 0f, FloorsHight[1] * 4, FloorsHight[3] * 4, StandAloneFloorHight };
            float temp = 0f;
            //Recolocate
            for (int forAUX = 0; forAUX < BBP.Length; forAUX++)
            {
                if (BBP[forAUX] < 50)
                {
                    Fs[forAUX].transform.position = transform.position + Vector3.up * temp * transform.lossyScale.y;// + transform.right * temp * 0.125f;
                    temp += tempheights[BBP[forAUX]];
                }
            }
            //ResetProps();
            DefineMatsID(0, MatWalls);
            DefineMatsID(1, MatConcretes);
            DefineMatsID(2, MatDoorsWindows);
            DefineMatsID(3, DayShops);
            DefineMatsID(4, DayShops2);
            BackYard(BuildingID[1]);
            ForceRoads(BuildingID[4]);
            UnifyMaterials();

            DayHour(transform, BuildingID[2], true);

            
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
        public void AddFloor()
        {
            if (BuildingID[0] < FloorLimit)
            {
                BuildingID[0]++;
                BuildBbuilding();
                FixSABackYard();
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }
        public void RemoveFloor()
        {
            if (BuildingID[0] > 1)
            {
                BuildingID[0]--;
                BuildBbuilding();
                FixSABackYard();
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }
        public void SetFloorsN(int hm)
        {
            if (hm < BuildingID[0])
            {
                for (int forAUX = 0; forAUX < (BuildingID[0] - hm); forAUX++)
                    RemoveFloor();
            }
            else
            {
                if (hm > BuildingID[0])
                    for (int forAUX = 0; forAUX < (hm - BuildingID[0]); forAUX++)
                        AddFloor();
            }
        }
        public void Placeholder()
        {
            CleanChildren(1);
            transform.GetChild(0).gameObject.SetActive(true);
        }
        public void Randomize()
        {
            GetReady();
            for (int forAUX = 0; forAUX < BuildingID[0]; forAUX++)
                if (Fs[forAUX].TryGetComponent(out RandomAddOns comp))
                {
                    comp.RandomFront();
                    if (BuildingID[1] == 1)
                        comp.RandomBack();
                }
            if (Fs[BuildingID[0] - 1].GetComponent<RandomAddOns>() != null)
                Fs[BuildingID[0] - 1].GetComponent<RandomAddOns>().RandomTop();

            if (BuildingID[4] == 1)
                if (GORoad.TryGetComponent(out RandomAddOns compR))
                    compR.RandomElements();

            ResetProps();
            Props();
            SetShops();
            UnifyMaterials();
            RandomMaterials();
            if (BuildingID[2] > 2)
            {
                InterchangeMats(DayMaterials, NightMaterials);
                InterchangeMats(MatDoors, MatDoorsNight);
            }
            else
            {
                InterchangeMats(NightMaterials, DayMaterials);
                InterchangeMats(MatDoorsNight, MatDoors);
            }
            FixSABackYard();
          
        }
        public void RandomFloorN()
        {
            //BuildingID[0] *= Random.Range(-2, 2);
            BuildingID[0] += Random.Range(4, -4);
            BuildingID[0] = Mathf.Clamp(BuildingID[0], 1, FloorLimit);
            BuildBbuilding();
        }
        public void BackYard(int ONOFF)
        {
            if (BuildingID[3] == 0)
            {
                GetReady();
                for (int forAUX = 0; forAUX < BuildingID[0]; forAUX++)
                    if (Fs[forAUX].TryGetComponent(out RandomAddOns comp))
                    {
                        if (ONOFF == 1)
                        {
                            comp.RandomBack();
                            if (BuildingID[0] == 1) comp.RandomTop();
                            UnifyMaterialsOneFloor(Fs[forAUX].transform);
                        }
                        else Fs[forAUX].GetComponent<RandomAddOns>().CleanBack();
                    }
                BuildingID[1] = ONOFF;
            }
        }
        void FixSABackYard()
        {
            if (BuildingID[3] == 1)
            {
                for (int forAUX = 0; forAUX < BuildingID[0]; forAUX++)
                    if (Fs[forAUX].TryGetComponent(out RandomAddOns comp))
                        Fs[forAUX].GetComponent<RandomAddOns>().CleanBack();
            }
        }
        void ResetProps()
        {
            foreach (Propsbyhour child in transform.GetComponentsInChildren<Propsbyhour>())
            {
                foreach (Transform child2 in child.transform)
                    child2.gameObject.SetActive(true);
            }
        }
        public void Props()
        {
            foreach (Propsbyhour comp in transform.GetComponentsInChildren<Propsbyhour>())
            {
                foreach (Transform child2 in comp.transform)
                    child2.gameObject.SetActive(true);
                if (!comp.CheckActive(BuildingID[2]))
                    foreach (Transform child2 in comp.transform)
                        child2.gameObject.SetActive(false);
            }
        }
        public void Roads(int todo)
        {
            GetReady();
            if (todo == 1)
            {
                if (BuildingID[4] == 0)
                {
                    if (BuildingID[3] == 0)
                    {
                        //GORoad = Instantiate(Road, AddOns.transform);

                        if (Application.isPlaying)
                            GORoad = Instantiate(Road, AddOns.transform);
                        else
#if UNITY_EDITOR
                            GORoad = (GameObject)PrefabUtility.InstantiatePrefab(Road, AddOns.transform);
#endif

                        GORoad.name = NoClone(GORoad.name);
                        if (GORoad.TryGetComponent(out RandomAddOns comp))
                            comp.RandomFront();
                        StreetLights(false);
                        if (BuildingID[2] == 3 || BuildingID[2] == 4 || BuildingID[2] == 5)
                            StreetLights(true);
                       
                    }
                    else
                    {
                        if (Application.isPlaying)
                            GORoad = Instantiate(SARoad, AddOns.transform);
                        else
                        {
#if UNITY_EDITOR

                            GORoad = (GameObject)PrefabUtility.InstantiatePrefab(SARoad, AddOns.transform);
#endif
                        }
                    }
                }
                BuildingID[4] = 1;
                GORoad.transform.localScale = transform.localScale;
            }
            if (todo == 0)
            {
                if (BuildingID[4] != 0)
                {
                    DestroyImmediate(GORoad);
                }
                BuildingID[4] = 0;
            }
        }
        public void ForceRoads(int todo)
        {
            //GetReady();
            if (BuildingID[4] == 1) DestroyImmediate(GORoad);
            BuildingID[4] = 0;
            if (todo == 1)
            {
                if (BuildingID[3] == 0)
                {
                    //GORoad = Instantiate(Road, AddOns.transform);
                    if (Application.isPlaying)
                        GORoad = Instantiate(Road, AddOns.transform);
                    else
#if UNITY_EDITOR
                        GORoad = (GameObject)PrefabUtility.InstantiatePrefab(Road, AddOns.transform);
#endif
                    GORoad.name = NoClone(GORoad.name);
                    if (GORoad.TryGetComponent(out RandomAddOns comp))
                        comp.RandomFront();
                    //AutoStreetLights();
                }
                else
                //GORoad = Instantiate(SARoad, AddOns.transform);
                {
                    if (Application.isPlaying)
                        GORoad = Instantiate(SARoad, AddOns.transform);
                    else
                    {
#if UNITY_EDITOR

                        GORoad = (GameObject)PrefabUtility.InstantiatePrefab(SARoad, AddOns.transform);
#endif
                    }
                }
                BuildingID[4] = 1;
                //GORoad.transform.localScale = transform.lossyScale;
            }
        }

       
       
       
       

        // materials
        public void RandomMaterials()
        {
            //int temp = BuildingID[2];
            MatsID = new int[5] { Random.Range(0, MatWalls.Length), Random.Range(0, MatConcretes.Length), Random.Range(0, MatDoorsWindows.Length), Random.Range(0, DayShops.Length), Random.Range(0, DayShops2.Length) };

            if (MatWalls != null) ChangeMat(transform, 0, MatWalls);
            if (MatConcretes != null) ChangeMat(transform, 1, MatConcretes);
            if (MatDoors != null) ChangeMat(transform, 2, MatDoorsWindows);
            if (MatDoors != null) ChangeMat(transform, 2, MatDoorsWindowsNight);
            if (MatDoors != null) ChangeMat(transform, 2, MatDoors);
            if (MatDoors != null) ChangeMat(transform, 2, MatDoorsNight);

            if (DayShops != null) ChangeShop(DayShops);
            if (NightShops != null) ChangeShop(NightShops);
            if (DayShops2.Length > 0) ChangeShop(DayShops2);
            if (NightShops2.Length > 0) ChangeShop(NightShops2);

            RandomizeGenericMaterials();
        }
        void DefineMatsID(int IDindex, Material[] Mat)
        {
            MatsID[IDindex] = 0;
            bool found = false;
            Material[] matAUX;
            foreach (Renderer child in transform.GetChild(2).transform.GetComponentsInChildren<Renderer>())
            {
                matAUX = child.sharedMaterials;
                for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                {
                    for (int forAUX2 = Mat.Length - 1; forAUX2 > -1; forAUX2--)
                    {
                        if (matAUX[forAUX] == Mat[forAUX2] && !found)
                        {
                            MatsID[IDindex] = forAUX2;
                            found = true;
                        }
                    }
                }
            }
        }
        void ChangeMat(Transform GO, int MatsIDindex, Material[] Mat2change)
        {
            GetReady();
            Material[] matAUX;
            foreach (Renderer child in GO.GetComponentsInChildren<Renderer>())
            {
                matAUX = child.sharedMaterials;
                for (int forAUX = 0; forAUX < matAUX.Length; forAUX++)
                {
                    for (int forAUX1 = 0; forAUX1 < Mat2change.Length; forAUX1++)
                    {
                        if (matAUX[forAUX] == Mat2change[forAUX1])
                            matAUX[forAUX] = Mat2change[MatsID[MatsIDindex]];
                    }
                }
                child.sharedMaterials = matAUX;
            }
        }
        void ChangeShop(Material[] Mat2Change)
        {
            //GetReady();
            //int matN = Mat2Change.Length;
            bool found = false;
            int index = 0;
            int newindex = 0;
            Material[] matAUX;
            foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
            {
                matAUX = child.sharedMaterials;
                for (int forAUX = 0; forAUX < matAUX.Length; forAUX++)
                {
                    for (int forAUX1 = 0; forAUX1 < Mat2Change.Length; forAUX1++)
                    {
                        if (matAUX[forAUX] == Mat2Change[forAUX1])
                        {
                            if (!found) index = forAUX1;
                            found = true;
                        }
                    }
                }
            }

            int coin = Random.Range(0, 10);
            if (coin > 3)
            {
                if (coin > 6) newindex = index + 1;
                else newindex = index - 1;
            }
            if (newindex > Mat2Change.Length - 1) newindex = 0;
            if (newindex < 0) newindex = Mat2Change.Length - 1;
            foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
            {
                matAUX = child.sharedMaterials;
                for (int forAUX = 0; forAUX < matAUX.Length; forAUX++)
                {
                    for (int forAUX1 = 0; forAUX1 < Mat2Change.Length; forAUX1++)
                    {
                        if (matAUX[forAUX] == Mat2Change[forAUX1])
                        {
                            matAUX[forAUX] = Mat2Change[newindex];
                        }
                    }
                }
                child.sharedMaterials = matAUX;
            }
        }
        void InterchangeMats(Material[] Mat1, Material[] Mat2)
        {
            //GetReady();
            Material[] matAUX;
            bool foundone = false;
            foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
            {
                matAUX = child.sharedMaterials;
                for (int forAUX = 0; forAUX < matAUX.Length; forAUX++)
                {
                    for (int forAUX2 = 0; forAUX2 < Mat1.Length; forAUX2++)
                    {
                        if (matAUX[forAUX] == Mat1[forAUX2])
                        {
                            matAUX[forAUX] = Mat2[forAUX2];
                            foundone = true;
                        }
                    }
                }
                if (foundone) child.sharedMaterials = matAUX;
            }

        }
        void UnifyMaterials()
        {
            if (MatWalls != null) ChangeMat(transform, 0, MatWalls);
            if (MatConcretes != null) ChangeMat(transform, 1, MatConcretes);
            if (MatDoors != null) ChangeMat(transform, 2, MatDoorsWindows);
            if (DayShops != null) ChangeMat(transform, 3, DayShops);
            if (NightShops != null) ChangeMat(transform, 3, NightShops);
            if (ClosedShops != null) ChangeMat(transform, 3, ClosedShops);
            if (DayShops2 != null) ChangeMat(transform, 4, DayShops2);
            if (NightShops2 != null) ChangeMat(transform, 4, NightShops2);
            if (ClosedShops2 != null) ChangeMat(transform, 4, ClosedShops2);
        }
        void UnifyMaterialsOneFloor(Transform GO)
        {
            if (MatWalls != null) ChangeMat(GO, 0, MatWalls);
            if (MatConcretes != null) ChangeMat(GO, 1, MatConcretes);
            if (MatDoors != null) ChangeMat(GO, 2, MatDoorsWindows);
            if (DayShops != null) ChangeMat(GO, 3, DayShops);
            if (NightShops != null) ChangeMat(GO, 3, NightShops);
            if (ClosedShops != null) ChangeMat(GO, 3, ClosedShops);
            if (DayShops2 != null) ChangeMat(GO, 4, DayShops2);
            if (NightShops2 != null) ChangeMat(GO, 4, NightShops2);
            if (ClosedShops2 != null) ChangeMat(GO, 4, ClosedShops2);
        }
        void RandomizeGenericMaterials()
        {
            // solo mira el primer material, no vale para materiales compuestos  ARREGLAR¡¡¡¡¡¡¡¡¡¡¡¡¡¡¡¡¡¡¡
            if (transform.childCount > 2)
            {
                int coin = Random.Range(0, RandomGenericMaterials.Length);
                Material[] matAUX;
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX2 = 0; forAUX2 < matAUX.Length; forAUX2++)
                    {
                        for (int forAUX = 0; forAUX < RandomGenericMaterials.Length; forAUX++)
                        {
                            if (matAUX[forAUX2] == RandomGenericMaterials[forAUX])
                            {
                                matAUX[forAUX2] = RandomGenericMaterials[coin];
                                child.sharedMaterials = matAUX;
                            }
                        }
                    }
                }
                /*
                bool foundone = false;
                int coin = Random.Range(0, RandomGenericMaterials.Length);
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = 0; forAUX < RandomGenericMaterials.Length - 1; forAUX++)
                    {
                        if (matAUX[0] == RandomGenericMaterials[0])
                        {
                            matAUX[0] = RandomGenericMaterials[coin];
                            foundone = true;
                        }
                    }
                    if (foundone) child.sharedMaterials = matAUX;
                }
                */
            }
        }

        // stand alone or part of a block
        public void StandAloneONOFF()
        {
            GetReady();
            if (BuildingID[3] == 1) StanAloneOFF();
            else StanAloneON();
        }
        public void StanAloneON()
        {
            BuildingID[3] = 1;
            FreshBuild();
            FixSABackYard();
        }
        public void StanAloneOFF()
        {
            BuildingID[3] = 0;
            FreshBuild();
        }

        // day/night
        public void NightLights(Transform GO, int level) //level 0 - 100
        {
            GetReady();
            //windows
            //if (transform.childCount > 1)
            {
                Material[] matAUX;
                foreach (Renderer child in GO.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = MatDoorsWindowsNight.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == MatDoorsWindows[forAUX2])
                            {
                                if (Random.Range(0, level) < 10) matAUX[forAUX] = MatDoorsWindowsNight[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }

            //doors
            //if (transform.childCount > 0)
            {
                Material[] matAUX;
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = MatDoorsNight.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == MatDoors[forAUX2])
                            {
                                matAUX[forAUX] = MatDoorsNight[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }
            /*
            //shops and signs
            if (transform.childCount > 0)
            {
                Material[] matAUX;
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = DayShops.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == DayShops[forAUX2])
                            {
                                matAUX[forAUX] = NightShops[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }
            */
            //elements iluminated
            //if (transform.childCount > 0)
            {
                Material[] matAUX;
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = DayMaterials.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == DayMaterials[forAUX2])
                            {
                                matAUX[forAUX] = NightMaterials[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }

        }
        public void DayLights(Transform GO)
        {
            GetReady();
            //windows
            if (transform.childCount > 1)
            {
                Material[] matAUX;
                foreach (Renderer child in GO.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = MatDoorsWindows.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == MatDoorsWindowsNight[forAUX2])
                            {
                                matAUX[forAUX] = MatDoorsWindows[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }
            //doors
            //if (transform.childCount > 0)
            {
                Material[] matAUX;
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = MatDoors.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == MatDoorsNight[forAUX2])
                            {
                                matAUX[forAUX] = MatDoors[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }
            /*
            //shops and signs
            if (transform.childCount > 0)
            {
                Material[] matAUX;
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = NightShops.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == NightShops[forAUX2])
                            {
                                matAUX[forAUX] = DayShops[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }
            */
            //element materials iluminated
            //if (transform.childCount > 0)
            {
                Material[] matAUX;
                foreach (Renderer child in transform.GetComponentsInChildren<Renderer>())
                {
                    matAUX = child.sharedMaterials;
                    for (int forAUX = matAUX.Length - 1; forAUX > -1; forAUX--)
                    {
                        for (int forAUX2 = NightMaterials.Length - 1; forAUX2 > -1; forAUX2--)
                        {
                            if (matAUX[forAUX] == NightMaterials[forAUX2])
                            {
                                matAUX[forAUX] = DayMaterials[forAUX2];
                            }
                        }
                    }
                    child.sharedMaterials = matAUX;
                }
            }
            /*
            //elements night OFF
            Transform[] allChildren = transform.GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                if (child.name == "StreetLight") child.transform.GetChild(1).gameObject.SetActive(false);
            }
            */
        }
       
        void StreetLights(bool ONOFF)
        {
            GetReady();
            {
                if (GORoad != null)
                {
                    Transform[] allChildren = GORoad.transform.GetComponentsInChildren<Transform>();
                    foreach (Transform child in allChildren)
                    {
                        if (child.name == "StreetLight")
                        {
                            child.transform.GetChild(1).gameObject.SetActive(ONOFF);
                            //Debug.Log("ONOFF farola");
                        }
                    }
                }
            }
        }
        public void DayHour(Transform GO, int newhour, bool force)
        {
            // 0 dawn           streetlights OFF    shops dark  closed      windows 0
            // 1 day            streetlights OFF    shops dark  open        windows 0
            // 2 sunset         streetlights OFF    shops light open        windows 1
            // 3 night          streetlights ON     shops light open        windows 3
            // 4 latenight      streetlights ON     shops dark  closed      windows 2
            // 5 dark night     streetlights ON     shops dark  closed      windows 1
            GetReady();
            if (newhour != BuildingID[2] || force)
            {
                BuildingID[2] = newhour;
                {
                    ResetProps();
                    SetShops();
                    if (BuildingID[2] == 0)  // 0 dawn   
                    {
                        Props();
                        DayLights(GO);
                        StreetLights(false);
                        InterchangeMats(NightMaterials, DayMaterials);
                    }
                    else if (BuildingID[2] == 1) // 1 day  
                    {
                        Props();
                        //SetShops();
                        DayLights(GO);
                        StreetLights(false);
                        InterchangeMats(NightMaterials, DayMaterials);
                    }
                    else if (BuildingID[2] == 2) // 2 sunset   
                    {
                        Props();
                        //SetShops();
                        DayLights(GO);
                        StreetLights(true);
                        NightLights(GO, 150);
                        InterchangeMats(DayMaterials, NightMaterials);
                    }
                    else if (BuildingID[2] == 3) // 3 night 
                    {
                        Props();
                        //SetShops();
                        DayLights(GO);
                        StreetLights(true);
                        NightLights(GO, 20);
                        InterchangeMats(DayMaterials, NightMaterials);
                    }
                    else if (BuildingID[2] == 4) // 4 latenight 

                    {
                        Props();
                        //SetShops();
                        DayLights(GO);
                        StreetLights(true);
                        NightLights(GO, 50);
                        InterchangeMats(DayMaterials, NightMaterials);
                    }
                    else if (BuildingID[2] == 5) // 5 dark night
                    {
                        Props();
                        //SetShops();
                        DayLights(GO);
                        NightLights(GO, 100);
                        InterchangeMats(DayMaterials, NightMaterials);
                        StreetLights(true);
                        InterchangeMats(MatDoors, MatDoorsNight);
                    }
                }
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
        void SetShops()
        {
            //Debug.Log("hora " + BuildingID[2]);
            if (BuildingID[2] == 0 || BuildingID[2] > 3)
            {
                InterchangeMats(NightShops, ClosedShops);
                InterchangeMats(DayShops, ClosedShops);
                InterchangeMats(NightShops2, ClosedShops2);
                InterchangeMats(DayShops2, ClosedShops2);
            }
            else if (BuildingID[2] == 1)
            {
                InterchangeMats(ClosedShops, DayShops);
                InterchangeMats(NightShops, DayShops);
                InterchangeMats(ClosedShops2, DayShops2);
                InterchangeMats(NightShops2, DayShops2);
            }
            else if (BuildingID[2] == 2 || BuildingID[2] == 3)
            {
                InterchangeMats(DayShops, NightShops);
                InterchangeMats(ClosedShops, NightShops);
                InterchangeMats(DayShops2, NightShops2);
                InterchangeMats(ClosedShops2, NightShops2);
            }
        }
        public void ResetDay()
        {
            ResetProps();
            SetShops();
            DayLights(transform);
            InterchangeMats(NightMaterials, DayMaterials);
        }
        public void OpenShops()
        {
            DefineMatsID(3, ClosedShops);
            Props();
            UnifyMaterialsOneFloor(Fs[0].transform);
            if (BuildingID[2] == 1) { InterchangeMats(ClosedShops, DayShops); InterchangeMats(NightShops, DayShops); }
            if (BuildingID[2] == 2 || BuildingID[2] == 3) { InterchangeMats(DayShops, NightShops); InterchangeMats(ClosedShops, NightShops); }
        }
        public void CloseShops()
        {
            DefineMatsID(3, DayShops);
            DefineMatsID(3, NightShops);
            DefineMatsID(4, DayShops2);
            DefineMatsID(4, NightShops2);
            Props();
            UnifyMaterialsOneFloor(Fs[0].transform);
            InterchangeMats(NightShops, ClosedShops);
            InterchangeMats(DayShops, ClosedShops);
            InterchangeMats(NightShops2, ClosedShops2);
            InterchangeMats(DayShops2, ClosedShops2);
        }

        //simplify        
        public void CleanCopy()
        {
            GameObject NewBuiding = Instantiate(gameObject, transform.position, transform.rotation);
            GameObject temp = new GameObject();            

            Transform[] tempGO = NewBuiding.transform.GetComponentsInChildren<Transform>(includeInactive: true);

            foreach (Transform temptrans in tempGO)
            {
                if (!temptrans.gameObject.activeSelf)
                {
                    string[] tempnames = new string[6] { "EssentialA", "EssentialB", "OptionalA", "OptionalB", "OptionalC", "OptionalD" };
                    for (int intAUX = 0; intAUX < 6; intAUX++)
                        for (int intAUX2 = 0; intAUX2 < 6; intAUX2++)
                        {
                            if (temptrans.name == tempnames[intAUX] + "0" + intAUX2)
                                temptrans.name = "deleteme";
                        }
                }
            }

            // delete empty floors
            tempGO = NewBuiding.transform.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform temptrans in tempGO)
            {
                if (!temptrans.gameObject.TryGetComponent(out Renderer comp) && temptrans.childCount == 0 && !temptrans.gameObject.TryGetComponent(out Light comp2))
                    temptrans.name = "deleteme";
            }

            tempGO = NewBuiding.transform.GetComponentsInChildren<Transform>(includeInactive: false);
            foreach (Transform temptrans in tempGO)
            {
                if (!temptrans.gameObject.TryGetComponent(out Renderer comp) && !temptrans.gameObject.TryGetComponent(out Light comp2))
                {
                    bool borrable = true;
                    foreach (Transform child in temptrans)
                    {
                        if (child.gameObject.activeSelf) borrable = false;
                    }
                    if (borrable)
                        temptrans.name = "deleteme";
                }
            }

            tempGO = NewBuiding.transform.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform temptrans in tempGO)
            {
                if (!temptrans.gameObject.activeSelf)
                    temptrans.name = "deleteme";
            }

            GameObject del = new GameObject();
            tempGO = NewBuiding.transform.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform temptrans in tempGO)
            {
                if (temptrans.name == "deleteme")
                    if (temptrans != null) temptrans.transform.parent = del.transform;
            }
            NewBuiding.name = gameObject.name;           

            DestroyImmediate(NewBuiding.GetComponent<Building>());
            DestroyImmediate(del);
            DestroyImmediate(temp);
        }
        public void SimplifyTop()
        {
            GetReady();
            for (int forAUX = 0; forAUX < BuildingID[0]; forAUX++)
                if (Fs[forAUX].TryGetComponent(out RandomAddOns comp))
                    comp.SimplifyTop();
        }
        public void Simplify()
        {
            for (int forAUX = 0; forAUX < BuildingID[0]; forAUX++)
                if (Fs[forAUX].TryGetComponent(out RandomAddOns comp))
                    comp.Simplify();
            if (transform.childCount > 1)
                if (transform.GetChild(1).childCount > 0)
                    if (transform.GetChild(1).transform.GetChild(0).TryGetComponent(out RandomAddOns comp2))
                        comp2.Simplify();
        }
        public float Top()
        {
            return Fs[BuildingID[0] - 1].transform.position.y / transform.lossyScale.y;
        }
        public float FloorHight(int number)
        {
            return Fs[number].transform.position.y;
        }
        string NoClone(string oldname)
        {
            string[] letters = oldname.Split("(", 2);
            return letters[0];
        }

        public void TEST()
        {
            /*
            FreshBuild2();
            Debug.Log("pisos " + BBP.Length);
            for (int forAUX = 0; forAUX < BBP.Length; forAUX++)
            {
                Debug.Log(" " + BBP[forAUX]);

            }
            //Recolocate
            float[] tempheights = new float[8] { FloorsHight[0], FloorsHight[1], FloorsHight[2], FloorsHight[3], 0f, FloorsHight[1] * 4, FloorsHight[3] * 4, StandAloneFloorHight };
            float temp = 0f;
            for (int forAUX = 0; forAUX < BBP.Length; forAUX++)
            {
                if (BBP[forAUX] < 50)
                {
                    Fs[forAUX].transform.position = transform.position + Vector3.up * temp + transform.right * temp * 0.125f;
                    temp += tempheights[BBP[forAUX]];
                }
            }
            */
        }
    }
}

// al cambiar de block a standalone no respeta la iluminacion nocturna
