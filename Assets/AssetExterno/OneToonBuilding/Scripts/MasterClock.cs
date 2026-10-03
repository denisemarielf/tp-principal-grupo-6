using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEditor;


namespace ToonTown
{
    public class MasterClock : MonoBehaviour
    {
        // 0 dawn           streetlights OFF    shops dark  closed      windows 0
        // 1 day            streetlights OFF    shops dark  open        windows 0
        // 2 sunset         streetlights OFF    shops light open        windows 1
        // 3 night          streetlights ON     shops light open        windows 3
        // 4 latenight      streetlights ON     shops dark  closed      windows 2
        // 5 dark night     streetlights ON     shops dark  closed      windows 1

        Light SUN;
        private Light[] lights;
        public Transform Dawn;
        public Transform Day;
        public Transform Sunset;
        public Transform Night;
        public Transform LateNight;
        public Transform DarkNight;
        public Material[] Sky;
        public Material[] SimpleSky;
        Transform[] Hours;
        Color[] SunColor;
        float[] Intensity;
        Color[] AmbientColor;
        bool[] FogOn;
        Color[] FogColor;
        Vector2[] FogRange;
        //[HideInInspector]
        public Transform Helper;
        public int Now = 1;
        bool OnPos = true;
        bool Editor = true;
        public float FarFog;
        public bool BackgroundCity;


        void Start()
        {
            //Helper = transform.GetChild(0).gameObject;
            lights = FindObjectsOfType(typeof(Light)) as Light[];
            foreach (Light light in lights)
            {
                if (light.name == "SUN")
                    SUN = light;
            }            
            Editor = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            OnPos = true;
            Hours = new Transform[6] { Dawn, Day, Sunset, Night, LateNight, DarkNight };
            SunColor = new Color[6] { new Color(2.53f,0.82f,0.92f,255f), new Color(2.55f, 2.52f, 1.94f, 255f), new Color(2.53f, 0.82f, 0.92f, 255f),
                                  new Color(0.75f, 0.96f, 1.30f, 255f), new Color(0.75f, 0.96f, 1.30f, 255f), new Color(0.75f, 0.96f, 1.30f, 255f) };
            Intensity = new float[6] { 1f, 1f, 1f, 1f, 1f, 0.5f };

            AmbientColor = new Color[6] { new Color(0.37f,0.88f,1.91f,255f), new Color(0.9f, 0.9f, 1.09f, 255f), new Color(0.61f, 0.12f, 0.3f, 255f),
                                  new Color(0.06f, 0.13f, 0.56f, 255f), new Color(0.09f, 0.22f, 0.44f, 255f), new Color(0.04f, 0.05f, 0.29f, 255f) };

            FogOn = new bool[6] { true, true, false, false, true, false };
            FogColor = new Color[6] { new Color(1.13f,1.08f,1.16f,255f), new Color(1.98f, 2.15f, 2.24f, 255f), new Color(2.53f, 0.82f, 0.92f, 255f),
                                  new Color(0.75f, 0.96f, 1.30f, 255f), new Color(0.26f, 0.33f, 0.64f, 255f), new Color(0.75f, 0.96f, 1.30f, 255f) }; ;
            FogRange = new Vector2[6] { new Vector2(25f, 1750f + FarFog), new Vector2(80f, 1700f + FarFog), new Vector2(50f, 400f + FarFog), new Vector2(50f, 400f + FarFog), new Vector2(50f, 800f + FarFog), new Vector2(50f, 400f + FarFog) };
            //Helper.rotation = Hours[Now].rotation;
        }

        void Update()
        {
            if (!OnPos)
            {
                MoveSun();
            }
        }

