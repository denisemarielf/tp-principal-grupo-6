using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Un extremo de cable. Los "source" se arrastran hacia un "target" del mismo color.
public class WireConnector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Config")]
    public bool isSource;
    public Image colorImage;   // imagen que se pinta con el color del cable
    public Image led;          // lucecita: gris -> verde al conectar
    public float wireThickness = 14f;

    [HideInInspector] public int colorId;
    [HideInInspector] public bool connected;

    WireMinigame game;
    RectTransform rt;
    RectTransform wire;
    Image wireImg;
    bool dragging;

    public void Init(WireMinigame g, int id, Color c)
    {
        game = g;
        colorId = id;
        rt = (RectTransform)transform;
        colorImage.color = c;
        if (led) led.color = Color.gray;
        connected = false;
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
        wireImg.raycastTarget = false;   // para no bloquear el raycast del destino

        // Dibujar el cable por encima de los demás conectores
        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 10;
    }

    void UpdateWire(Vector2 localEnd)
    {
        wire.sizeDelta = new Vector2(localEnd.magnitude, wireThickness);
        float angle = Mathf.Atan2(localEnd.y, localEnd.x) * Mathf.Rad2Deg;
        wire.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!isSource || connected) return;
        dragging = true;
        if (wire == null) CreateWire();
        wireImg.color = colorImage.color;
        wire.gameObject.SetActive(true);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local);
        UpdateWire(local);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;

        var hit = e.pointerCurrentRaycast.gameObject;
        var target = hit ? hit.GetComponentInParent<WireConnector>() : null;

        if (target != null && !target.isSource && !target.connected)
        {
            if (target.colorId == colorId) { Success(target); return; }
            game.PlayError();
            StartCoroutine(FailRoutine());
            return;
        }
        wire.gameObject.SetActive(false);
    }

    void Success(WireConnector target)
    {
        connected = target.connected = true;
        UpdateWire(rt.InverseTransformPoint(target.transform.position));
        if (led) led.color = Color.green;
        if (target.led) target.led.color = Color.green;

        StartCoroutine(Pop(transform));
        StartCoroutine(Pop(target.transform));
        game.OnWireConnected(target.transform.position);
    }

    IEnumerator FailRoutine()
    {
        wireImg.color = Color.red;
        Vector3 start = transform.localPosition;
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            transform.localPosition = start + (Vector3)(Random.insideUnitCircle * 6f);
            yield return null;
        }
        transform.localPosition = start;
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
}
