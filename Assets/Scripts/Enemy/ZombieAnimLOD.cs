using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ZombieAnimLOD : MonoBehaviour
{
    [SerializeField] float nearDistance = 25f;   // animación completa
    [SerializeField] float farDistance = 60f;    // más lejos: actualiza cada 4 frames
    [SerializeField] int midInterval = 2;
    [SerializeField] int farInterval = 4;

    Animator anim;
    Transform cam;
    float accumulated;
    int frameOffset;

    void Awake()
    {
        anim = GetComponent<Animator>();
        frameOffset = Random.Range(0, 8); // evita que todos actualicen el mismo frame
    }

    void Update()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        float dist = Vector3.Distance(cam.position, transform.position);
        int interval = dist < nearDistance ? 1 : dist < farDistance ? midInterval : farInterval;

        if (interval == 1)
        {
            if (!anim.enabled) anim.enabled = true;
            accumulated = 0f;
            return;
        }

        // Modo reducido: el Animator se actualiza a mano cada N frames
        if (anim.enabled) anim.enabled = false;
        accumulated += Time.deltaTime;

        if ((Time.frameCount + frameOffset) % interval == 0)
        {
            anim.Update(accumulated);
            accumulated = 0f;
        }
    }
}