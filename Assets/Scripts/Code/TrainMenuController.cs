using System.Collections;
using UnityEngine;

public class TrainMenuController : MonoBehaviour
{
    [Header("Tren")]
    [SerializeField] private Transform train;          // padre con los vagones
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform stationPoint;
    [SerializeField] private float speed = 25f;
    [SerializeField] private bool alternateDirection = true; // ida y vuelta

    [Header("Intervalo entre trenes (segundos)")]
    [SerializeField] private float minInterval = 10f;
    [SerializeField] private float maxInterval = 25f;
    [SerializeField] private float firstTrainDelay = 3f;

    [Header("Bocina")]
    [SerializeField] private float hornDistance = 40f;  // distancia a la estación para tocar

    [Header("Audio")]
    [SerializeField] private AudioSource movingSource;   // loop del tren rodando
    [SerializeField] private AudioSource hornSource;     // solo el AudioSource, sin clip fijo
    [SerializeField] private AudioClip[] hornClips;      // todas tus bocinas
    [SerializeField] private Vector2 hornPitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private float fadeTime = 1.5f;
    [SerializeField] private float movingMaxVolume = 1f;
    [Header("Colisiones")]
    [SerializeField] private string[] ignoreTags = { "WorldWall", "Player" };

    [Header("Rotación")]
    [Tooltip("Ajustá si el tren avanza de costado (ej: 0, 90, 0)")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private int lastHornIndex = -1;
    [SerializeField] private AudioClip[] movingClips;
    private bool forward = true;
    private int lastMovingIndex = -1;

    private void Start()
    {
        IgnoreCollisionsByTag();
        train.gameObject.SetActive(false);
        StartCoroutine(TrainLoop());
    }

    private void PlayRandomMoving()
    {
        if (movingClips == null || movingClips.Length == 0) return;

        int index = Random.Range(0, movingClips.Length);
        if (movingClips.Length > 1 && index == lastMovingIndex)
            index = (index + 1) % movingClips.Length;
        lastMovingIndex = index;

        movingSource.clip = movingClips[index];
        movingSource.loop = true;
        movingSource.volume = 0f;
        movingSource.Play();
    }
    private void PlayRandomHorn()
    {
        if (hornClips == null || hornClips.Length == 0) return;

        int index = Random.Range(0, hornClips.Length);
        if (hornClips.Length > 1 && index == lastHornIndex)
            index = (index + 1) % hornClips.Length;
        lastHornIndex = index;

        hornSource.pitch = Random.Range(hornPitchRange.x, hornPitchRange.y);
        hornSource.PlayOneShot(hornClips[index]);
    }
    private IEnumerator TrainLoop()
    {
        yield return new WaitForSeconds(firstTrainDelay);

        while (true)
        {
            yield return StartCoroutine(RunTrain(forward));

            if (alternateDirection) forward = !forward;

            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    private IEnumerator RunTrain(bool goForward)
    {
        Transform from = goForward ? startPoint : endPoint;
        Transform to = goForward ? endPoint : startPoint;

        Vector3 dir = (to.position - from.position).normalized;
        train.position = from.position;
        train.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(rotationOffset);
        train.gameObject.SetActive(true);
        movingSource.transform.position = train.position;
        hornSource.transform.position = train.position;
        movingSource.volume = 0f;
        movingSource.Play();

        bool hornPlayed = false;
        float fade = 0f;

        while (Vector3.Distance(train.position, to.position) > 0.1f)
        {
            train.position = Vector3.MoveTowards(train.position, to.position, speed * Time.unscaledDeltaTime);

            // Fade in al salir, fade out al llegar
            float distFromStart = Vector3.Distance(train.position, from.position);
            float distToEnd = Vector3.Distance(train.position, to.position);
            fade = Mathf.Clamp01(Mathf.Min(distFromStart, distToEnd) / (speed * fadeTime));
            movingSource.volume = fade * movingMaxVolume;

            // Bocina una sola vez al acercarse a la estación
            if (!hornPlayed && Vector3.Distance(train.position, stationPoint.position) < hornDistance)
            {
                PlayRandomHorn();
                hornPlayed = true;
            }

            yield return null;
        }

        PlayRandomMoving();
        train.gameObject.SetActive(false);
    }

    private void IgnoreCollisionsByTag()
    {
        Collider[] trainColliders = train.GetComponentsInChildren<Collider>(true);

        foreach (string tag in ignoreTags)
        {
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag(tag))
            {
                foreach (Collider other in obj.GetComponentsInChildren<Collider>(true))
                {
                    foreach (Collider trainCol in trainColliders)
                    {
                        Physics.IgnoreCollision(trainCol, other, true);
                    }
                }
            }
        }
    }
}