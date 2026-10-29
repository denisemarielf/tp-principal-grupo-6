using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Deja armas, cajas de balas y botiquines alrededor del refugio. Solo el server los crea.
[RequireComponent(typeof(NetworkObject))]
public class SurvivalLootSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject[] weaponPrefabs;
    [SerializeField] private GameObject ammoPrefab;
    [SerializeField] private GameObject medkitPrefab;
    [SerializeField] private float weaponRadius = 14f;
    [SerializeField] private float ammoRadius = 20f;
    [SerializeField] private float medkitRadius = 10f;
    [SerializeField] private int ammoCount = 4;
    [SerializeField] private int medkitCount = 4;
    [SerializeField] private float medkitRespawnSeconds = 15f;

    private const float MinSeparation = 3.2f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        var used = new List<Vector3>();
        SpawnWeapons(used);
        SpawnAmmo(used);
        SpawnMedkits(used);
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
    }

    private void SpawnWeapons(List<Vector3> used)
    {
        if (weaponPrefabs == null)
            return;

        int count = 0;
        for (int i = 0; i < weaponPrefabs.Length; i++)
        {
            if (weaponPrefabs[i] != null)
                count++;
        }

        if (count == 0)
            return;

        int index = 0;
        for (int i = 0; i < weaponPrefabs.Length; i++)
        {
            GameObject prefab = weaponPrefabs[i];
            if (prefab == null)
                continue;

            if (!TryPoint(weaponRadius, index, count, 0.4f, used, out Vector3 point))
            {
                Debug.LogWarning("SurvivalLootSpawner: no pude apoyar un arma en el NavMesh.");
                index++;
                continue;
            }

            used.Add(point);
            SpawnWeapon(prefab, point);
            index++;
        }
    }

    private void SpawnAmmo(List<Vector3> used)
    {
        if (ammoPrefab == null || ammoCount <= 0)
            return;

        for (int i = 0; i < ammoCount; i++)
        {
            if (!TryPoint(ammoRadius, i, ammoCount, 1.1f, used, out Vector3 point))
            {
                Debug.LogWarning("SurvivalLootSpawner: no pude apoyar una caja de balas en el NavMesh.");
                continue;
            }

            used.Add(point);
            SpawnNetworked(ammoPrefab, point);
        }
    }

    private void SpawnMedkits(List<Vector3> used)
    {
        if (medkitPrefab == null || medkitCount <= 0)
            return;

        for (int i = 0; i < medkitCount; i++)
        {
            if (!TryPoint(medkitRadius, i, medkitCount, 0f, used, out Vector3 point))
            {
                Debug.LogWarning("SurvivalLootSpawner: no pude apoyar un botiquin en el NavMesh.");
                continue;
            }

            used.Add(point);
            SpawnMedkit(point);
        }
    }

    private void SpawnWeapon(GameObject prefab, Vector3 point)
    {
        GameObject spawned = SpawnNetworked(prefab, point);
        if (spawned == null)
            return;

        Rigidbody body = spawned.GetComponentInChildren<Rigidbody>();
        if (body == null)
            return;

        body.useGravity = false;
        body.isKinematic = true;
    }

    private void SpawnMedkit(Vector3 point)
    {
        GameObject spawned = SpawnNetworked(medkitPrefab, point);
        if (spawned == null)
            return;

        MedkitPickup medkit = spawned.GetComponent<MedkitPickup>();
        if (medkit == null)
            return;

        Vector3 respawnPoint = spawned.transform.position;
        medkit.Collected += _ => StartCoroutine(RespawnMedkit(respawnPoint));
    }

    private IEnumerator RespawnMedkit(Vector3 point)
    {
        yield return new WaitForSeconds(medkitRespawnSeconds);

        if (!IsServer || !IsSpawned)
            yield break;

        SurvivalModeController mode = SurvivalModeController.Current;
        if (mode != null && mode.IsFinished)
            yield break;

        SpawnMedkit(point);
    }

    private GameObject SpawnNetworked(GameObject prefab, Vector3 point)
    {
        GameObject spawned = Instantiate(prefab, point, Quaternion.identity);
        PickupGroundSnap.SnapToGround(spawned.transform);

        NetworkObject netObj = spawned.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("SurvivalLootSpawner: el prefab no tiene NetworkObject.", prefab);
            Destroy(spawned);
            return null;
        }

        netObj.Spawn(true);
        return spawned;
    }

    // Anillo sobre el NavMesh, separado del resto del loot. phase desfasa cada tipo para que no compartan el mismo radio visual.
    private bool TryPoint(float radius, int index, int count, float phase, List<Vector3> used, out Vector3 point)
    {
        int safeCount = Mathf.Max(1, count);
        for (int ring = 0; ring < 6; ring++)
        {
            float distance = radius + ring * 2.2f;
            float angle = (Mathf.PI * 2f * index) / safeCount + phase + ring * 0.35f;
            Vector3 desired = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                continue;

            point = hit.position;
            if (!TooClose(point, used))
                return true;
        }

        point = transform.position;
        return false;
    }

    private static bool TooClose(Vector3 point, List<Vector3> used)
    {
        foreach (Vector3 other in used)
        {
            Vector3 flat = point - other;
            flat.y = 0f;
            if (flat.sqrMagnitude < MinSeparation * MinSeparation)
                return true;
        }

        return false;
    }
}
