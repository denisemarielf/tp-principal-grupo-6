
using UnityEngine;

[RequireComponent(typeof(Light))]
public class LuzParpadeante : MonoBehaviour
{
    [Header("Intensidad de la luz")]
    [SerializeField] private float intensidadEncendida = 0.4f;

    [Header("Tiempo entre parpadeos")]
    [SerializeField] private float tiempoMinimo = 0.05f;
    [SerializeField] private float tiempoMaximo = 0.3f;

    private Light luz;
    private float temporizador;
    private bool encendida = true;

    private void Awake()
    {
        luz = GetComponent<Light>();
        luz.intensity = intensidadEncendida;
        temporizador = Random.Range(tiempoMinimo, tiempoMaximo);
    }

    private void Update()
    {
        temporizador -= Time.deltaTime;

        if (temporizador <= 0f)
        {
            encendida = !encendida;

            luz.intensity = encendida ? intensidadEncendida : 0f;

            temporizador = Random.Range(tiempoMinimo, tiempoMaximo);
        }
    }
}