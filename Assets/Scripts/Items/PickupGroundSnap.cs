using UnityEngine;

// Baja pickups (armas, botiquines, municion) hasta el suelo con un raycast.
// Se ejecuta en cada cliente para objetos de escena: no hace falta NetworkTransform.
public static class PickupGroundSnap
{
    private const float RayStartHeight = 5f;
    private const float MaxRayDistance = 40f;
    private const float Clearance = 0.03f;

    public static Vector3 SnapPosition(Vector3 position, Transform self = null)
    {
        Vector3 origin = position + Vector3.up * RayStartHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            MaxRayDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return position;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (self != null && (hit.transform == self || hit.transform.IsChildOf(self)))
                continue;

            float bottomToPivot = 0f;
            if (self != null)
            {
                Collider[] cols = self.GetComponentsInChildren<Collider>();
                if (cols.Length > 0)
                {
                    Bounds bounds = cols[0].bounds;
                    for (int i = 1; i < cols.Length; i++)
                    {
                        if (cols[i] != null)
                            bounds.Encapsulate(cols[i].bounds);
                    }
                    bottomToPivot = position.y - bounds.min.y;
                }
            }

            return new Vector3(position.x, hit.point.y + bottomToPivot + Clearance, position.z);
        }

        return position;
    }

    public static void SnapToGround(Transform target)
    {
        if (target == null) return;
        target.position = SnapPosition(target.position, target);
    }
}
