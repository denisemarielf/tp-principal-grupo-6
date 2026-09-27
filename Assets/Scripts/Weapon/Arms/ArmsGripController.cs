using UnityEngine;
using UnityEngine.Animations.Rigging;


public class ArmsGripController : MonoBehaviour
{
    [Header("Constraints a re-targetear")]
    public TwoBoneIKConstraint rightHandConstraint;
    public TwoBoneIKConstraint leftHandConstraint;

    [Header("Nombre del hijo que cada arma debe tener")]
    public string rightGripChildName = "RightHandGrip";
    public string leftGripChildName = "LeftHandGrip";

    [Header("Rig Builder (para forzar reconstrucción tras retargetear)")]
    public RigBuilder rigBuilder; // NUEVO: arrastrá el que está en ArmsSoldier
    public Transform leftHandRestPosition;

    public void SetGripsForWeapon(GameObject weapon)
    {
        if (weapon == null)
        {
            return;
        }

        Transform rightGrip = FindDeepChild(weapon.transform, rightGripChildName);
        SetTarget(rightHandConstraint, rightGrip, null);

        if (leftHandConstraint != null)
        {
            Transform leftGrip = FindDeepChild(weapon.transform, leftGripChildName);
            SetTarget(leftHandConstraint, leftGrip, leftHandRestPosition); // NUEVO: pasa el fallback
        }

        if (rigBuilder != null)
        {
            rigBuilder.Build();
        }
    }

    private void SetTarget(TwoBoneIKConstraint constraint, Transform target, Transform restFallback)
    {
        if (constraint == null) return;

        // Si no hay grip para esta mano, usamos la posición de descanso en vez de apagar el constraint.
        Transform finalTarget = target != null ? target : restFallback;

        if (finalTarget == null)
        {
            constraint.weight = 0f;
            return;
        }

        constraint.weight = 1f;
        var data = constraint.data;
        data.target = finalTarget;
        constraint.data = data;
    }
    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;

            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
}