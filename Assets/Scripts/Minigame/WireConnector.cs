using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Un extremo de cable. Los "source" se arrastran hacia un "target" del mismo color.
public class WireConnector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Config")]
    public bool isSource;
    public Image colorImage;
    public Image led;
    [Tooltip("Grosor del cable en unidades del Canvas (el CanvasScaler lo escala solo)")]
    public float wireThickness = 14f;

    [HideInInspector] public int colorId;
    [HideInInspector] public bool connected;

    WireMinigame game;
    RectTransform rt;
    RectTransform wire;
    Image wireImg;
    bool dragging;
    WireConnector linkedTarget;          // destino al que quedó conectado
    Vector3 shakeOffset;

    /// Centro visual del conector en coordenadas mundo (no depende del pivote).
    public Vector3 WorldCenter => rt.TransformPoint(rt.rect.center);

    public void Init(WireMinigame g, int id, Color c)
    {
        game = g;
        colorId = id;
        rt = (RectTransform)transform;

        StopAllCoroutines();
        gameObject.SetActive(true);

        colorImage.color = c;
        if (led) led.color = Color.gray;
        connected = false;
        linkedTarget = null;
        dragging = false;
        transform.localScale = Vector3.one;
        if (wire != null) wire.gameObject.SetActive(false);
    }

    void CreateWire()
    {
        var go = new GameObject("Wire", typeof(RectTransform), typeof(Image));
        wire = go.GetComponent<RectTransform>();
        wire.SetParent(rt, false);
        wire.anchorMin = wire.anchorMax = new Vector2(0.5f, 0.5f);
        wire.pivot = new Vector2(0f, 0.5f);
        wire.anchoredPosition = Vector2.zero;

        wireImg = go.GetComponent<Image>();
        wireImg.color = colorImage.color;
        wireImg.raycastTarget = false;

        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 10;
    }

    /// worldEnd: punto final del cable en coordenadas mundo.
    void UpdateWireToWorld(Vector3 worldEnd)
    {
        // Relativo al CENTRO del rect (el origen real del cable), no al pivote
        Vector2 localEnd = (Vector2)rt.InverseTransformPoint(worldEnd) - rt.rect.center;
        wire.sizeDelta = new Vector2(localEnd.magnitude, wireThickness);
        float angle = Mathf.Atan2(localEnd.y, localEnd.x) * Mathf.Rad2Deg;
        wire.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!isSource || connected || game.Locked) return;
        dragging = true;
        if (wire == null) CreateWire();
        wireImg.color = colorImage.color;
        wire.gameObject.SetActive(true);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                rt, e.position, e.pressEventCamera, out var world))
            UpdateWireToWorld(world);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;

        var hit = e.pointerCurrentRaycast.gameObject;
        var target = hit ? hit.GetComponentInParent<WireConnector>() : null;

        // Si no pegó justo, buscar el destino más cercano dentro de una tolerancia
        if (target == null || target.isSource)
            target = game.FindTargetNear(e.position, e.pressEventCamera);

        if (target != null && !target.isSource && !target.connected)
        {
            if (target.colorId == colorId) { Success(target); return; }
            StartCoroutine(FailRoutine());
            game.PlayError();
            return;
        }
        wire.gameObject.SetActive(false);
    }

    /// Intercambia color e id con otro conector (para la mezcla a mitad de nivel).
    public void SwapColorWith(WireConnector other)
    {
        (colorId, other.colorId) = (other.colorId, colorId);

        Color c = colorImage.color;
        colorImage.color = other.colorImage.color;
        other.colorImage.color = c;

        StartCoroutine(Pop(transform));
        other.StartCoroutine(other.Pop(other.transform));
    }

    void Success(WireConnector target)
    {
        connected = target.connected = true;
        linkedTarget = target;
        UpdateWireToWorld(target.WorldCenter);
        if (led) led.color = Color.green;
        if (target.led) target.led.color = Color.green;

        StartCoroutine(Pop(transform));
        StartCoroutine(Pop(target.transform));
        game.OnWireConnected(target.WorldCenter);
    }

    // Si el layout cambia (rotación, resize de ventana), el cable sigue pegado al destino
    void LateUpdate()
    {
        if (connected && linkedTarget != null && wire != null && wire.gameObject.activeSelf)
            UpdateWireToWorld(linkedTarget.WorldCenter);
    }

    IEnumerator FailRoutine()
    {
        wireImg.color = Color.red;
        // Shake solo visual: se mueve el hijo "colorImage", no el conector,
        // así no pelea con el Layout Group.
        var visual = colorImage.rectTransform;
        Vector2 start = visual.anchoredPosition;
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            visual.anchoredPosition = start + Random.insideUnitCircle * 6f;
            yield return null;
        }
        visual.anchoredPosition = start;
        wire.gameObject.SetActive(false);
    }

    IEnumerator Pop(Transform t)
    {
        for (float x = 0; x < 1f; x += Time.deltaTime * 6f)
        {
            t.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(x * Mathf.PI));
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    public void FlashLed(bool on)
    {
        if (led && !connected) led.color = on ? Color.red : Color.gray;
    }
}