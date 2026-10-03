using System;
using UnityEngine;
using System.Collections.Generic;


[Serializable]

public class BulletTracer
{
    private List<GameObject> bullet_Tracer_List = new List<GameObject>();
    [SerializeField] private GameObject bullet_Tracer_prefab;
    [SerializeField] private Transform bullet_Tracer_Holder;
    [Header("Tracer settings")]
    [SerializeField] private int No_Of_Tracers = 30;
    [SerializeField] private float tracer_Speed = 300;
    [SerializeField] private float tracer_Life_Time = 2;

    public void initializeTracers()
    {
        for (int i = 0; i < No_Of_Tracers; ++i)
        {
            // Instanciamos SIN padre y reparenteamos después: Unity no permite
            // crear un objeto ya parentado a algo que vive en la escena
            // persistente (DontDestroyOnLoad, como Player) en la misma llamada
            // a Instantiate, pero SetParent posterior sí es válido.
            GameObject new_Tracer = UnityEngine.Object.Instantiate(bullet_Tracer_prefab);
            new_Tracer.transform.SetParent(bullet_Tracer_Holder, false);
            bullet_Tracer_List.Add(new_Tracer);
            bullet_Tracer_List[i].SetActive(false);
        }
    }


    public void PlayTracer(Vector3 start_pos, Vector3 end_pos)
    {
        GameObject current_Trace = GetPoolBulletTracers(start_pos);
        if (current_Trace == null) return;
        current_Trace.SetActive(true);
        BulletTracerMovement tracer_Movement = current_Trace.GetComponent<BulletTracerMovement>();
        if (tracer_Movement != null)
        {
            tracer_Movement.Initialized(end_pos, tracer_Speed, tracer_Life_Time);
        }

    }
    private GameObject GetPoolBulletTracers(Vector3 emit_Pos)
    {
        int index = UnityEngine.Random.Range(0, bullet_Tracer_List.Count);
        for (int i = 0; i < bullet_Tracer_List.Count; ++i)
        {
            if (!bullet_Tracer_List[index].activeInHierarchy)
            {
                bullet_Tracer_List[index].transform.position = emit_Pos;
                return bullet_Tracer_List[index];

            }
            else
            {
                index = UnityEngine.Random.Range(0, bullet_Tracer_List.Count);

            }


        }
        return null;
    }
}