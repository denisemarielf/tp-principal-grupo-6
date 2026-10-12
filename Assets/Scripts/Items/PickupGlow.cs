using UnityEngine;

public class PickupGlow : MonoBehaviour
{
    [Header("Pulso de la luz")]
    public Light glowLight;
    public float pulseSpeed = 2f;
    public float minIntensity = 1f;
    public float maxIntensity = 2.5f;

    void Update()
    {
        if (glowLight != null)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            glowLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        }
    }
}