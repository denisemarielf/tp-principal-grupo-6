using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ToonTown
{

    public class RandomAddOns : MonoBehaviour
    {
        public void RandomElements()
        {
            for (int forAUX = 0; forAUX < transform.childCount; forAUX++)
            {
                RandomTop();
                RandomFront();
                RandomBack();
                //if (transform.GetChild(forAUX).name == "Top") RandomON(transform.GetChild(forAUX).gameObject);
                //if (transform.GetChild(forAUX).name == "Front") RandomON(transform.GetChild(forAUX).gameObject);
                //if (transform.GetChild(forAUX).name == "Back") RandomON(transform.GetChild(forAUX).gameObject);
            }
        }
        public void RandomTop()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Top")
                    RandomON(child.gameObject);
            }
        }
        public void RandomFront()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Front")
                    RandomON(child.gameObject);
            }
        }
        public void RandomBack()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Back")
                    RandomON(child.gameObject);
            }
        }

        public void Simplify()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Front")
                    OptionalsOFF(child.gameObject);
                if (child.name == "Top")
                    OptionalsOFF(child.gameObject);
            }
        }
        public void SimplifyTop()
        {
            //foreach (Transform child in transform.GetComponentsInChildren<Transform>())  
            //    if (child.name == "Top")
            //        OptionalsOFF(child.gameObject);            
            CleanTop();
        }

        public void CleanElements()
        {
            CleanTop();
            CleanFront();
            CleanBack();
            /*
            string[] names = new[] { "OptionalA", "OptionalB", "OptionalC", "OptionalD" };
            Transform[] compAUX;
            compAUX = GetComponentsInChildren<Transform>();
            foreach (Transform child in compAUX)
            {
                for (int forAUX1 = 0; forAUX1 < names.Length; forAUX1++)
                    for (int forAUX2 = 0; forAUX2 < 6; forAUX2++)
                        if (child.name == names[forAUX1] + "0" + forAUX2) child.gameObject.SetActive(false);
            }
            */
        }
        public void CleanTop()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Top")
                    RandomOFF(child.gameObject);
            }
        }
        public void CleanFront()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Front")
                    RandomOFF(child.gameObject);
            }
        }
        public void CleanBack()
        {
            foreach (Transform child in transform.GetComponentsInChildren<Transform>())
            {
                if (child.name == "Back")
                    RandomOFF(child.gameObject);
            }
        }

        public void UnhideALL()
        {
            for (int forAUX = 0; forAUX < transform.childCount; forAUX++)
            {
                if (transform.GetChild(forAUX).name == "Top" || transform.GetChild(forAUX).name == "Front" || transform.GetChild(forAUX).name == "Back")
                {
                    for (int forAUX2 = 0; forAUX2 < transform.GetChild(forAUX).transform.childCount; forAUX2++)
                        transform.GetChild(forAUX).transform.GetChild(forAUX2).gameObject.SetActive(true);
                }
            }
        }

        void RandomON(GameObject target)
        {
            GameObject[][] AddOns = new GameObject[6][];
            string[] names = new[] { "EssentialA", "EssentialB", "OptionalA", "OptionalB", "OptionalC", "OptionalD" };
            int[] indexs = new int[] { 0, 0, 0, 0, 0, 0 };
            AddOns[0] = new GameObject[6];
            AddOns[1] = new GameObject[6];
            AddOns[2] = new GameObject[6];
            AddOns[3] = new GameObject[6];
            AddOns[4] = new GameObject[6];
            AddOns[5] = new GameObject[6];
            for (int onechild = 0; onechild < target.transform.childCount; onechild++)
            {
                for (int searchname = 0; searchname < 6; searchname++)
                {
                    for (int index = 0; index < 6; index++)
                    {
                        if (target.transform.GetChild(onechild).name == names[searchname] + "0" + (index + 1))
                        {
                            AddOns[searchname][indexs[searchname]] = target.transform.GetChild(onechild).gameObject;
                            indexs[searchname]++;
                        }
                    }
                }
            }

            for (int AddOnsN = 0; AddOnsN < 6; AddOnsN++)
            {
                for (int index = 0; index < indexs[AddOnsN]; index++)
                    if (indexs[AddOnsN] > 0) AddOns[AddOnsN][index].SetActive(false);
            }
            //Essentials
            for (int AddOnsN = 0; AddOnsN < 2; AddOnsN++)
            {
                if (indexs[AddOnsN] > 0) AddOns[AddOnsN][Random.Range(0, indexs[AddOnsN])].SetActive(true);
            }
            //Optionals
            for (int AddOnsN = 2; AddOnsN < 6; AddOnsN++)
            {
                int coin = Random.Range(0, 6);
                if (indexs[AddOnsN] > 0 && coin < indexs[AddOnsN]) AddOns[AddOnsN][coin].SetActive(true);
            }


        }
        void RandomOFF(GameObject target)
        {
            string[] names = new[] { "EssentialA", "EssentialB", "OptionalA", "OptionalB", "OptionalC", "OptionalD" };
            Transform[] compAUX;
            compAUX = target.GetComponentsInChildren<Transform>();
            foreach (Transform child in compAUX)
            {
                for (int forAUX1 = 0; forAUX1 < names.Length; forAUX1++)
                    for (int forAUX2 = 0; forAUX2 < 6; forAUX2++)
                        if (child.name == names[forAUX1] + "0" + forAUX2) child.gameObject.SetActive(false);
            }
        }
        void OptionalsOFF(GameObject target)
        {
            string[] names = new[] { "OptionalA", "OptionalB", "OptionalC", "OptionalD" };
            Transform[] compAUX;
            compAUX = target.GetComponentsInChildren<Transform>();
            foreach (Transform child in compAUX)
            {
                for (int forAUX1 = 0; forAUX1 < names.Length; forAUX1++)
                    for (int forAUX2 = 0; forAUX2 < 6; forAUX2++)
                        if (child.name == names[forAUX1] + "0" + forAUX2) child.gameObject.SetActive(false);
            }
        }
    }
}
