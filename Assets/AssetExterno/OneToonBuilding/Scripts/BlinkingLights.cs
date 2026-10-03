//using System.Collections;
//using System.Collections.Generic;
using UnityEngine;


namespace ToonTown
{
    public class BlinkingLights : MonoBehaviour
    {
        Renderer rend;
        public float timing;
        public int matindex;
        public int offset;
        public int texturesteps;
        Vector2 dist;
        public bool RandomOffset;
        public bool RandomSpeed;
        float pin = 1f;
        float mytime;
        private void Start()
        {
            if (RandomSpeed)
            {
                timing *= Random.Range(0.7f, 1.3f);
                if (Random.Range(0, 2) == 1) pin = -1f;
            }
            rend = GetComponent<Renderer>();
            dist = new Vector2(1f / texturesteps, 0f) * Random.Range(-offset, offset);
            rend.materials[matindex].SetTextureOffset("_BaseMap", dist);
            mytime = Random.Range(0f, timing);
        }

        void SetOffset()
        {
            dist += new Vector2((1f/ texturesteps) * pin, 0f);         
            rend.materials[matindex].SetTextureOffset("_BaseMap", dist);
        }

        private void Update()
        {
            mytime += Time.deltaTime;
            if (mytime > timing)
            {
                dist += new Vector2((1f / texturesteps) * pin, 0f);
                rend.materials[matindex].SetTextureOffset("_BaseMap", dist);
                mytime = 0f;
            }
        }



        
    }
}
