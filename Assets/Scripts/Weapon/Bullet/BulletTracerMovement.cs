using UnityEngine;
using System.Collections;

public class BulletTracerMovement : MonoBehaviour
{
    private Vector3 target_point;
    private float move_Speed;
    private float life_Time;
    private bool can_Move = true;

    private TrailRenderer trailRenderer;

    private void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    private void Update()
    {
        if (!can_Move)
            return;

        transform.position = Vector3.MoveTowards(
        transform.position,
        target_point,
        move_Speed * Time.deltaTime);

        float distance = Vector3.Distance(transform.position, target_point);

        if (distance < 0.05f)
        {
            can_Move = false;

            // Lo devolvemos al pool inmediatamente al llegar
            gameObject.SetActive(false);
        }
    }

    public void Initialized(Vector3 end_point, float speed, float lifetime)
    {
        // Evita corrutinas viejas del mismo objeto
        StopAllCoroutines();

        target_point = end_point;
        move_Speed = speed;
        life_Time = lifetime;
        can_Move = true;

        // Limpia cualquier estela anterior
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }

        StartCoroutine(HideTracer(life_Time));
    }

    private IEnumerator HideTracer(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        if (gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // Limpia la estela al volver al pool
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }
    }
}