using UnityEngine;
using UnityEngine.Animations.Rigging;


public class ArmsGripController : MonoBehaviour
{
    [Header("Constraints a re-targetear")]
    public TwoBoneIKConstraint rightHandConstraint;
    public TwoBoneIKConstraint leftHandConstraint; // opcional, para armas a dos manos

    [Header("Nombre del hijo que cada arma debe tener")]
    public string rightGripChildName = "RightHandGrip";
    public string leftGripChildName = "LeftHandGrip"; // opcional


    /// <summary>Llamado por WeaponSwitcher cada vez que se equipa un arma.</summary>
    public void SetGripsForWeapon(GameObject weapon)
    {
        if (weapon == null)
        {
            
            return;
        }

        Transform rightGrip = FindDeepChild(weapon.transform, rightGripChildName);
        
        SetTarget(rightHandConstraint, rightGrip);

        if (leftHandConstraint != null)
        {
            SetTarget(leftHandConstraint, FindDeepChild(weapon.transform, leftGripChildName));
        }
    }

    /// <summary>Busca un hijo por nombre en toda la jerarquía del arma (a cualquier profundidad),
    /// ya que el grip puede estar anidado dentro del mesh visual (ej. WeaponModel) y no ser un hijo directo.</summary>
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

    private void SetTarget(TwoBoneIKConstraint constraint, Transform target)
    {
        if (constraint == null)
        {
           
            return;
        }

        if (target == null)
        {
          
            // Esta arma no usa esta mano (ej. pistola a una mano): apagamos el
            // constraint para que el brazo vuelva a su pose de reposo, en vez
            // de quedar estirado hacia el último target que tuvo.
            constraint.weight = 0f;
            return;
        }

        
        constraint.weight = 1f;

        // TwoBoneIKConstraintData es un struct: hay que copiarlo, modificarlo
        // y reasignarlo entero, no se puede escribir constraint.data.target directo.
        var data = constraint.data;
        data.target = target;
        constraint.data = data;
    }
}