        // day/night
        void MoveSun()
        {
            if (Vector3.Angle(SUN.transform.position, Helper.position) < 0.1f) OnPos = true;
            SUN.transform.rotation = Quaternion.Lerp(SUN.transform.rotation, Helper.rotation, 2f * Time.deltaTime);
        }
        public void ChangeHour(int NewHour)
        {
            //Helper = transform.GetChild(0).gameObject;
            if (NewHour != Now)
            {
                if (Editor) InEditor();
                OnPos = false;
                Now = NewHour;
                ChangeSun();
            }
        }
        public void ChangeHourB(int NewHour)
        {
            //Helper = transform.GetChild(0).gameObject;
            if (NewHour != Now)
            {
                if (Editor) InEditor();
                OnPos = false;
                Now = NewHour;
                ChangeSunB();
            }
        }
        public void NextHour(int NewHour)
        {
            //Helper = transform.GetChild(0).gameObject;
            if (Editor) InEditor();
            OnPos = false;
            Now += NewHour;
            if (Now < 0) Now = 5;
            if (Now > 5) Now = 0;
            ChangeSun();
        }
        public void ChangeFogRange()
        {
            //Helper = transform.GetChild(0).gameObject;
            FogRange = new Vector2[6] { new Vector2(12f, 1300f), new Vector2(125f, 2500f), new Vector2(50f, 1200f), new Vector2(50f, 1200f), new Vector2(50f, 1600f), new Vector2(50f, 1200f) };

        }
        void ChangeSun()
        {
            Helper.rotation = Hours[Now].rotation;
            if (Editor) SUN.transform.rotation = Helper.rotation;
            SUN.GetComponent<Light>().color = SunColor[Now];
            SUN.GetComponent<Light>().intensity = Intensity[Now];

            if (BackgroundCity) RenderSettings.skybox = Sky[Now];
            else RenderSettings.skybox = SimpleSky[Now];
            DynamicGI.UpdateEnvironment();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor[Now];
            RenderSettings.ambientIntensity = 0f;
            DynamicGI.UpdateEnvironment();

            //fog
            RenderSettings.fog = FogOn[Now];
            RenderSettings.fogColor = FogColor[Now];
            RenderSettings.fogStartDistance = FogRange[Now].x;
            RenderSettings.fogEndDistance = FogRange[Now].y;

            // blocks
            Building[] OneBuild  = FindObjectsOfType<Building>();
            foreach (Building item in OneBuild)
            {
                item.GetComponent<Building>().DayHour(item.transform, Now, true);
            }
            /*
            Block[] AllBlocks = FindObjectsOfType<Block>();
            foreach (Block item in AllBlocks)
            {
                item.GetComponent<Block>().DayHour(Now);
            }
            AllBuildings[] TheAllBuildings = FindObjectsOfType<AllBuildings>();
            foreach (AllBuildings item in TheAllBuildings)
            {
                if (item.transform.parent == null)
                item.GetComponent<AllBuildings>().DayHour(Now);        
            }
*/
        }
        void ChangeSunB()
        {
            Helper.rotation = Hours[Now].rotation;
            if (Editor) SUN.transform.rotation = Helper.rotation;
            SUN.GetComponent<Light>().color = SunColor[Now];
            SUN.GetComponent<Light>().intensity = Intensity[Now];

            RenderSettings.skybox = Sky[Now];
            //RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.realtime;

            //if (Now < 3) RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            //else
            {
                DynamicGI.UpdateEnvironment();
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                //RenderSettings.ambientSkyColor = AmbientColor[Now];
                RenderSettings.ambientLight = AmbientColor[Now];

                RenderSettings.ambientIntensity = 0f;
                DynamicGI.UpdateEnvironment();
            }
            //if (EditorApplication.isPlaying) RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.;
            //fog
            RenderSettings.fog = FogOn[Now];
            RenderSettings.fogColor = FogColor[Now];
            RenderSettings.fogStartDistance = FogRange[Now].x;
            RenderSettings.fogEndDistance = FogRange[Now].y;

            // blocks
            Building[] Buildings = FindObjectsOfType<Building>();
            foreach (Building item in Buildings)
            {
                item.GetComponent<Building>().DayHour(item.transform, Now, true);
            }
        }
        void InEditor()
        {
            lights = FindObjectsOfType(typeof(Light)) as Light[];
            foreach (Light light in lights)
            {
                if (light.name == "SUN")
                    SUN = light;
            }



            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Hours = new Transform[6] { Dawn, Day, Sunset, Night, LateNight, DarkNight };
            SunColor = new Color[6] { new Color(2.53f,0.82f,0.92f,255f), new Color(2.55f, 2.52f, 1.94f, 255f), new Color(2.53f, 0.82f, 0.92f, 255f),
                                  new Color(0.75f, 0.96f, 1.30f, 255f), new Color(0.75f, 0.96f, 1.30f, 255f), new Color(0.75f, 0.96f, 1.30f, 255f) };
            Intensity = new float[6] { 1f, 1f, 1f, 1f, 1f, 0.5f };

            AmbientColor = new Color[6] { new Color(0.37f,0.88f,1.91f,255f), new Color(0.9f, 0.9f, 1.09f, 255f), new Color(0.61f, 0.12f, 0.3f, 255f),
                                  new Color(0.06f, 0.13f, 0.56f, 255f), new Color(0.09f, 0.22f, 0.44f, 255f), new Color(0.04f, 0.05f, 0.29f, 255f) };

            FogOn = new bool[6] { true, true, false, false, true, false };
            FogColor = new Color[6] { new Color(2.1f,1.41f,2.06f,255f), new Color(1.98f, 2.15f, 2.24f, 255f), new Color(2.53f, 0.82f, 0.92f, 255f),
                                  new Color(0.75f, 0.96f, 1.30f, 255f), new Color(0.26f, 0.33f, 0.64f, 255f), new Color(0.75f, 0.96f, 1.30f, 255f) }; ;
            FogRange = new Vector2[6] { new Vector2(25f, 1750f + FarFog), new Vector2(80f, 1700f + FarFog), new Vector2(50f, 400f + FarFog), new Vector2(50f, 400f + FarFog), new Vector2(50f, 800f + FarFog), new Vector2(50f, 400f + FarFog) };
            //Helper = transform.GetChild(0).gameObject;

            Helper.rotation = Hours[Now].rotation;

            //SUN.transform.parent = Helper.transform;
        }

