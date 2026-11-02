using UnityEngine;

[RequireComponent(typeof(QuickOutline.Outline))]
public class ObjectiveHighlight : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 0.55f, 0.1f);
    [SerializeField] private float width = 4f;

    private QuickOutline.Outline outline;

    private void Awake()
    {
        outline = GetComponent<QuickOutline.Outline>();
        outline.OutlineMode = QuickOutline.Outline.Mode.OutlineAll;
        outline.OutlineColor = color;
        outline.OutlineWidth = width;
        outline.enabled = false;
    }

    public void SetHighlighted(bool on) => outline.enabled = on;
}