using UnityEngine;
using System.Collections;

public class BulletTracerMovement : MonoBehaviour
{
    private Vector3 target_point;
    private float move_Speed;
    private bool can_Move;
    private Transform home;
    private TrailRenderer trailRenderer;

    private void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    private void Update()
    {
        if (!can_Move)
            return;

        transform.position = Vector3.MoveTowards(transform.position, target_point, move_Speed * Time.deltaTime);

        if ((transform.position - target_point).sqrMagnitude < 0.0025f)
        {
            can_Move = false;

            // No se apaga: la estela se desvanece sola y recién después vuelve al pool.
            StopAllCoroutines();
            float fade = trailRenderer != null ? trailRenderer.time : 0f;
            StartCoroutine(ReturnToPool(fade));
        }
    }

    public void Initialized(Vector3 end_point, float speed, float lifetime, Transform poolHome)
    {
        StopAllCoroutines();

        home = poolHome;
        target_point = end_point;
        move_Speed = speed;
        can_Move = true;

        if (trailRenderer != null)
            trailRenderer.Clear();

        // Seguro por si nunca llega al destino
        StartCoroutine(ReturnToPool(lifetime));
    }

    private IEnumerator ReturnToPool(float delay)
    {
        yield return new WaitForSeconds(delay);
        Release();
    }

    private void Release()
    {
        can_Move = false;

        // Si el arma ya no existe, el tracer huérfano se destruye
        if (home == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.SetParent(home, false);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (trailRenderer != null)
            trailRenderer.Clear();
    }
}