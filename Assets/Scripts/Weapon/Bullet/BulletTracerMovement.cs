using UnityEngine;
using System.Collections;

public class BulletTracerMovement : MonoBehaviour
{
    private Vector3 target_point;
    private float move_Speed;
    private float life_Time;
    private bool can_Move = true;
  
    // Update is called once per frame
    void Update()
    {
        if (!can_Move) return;
        transform.position = Vector3.MoveTowards(transform.position, target_point, move_Speed * Time.deltaTime);
        float distance = Vector3.Distance(transform.position, target_point);
        if (distance < 0.05f)
        {
            can_Move = false;
        }
    }

    public void Initialized(Vector3 end_point, float speed, float lifetime)
    {
        target_point = end_point;
        move_Speed = speed;
        life_Time = lifetime;
        StartCoroutine(HideTracer(life_Time));
    }

    IEnumerator HideTracer(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        gameObject.SetActive(false);
    }
}
