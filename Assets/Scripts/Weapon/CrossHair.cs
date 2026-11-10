using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class CrossHair
{
    public RectTransform cross_Hair;

    [Header("CrossHair sizes")]
    [Tooltip("Tamaño con spread 0 (mira quieta y precisa). Ajustalo según tu sprite.")]
    [SerializeField] private float compact_Size = 40;
    [SerializeField] private float expanded_Size = 60;   // mínimo mientras rotás la cámara
    [SerializeField] private float sprint_Size = 110;    // mínimo mientras corrés
    [Tooltip("Tope de tamaño por dispersión (evita que la mira se vaya de pantalla).")]
    [SerializeField] private float max_Spread_Size = 220;
    [SerializeField] private float size_Change_Speed = 14;

    private float current_Size;
    private bool initialized;
    private Canvas rootCanvas;

    /// <param name="spreadScreenPixels">Dispersión actual convertida a píxeles de pantalla (semiancho).</param>
    public void UpdateCrossHairSize(float spreadScreenPixels, bool isSprinting)
    {
        if (cross_Hair == null) return;

        if (rootCanvas == null)
        {
            Canvas c = cross_Hair.GetComponentInParent<Canvas>();
            rootCanvas = c != null ? c.rootCanvas : null;
        }

        // sizeDelta está en unidades de canvas, no en píxeles de pantalla.
        float scale = rootCanvas != null && rootCanvas.scaleFactor > 0.01f ? rootCanvas.scaleFactor : 1f;
        float spreadSize = compact_Size + (spreadScreenPixels / scale) * 2f;
        spreadSize = Mathf.Min(spreadSize, max_Spread_Size);

        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        bool is_Rotating = mouseDelta.sqrMagnitude > 0.01f;

        float target_Size = spreadSize;
        if (isSprinting) target_Size = Mathf.Max(target_Size, sprint_Size);
        else if (is_Rotating) target_Size = Mathf.Max(target_Size, expanded_Size);

        if (!initialized)
        {
            current_Size = target_Size;
            initialized = true;
        }

        current_Size = Mathf.Lerp(current_Size, target_Size, size_Change_Speed * Time.deltaTime);
        cross_Hair.sizeDelta = new Vector2(current_Size, current_Size);
    }
}
