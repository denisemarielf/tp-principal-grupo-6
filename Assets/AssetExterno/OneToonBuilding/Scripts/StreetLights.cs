using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ToonTown
{
    public class StreetLights : MonoBehaviour
    {        
        void Update()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 viewline = new(cam.transform.position.x, transform.position.y, cam.transform.position.z);
            transform.LookAt(viewline, transform.parent.transform.up);            
        }        
    }
}
