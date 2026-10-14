using UnityEngine;
using System;

public class CameraRecoil : MonoBehaviour
{
    [Header("Hip Fire recoil")]
    [SerializeField] private Vector2 vertical_Recoil_Range = new Vector2(1.2f, 2);
    [SerializeField] private float horizontal_Recoil = 0.45f;
    [SerializeField] private float recoil_Roll = 0.15f;
    [Header("ADS Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float aiming_Recoil_Multiplier = 0.55f;
    [Header("Recoil Behaviour")]
    [SerializeField] private float recoil_Snapiness = 18f;
    [SerializeField] private float recoil_Return_Speed = 10;
    [SerializeField] private float max_Vetical_Recoil = 8;

    private Vector3 current_Recoil;
    private Vector3 target_Recoil;

    public void AddRecoil(bool is_aiming)
    {
        float recoil_multiplier = is_aiming ? aiming_Recoil_Multiplier : 1;
        float vertical_Kick = UnityEngine.Random.Range(vertical_Recoil_Range.x, vertical_Recoil_Range.y);

        float horizontal_Kick = UnityEngine.Random.Range(-horizontal_Recoil, horizontal_Recoil);
        float roll_Kick = UnityEngine.Random.Range(-recoil_Roll, recoil_Roll);
        target_Recoil += new Vector3(-vertical_Kick, horizontal_Kick, roll_Kick) * recoil_multiplier;

        target_Recoil.x = Mathf.Clamp(target_Recoil.x, -max_Vetical_Recoil, 0);

    }
    public Vector3 UpdateRecoil()
    {
        target_Recoil = Vector3.Lerp(target_Recoil, Vector3.zero, recoil_Return_Speed * Time.deltaTime);
        current_Recoil = Vector3.Lerp(current_Recoil, target_Recoil, recoil_Snapiness * Time.deltaTime);
        return current_Recoil;
    }

}
