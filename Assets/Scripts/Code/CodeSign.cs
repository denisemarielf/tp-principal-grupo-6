using TMPro;
using UnityEngine;

public class CodeSign : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color correctColor = new Color(1f, 0.5f, 0f); // naranja

    public void Setup(string code, bool isCorrect)
    {
        label.text = code;
        label.color = isCorrect ? correctColor : normalColor;
    }
}