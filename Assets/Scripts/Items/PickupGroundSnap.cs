using UnityEngine;

// Baja pickups (armas, botiquines, municion) hasta el suelo con un raycast.
// Preferible solo en el server: en clientes puede dejar el objeto bajo el piso.
public static class PickupGroundSnap
{
    private const float RayStartHeight = 2f;
    private const float MaxRayDistance = 40f;
    private const float Clearance = 0.03f;

    // Colliders que NO cuentan como piso: personajes, enemigos y otros pickups.
    // Sin esto, un loot soltado por un zombi se apoyaba sobre el propio zombi y quedaba flotando.
    private static bool IsNotGround(Collider c)
    {
        return c.GetComponentInParent<EnemyHealth>() != null
            || c.GetComponentInParent<PlayerHealth>() != null
            || c.GetComponentInParent<ItemPickup>() != null;
    }

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
            if (IsNotGround(hit.collider))
                continue;

            float bottomToPivot = 0f;
            if (self != null)
            {
                // Solo colliders solidos: un trigger grande (radio de uso) inflaria el calculo y lo dejaria en el aire.
                bool found = false;
                Bounds bounds = new Bounds(position, Vector3.zero);
                foreach (Collider col in self.GetComponentsInChildren<Collider>())
                {
                    if (col == null || col.isTrigger)
                        continue;

                    if (!found)
                    {
                        bounds = col.bounds;
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(col.bounds);
                    }
                }

                if (found)
                    bottomToPivot = Mathf.Max(0f, position.y - bounds.min.y);
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