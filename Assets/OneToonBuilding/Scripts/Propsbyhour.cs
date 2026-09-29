using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine;

namespace ToonTown
{
    public class Propsbyhour : MonoBehaviour
    {
        public bool Dawn;
        public bool Day;
        public bool Sunset;
        public bool Night;
        public bool LateNight;
        public bool DarkNight;

        public bool CheckActive(int when)
        {
            bool active = false;
            if (Dawn && when == 0) active = true;
            if (Day && when == 1) active = true;
            if (Sunset && when == 2) active = true;
            if (Night && when == 3) active = true;
            if (LateNight && when == 4) active = true;
            if (DarkNight && when == 5) active = true;

            return active;
        }
    }
}

   
