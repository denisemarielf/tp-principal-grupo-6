using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Effects/Heal")]
public class HealEffect : ItemEffect
{
    [SerializeField] private float amount = 25f;

    public override bool Apply(GameObject user)
    {
        var health = user.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead || health.IsFull) return false;

        health.Heal(amount);
        return true;
    }
}