using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ToonTown
{
    public class StreetLights : MonoBehaviour
    {        
        void Update()
        {
            Vector3 viewline = new(Camera.main.transform.position.x, transform.position.y, Camera.main.transform.position.z);
            transform.LookAt(viewline, transform.parent.transform.up);            
        }        
    }
}
