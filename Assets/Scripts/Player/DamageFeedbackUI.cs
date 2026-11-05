using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Flash rojo (viñeta) cuando el jugador local recibe daño.
// Ponelo en el prefab del Player, en el mismo objeto que PlayerHealth.
public class DamageFeedbackUI : NetworkBehaviour
{
    [SerializeField] private Color color = new Color(0.8f, 0f, 0f, 1f);
    [SerializeField] private float maxAlpha = 0.85f;
    [SerializeField] private float fadeSpeed = 1.8f;   // mayor = se va mas rapido
    [SerializeField] private int sortingOrder = 20;    // bajalo si tapa tu GameOverUI

    private PlayerHealth health;
    private Image overlay;
    private float alpha;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { enabled = false; return; }

        health = GetComponent<PlayerHealth>();
        if (health != null)
            health.DamageTaken += OnDamage;

        BuildOverlay();
    }

    public override void OnNetworkDespawn()
    {
        if (health != null)
            health.DamageTaken -= OnDamage;
        if (overlay != null)
            Destroy(overlay.canvas.gameObject);
    }

    private void OnDamage(float damage)
    {
        float hit = Mathf.Lerp(0.45f, maxAlpha, Mathf.Clamp01(damage / 40f));
        alpha = Mathf.Max(alpha, hit);
    }

    private void Update()
    {
        if (overlay == null) return;

        alpha = Mathf.MoveTowards(alpha, 0f, fadeSpeed * Time.deltaTime);
        Color c = Color.white;
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

    // Rojo en los bordes, casi transparente en el centro.
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
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
