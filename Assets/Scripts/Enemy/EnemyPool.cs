using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Object pooling de enemigos para Netcode.
//
// Poner UN componente EnemyPool en la escena de juego (el mismo en host y clientes).
// Registra un INetworkPrefabInstanceHandler por cada prefab de la lista de Network Prefabs que tenga EnemyHealth
// (zombie, miniboss, arana...). Asi:
//   - Cliente: cuando llega el spawn, Netcode pide la instancia al pool en vez de hacer Instantiate.
//   - Server : EnemySceneSetup pide la instancia con EnemyPool.Create(...) y despues hace netObj.Spawn(true).
//   - Ambos  : netObj.Despawn(true) ya NO destruye; Netcode llama a Destroy() del handler y el objeto vuelve al pool.
//
// Los objetos inactivos NO se parentean a nada: Netcode no permite reparentar un NetworkObject bajo un objeto sin NetworkObject.
public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance { get; private set; }

    [Tooltip("Instancias que se crean de antemano por prefab (evita el pico del primer spawn de una horda). 0 = ninguna.")]
    [SerializeField, Min(0)] private int prewarmPerPrefab = 0;

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, Handler> handlers = new Dictionary<GameObject, Handler>();
    private NetworkManager subscribedTo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        StartCoroutine(Subscribe());
    }

    private System.Collections.IEnumerator Subscribe()
    {
        while (NetworkManager.Singleton == null)
            yield return null;

        subscribedTo = NetworkManager.Singleton;
        subscribedTo.OnServerStarted += RegisterAll;
        subscribedTo.OnClientStarted += RegisterAll;
        subscribedTo.OnServerStopped += OnStopped;
        subscribedTo.OnClientStopped += OnStopped;

        if (subscribedTo.IsListening)
            RegisterAll();
    }

    private void OnDisable()
    {
        if (subscribedTo != null)
        {
            subscribedTo.OnServerStarted -= RegisterAll;
            subscribedTo.OnClientStarted -= RegisterAll;
            subscribedTo.OnServerStopped -= OnStopped;
            subscribedTo.OnClientStopped -= OnStopped;
        }

        UnregisterAll();
        if (Instance == this)
            Instance = null;
    }

    private void OnStopped(bool _)
    {
        UnregisterAll();
    }

    private void RegisterAll()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        foreach (NetworkPrefab entry in nm.NetworkConfig.Prefabs.Prefabs)
        {
            GameObject prefab = entry.Prefab;
            if (prefab == null || handlers.ContainsKey(prefab))
                continue;
            if (prefab.GetComponent<EnemyHealth>() == null)
                continue;

            var handler = new Handler(this, prefab);
            handlers.Add(prefab, handler);
            nm.PrefabHandler.AddHandler(prefab, handler);

            for (int i = 0; i < prewarmPerPrefab; i++)
            {
                GameObject go = Instantiate(prefab);
                go.SetActive(false);
                QueueFor(prefab).Enqueue(go);
            }
        }
    }

    private void UnregisterAll()
    {
        NetworkManager nm = NetworkManager.Singleton;
        foreach (GameObject prefab in handlers.Keys)
        {
            if (nm != null && prefab != null)
                nm.PrefabHandler.RemoveHandler(prefab);
        }

        handlers.Clear();
        pools.Clear();
    }

    private Queue<GameObject> QueueFor(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pools.Add(prefab, queue);
        }

        return queue;
    }

    private GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Queue<GameObject> queue = QueueFor(prefab);
        GameObject go = null;

        // Si cambio la escena, los inactivos pudieron destruirse: se saltean.
        while (go == null && queue.Count > 0)
            go = queue.Dequeue();

        if (go == null)
            return Instantiate(prefab, position, rotation);

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    private void Release(GameObject prefab, GameObject go)
    {
        if (go == null)
            return;

        go.SetActive(false);
        QueueFor(prefab).Enqueue(go);
    }

    // Lo usa el server en lugar de Instantiate. Si el prefab no esta registrado en el pool, instancia normal.
    public static GameObject Create(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (Instance != null && Instance.handlers.ContainsKey(prefab))
            return Instance.Get(prefab, position, rotation);

        return Instantiate(prefab, position, rotation);
    }

    private class Handler : INetworkPrefabInstanceHandler
    {
        private readonly EnemyPool pool;
        private readonly GameObject prefab;

        public Handler(EnemyPool pool, GameObject prefab)
        {
            this.pool = pool;
            this.prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            return pool.Get(prefab, position, rotation).GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            pool.Release(prefab, networkObject.gameObject);
        }
    }
}
