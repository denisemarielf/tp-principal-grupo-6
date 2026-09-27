using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class CrossHair
{
    public RectTransform cross_Hair;

    [Header("CrossHair sizes")]
    [SerializeField] private float compact_Size = 70;
    [SerializeField] private float expanded_Size = 90;
    [SerializeField] private float max_Size = 110;
    [SerializeField] private float size_Change_Speed = 10;

    private float current_Size;

    public void UpdateCrossHairSize(bool isSprinting, bool isWalking, bool isShooting)
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        bool is_Rotating = mouseDelta.sqrMagnitude > 0.01f;

        float target_Size;

        if (!isSprinting && isWalking || is_Rotating)
        {
            target_Size = expanded_Size;
        }
        else if (isSprinting && isShooting)
        {
            target_Size = max_Size;
        }
        else
        {
            target_Size = compact_Size;
        }

        current_Size = Mathf.Lerp(current_Size, target_Size, size_Change_Speed * Time.deltaTime);
        cross_Hair.sizeDelta = new Vector2(current_Size, current_Size);
    }
}