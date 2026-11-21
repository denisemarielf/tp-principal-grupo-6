using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Flash (viñeta) cuando el jugador local recibe daño. Rojo por defecto, verde si el daño es de acido.
// Ponelo en el prefab del Player, en el mismo objeto que PlayerHealth.
public class DamageFeedbackUI : NetworkBehaviour
{
    [SerializeField] private Color color = new Color(0.8f, 0f, 0f, 1f);          // daño generico
    [SerializeField] private Color acidColor = new Color(0.3f, 0.85f, 0.05f, 1f); // daño de acido
    [SerializeField] private float maxAlpha = 0.85f;
    [SerializeField] private float fadeSpeed = 1.8f;   // mayor = se va mas rapido
    [SerializeField] private int sortingOrder = 20;    // bajalo si tapa tu GameOverUI

    private PlayerHealth health;
    private Image overlay;
    private float alpha;
    private Color currentTint;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { enabled = false; return; }

        health = GetComponent<PlayerHealth>();
        if (health != null)
            health.DamageTakenTyped += OnDamage;

        currentTint = color;
        BuildOverlay();
    }

    public override void OnNetworkDespawn()
    {
        if (health != null)
            health.DamageTakenTyped -= OnDamage;
        if (overlay != null)
            Destroy(overlay.canvas.gameObject);
    }

    private void OnDamage(float damage, DamageKind kind)
    {
        currentTint = kind == DamageKind.Acid ? acidColor : color;

        float hit = Mathf.Lerp(0.45f, maxAlpha, Mathf.Clamp01(damage / 40f));
        alpha = Mathf.Max(alpha, hit);
    }

    private void Update()
    {
        if (overlay == null) return;

        alpha = Mathf.MoveTowards(alpha, 0f, fadeSpeed * Time.deltaTime);
        Color c = currentTint;
        c.a = alpha;
        overlay.color = c;
    }

    private void BuildOverlay()
    {
        var canvasGo = new GameObject("DamageFeedbackCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var imgGo = new GameObject("DamageOverlay");
        imgGo.transform.SetParent(canvasGo.transform, false);
        overlay = imgGo.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.sprite = CreateVignetteSprite();
        overlay.color = new Color(1f, 1f, 1f, 0f);

        var rt = overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Borde opaco, casi transparente en el centro. Es blanca: el color se aplica en Image.color.
    private Sprite CreateVignetteSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x / (size - 1f), y / (size - 1f)) * 2f - Vector2.one;
                float d = Mathf.Clamp01(p.magnitude / 1.2f);
                float a = Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
