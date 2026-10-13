using UnityEngine;

// Le pone a cada zombi una luz de relleno tenue, solo en esta escena.
// En interiores oscuros los zombis quedan como siluetas negras, sobre todo a contraluz. El prefab del zombi tambien
// lo usan otros mapas, asi que la luz no se agrega ahi sino aca: se crea como hijo de cada zombi cuando aparece.
public class ZombieFillLight : MonoBehaviour
{
    private const string LightName = "FillLight";

    [SerializeField] private Color color = new Color(0.6f, 0.75f, 0.7f);
    [SerializeField] private float intensity = 1.1f;
    [SerializeField] private float range = 8f;
    [Tooltip("Posicion local respecto del zombi (escala 1.5): un poco arriba y hacia el frente.")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.3f, 0.7f);
    [SerializeField] private float checkInterval = 0.5f;

    private float nextCheck;

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude))
        {
            if (enemy.transform.Find(LightName) != null) continue;

            GameObject fill = new GameObject(LightName);
            fill.transform.SetParent(enemy.transform, false);
            fill.transform.localPosition = localOffset;

            Light light = fill.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
