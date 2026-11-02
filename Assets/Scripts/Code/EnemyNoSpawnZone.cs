using System.Collections.Generic;
using UnityEngine;

// Zona donde el AIDirector NUNCA hace aparecer enemigos (por ejemplo la calle).
// Es una caja que se mueve, rota y escala con el objeto. En la Scene view se ve en rojo.
// Ojo con la altura: si la caja es muy alta y cubre un edificio, tambien bloquea sus interiores.
public class EnemyNoSpawnZone : MonoBehaviour
{
    private static readonly List<EnemyNoSpawnZone> All = new List<EnemyNoSpawnZone>();

    [Tooltip("Tamano de la caja en metros (espacio local del objeto).")]
    [SerializeField] private Vector3 size = new Vector3(30f, 6f, 30f);

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public static bool IsBlocked(Vector3 point)
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            EnemyNoSpawnZone zone = All[i];
            if (zone == null)
            {
                All.RemoveAt(i);
                continue;
            }

            if (zone.Contains(point))
                return true;
        }

        return false;
    }

    private bool Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        Vector3 half = size * 0.5f;
        return Mathf.Abs(local.x) <= half.x
            && Mathf.Abs(local.y) <= half.y
            && Mathf.Abs(local.z) <= half.z;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}