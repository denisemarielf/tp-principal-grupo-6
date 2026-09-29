using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class EnemySceneSetup : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;

    private void Start()
    {
        StartCoroutine(SetupWhenServer());
    }

    private IEnumerator SetupWhenServer()
    {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        if (!NetworkManager.Singleton.IsServer)
            yield break;

        BuildNavMesh();
        yield return null;

        Spawn(new Vector3(3f, 1f, 2f), EnemyVariant.Normal);
        Spawn(new Vector3(-2.5f, 1f, 3.5f), EnemyVariant.Resistente);
    }

    private void Spawn(Vector3 position, EnemyVariant variant)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySceneSetup: falta el prefab del enemigo.");
            return;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            position = hit.position + Vector3.up;

        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
            health.Configure(variant);

        NetworkObject netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
    }

    private void BuildNavMesh()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            Debug.LogError("EnemySceneSetup: no hay un mesh para armar el NavMesh.");
            return;
        }

        var sources = new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = filter.sharedMesh,
                transform = transform.localToWorldMatrix,
                area = 0
            }
        };

        Bounds bounds = new Bounds(transform.position, new Vector3(80f, 20f, 80f));
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByIndex(0),
            sources,
            bounds,
            transform.position,
            Quaternion.identity);

        if (data != null)
            NavMesh.AddNavMeshData(data);
    }
}
