using System.Collections;
using UnityEngine;

// Explosiones lejanas de ambiente: cada tanto aparece una explosion en un punto al azar alrededor del centro,
// con un destello naranja que ilumina el entorno un instante. Es solo decorativo (no tiene dano ni red).
public class AmbientExplosions : MonoBehaviour
{
    [SerializeField] private GameObject explosionPrefab;
    [Tooltip("Centro alrededor del cual aparecen. Si esta vacio, este mismo objeto.")]
    [SerializeField] private Transform center;
    [Tooltip("Distancia al centro (min, max) en metros.")]
    [SerializeField] private Vector2 distance = new Vector2(60f, 150f);
    [Tooltip("Segundos entre explosiones (min, max).")]
    [SerializeField] private Vector2 interval = new Vector2(10f, 30f);
    [SerializeField] private float explosionScale = 4f;
    [SerializeField] private float groundY = 17f;
    [SerializeField] private Color flashColor = new Color(1f, 0.55f, 0.2f);
    [SerializeField] private float flashIntensity = 6f;
    [SerializeField] private float flashRange = 90f;

    private void OnEnable()
    {
        StartCoroutine(Loop());
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(interval.x * 0.5f, interval.y));

        while (true)
        {
            Transform origin = center != null ? center : transform;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(distance.x, distance.y);
            Vector3 position = origin.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            position.y = groundY;

            StartCoroutine(Explode(position));
            yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
        }
    }

    private IEnumerator Explode(Vector3 position)
    {
        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(explosionPrefab, position, Quaternion.identity);
            effect.transform.localScale *= explosionScale;
            Destroy(effect, 6f);
        }

        GameObject flash = new GameObject("ExplosionFlash");
        flash.transform.position = position + Vector3.up * 8f;
        Light light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = flashColor;
        light.range = flashRange;
        light.shadows = LightShadows.None;

        const float duration = 0.8f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            light.intensity = flashIntensity * (1f - t / duration);
            yield return null;
        }

        Destroy(flash);
    }
}
