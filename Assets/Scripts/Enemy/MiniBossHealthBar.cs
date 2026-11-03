using UnityEngine;
using UnityEngine.UI;

// Barra de vida flotante sobre la cabeza del mini jefe. Se arma sola por codigo (no hace falta prefab de UI)
// y lee la vida de EnemyHealth, que ya viaja sincronizada a todos los clientes.
[RequireComponent(typeof(EnemyHealth))]
public class MiniBossHealthBar : MonoBehaviour
{
    [SerializeField] private float heightAboveGround = 4.2f;
    [SerializeField] private float barWidth = 2.6f;          // ancho en metros
    [SerializeField] private Color fillColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private float smoothing = 8f;

    private const float PixelWidth = 260f;
    private const float PixelHeight = 26f;
    private const float Border = 4f;

    private EnemyHealth health;
    private Transform barRoot;
    private RectTransform fill;
    private float shown = 1f;
    private bool initialised;

    private void Start()
    {
        health = GetComponent<EnemyHealth>();
        Build();
    }

    private void Build()
    {
        GameObject root = new GameObject("MiniBossHealthBar", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        barRoot = root.transform;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(PixelWidth, PixelHeight);
        rootRect.localScale = Vector3.one * (barWidth / PixelWidth);
        rootRect.localPosition = new Vector3(0f, heightAboveGround, 0f);

        Image back = root.AddComponent<Image>();
        back.color = backColor;
        back.raycastTarget = false;

        GameObject slotObject = new GameObject("Slot", typeof(RectTransform));
        RectTransform slot = slotObject.GetComponent<RectTransform>();
        slot.SetParent(rootRect, false);
        slot.anchorMin = Vector2.zero;
        slot.anchorMax = Vector2.one;
        slot.offsetMin = new Vector2(Border, Border);
        slot.offsetMax = new Vector2(-Border, -Border);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
        fill = fillObject.GetComponent<RectTransform>();
        fill.SetParent(slot, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (barRoot == null)
            return;

        if (health == null || health.IsDead())
        {
            if (barRoot.gameObject.activeSelf)
                barRoot.gameObject.SetActive(false);
            return;
        }

        float current = health.GetCurrentHealth();
        float target = Mathf.Clamp01(current / Mathf.Max(health.MaxHealth, 1f));

        // La primera vez que llega la vida sincronizada, saltar directo (evita ver la barra "llenarse").
        if (!initialised && current > 0f)
        {
            shown = target;
            initialised = true;
        }

        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        fill.anchorMax = new Vector2(Mathf.Clamp01(shown), 1f);

        Camera cam = Camera.main;
        if (cam != null)
            barRoot.rotation = cam.transform.rotation;
    }
}