        // change blocks
        public void ResetAll()
        {
            Building[] Buildings = FindObjectsOfType<Building>();
            foreach (Building item in Buildings)
            {
                item.GetComponent<Building>().FreshBuild();
            }

        }
        public void RandomizeAll()
        {
            Building[] Buildings = FindObjectsOfType<Building>();
            foreach (Building item in Buildings)
            {
                item.GetComponent<Building>().Randomize();
            }
        }
        public void FloorN(int howmany)
        {
            Building[] Buildings = FindObjectsOfType<Building>();
            foreach (Building item in Buildings)
            {
                if (howmany > 0)
                    item.GetComponent<Building>().AddFloor();
                else item.GetComponent<Building>().RemoveFloor();
            }
        }
        
        // fog
        public void ChangeFogFarRange(float distance)
        {
            //Helper = transform.GetChild(0).gameObject;
            FarFog += distance * 200f;
            FarFog = Mathf.Clamp(FarFog, 10f, 8000f);
            FogRange = new Vector2[6] { new Vector2(25f, 1750f + FarFog), new Vector2(80f, 1700f + FarFog), new Vector2(50f, 400f + FarFog), new Vector2(50f, 400f + FarFog), new Vector2(50f, 800f + FarFog), new Vector2(50f, 400f + FarFog) };
            RenderSettings.fogEndDistance = FogRange[Now].y;
            Debug.Log("niebla  " + FogRange[Now].y);
        }

        //city
        public void CityONOFF()
        {
            BackgroundCity = !BackgroundCity;
            if (BackgroundCity) RenderSettings.skybox = Sky[Now];
            else RenderSettings.skybox = SimpleSky[Now];
        }
        
    }
}