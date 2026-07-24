using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a shuffled queue of client prefabs and supplies each client once
/// before beginning a new randomized round.
/// </summary>
public class RandomizedClientList : MonoBehaviour
{
    [SerializeField] private List<GameObject> clientPrefabs = new();
    [SerializeField] private bool reshuffleWhenEmpty = true;

    private readonly List<GameObject> randomizedClients = new();
    private int nextClientIndex;
    private GameObject lastClient;

    public int RemainingClients => randomizedClients.Count - nextClientIndex;
    public bool HasNextClient => RemainingClients > 0 ||
                                 (reshuffleWhenEmpty && clientPrefabs.Count > 0);

    public event Action<GameObject> ClientSelected;

    private void Awake()
    {
        ShuffleClients();
    }

    /// <summary>
    /// Returns the next client prefab. Returns null when the list is empty.
    /// </summary>
    public GameObject GetNextClient()
    {
        if (nextClientIndex >= randomizedClients.Count)
        {
            if (!reshuffleWhenEmpty)
                return null;

            ShuffleClients();
        }

        if (randomizedClients.Count == 0)
            return null;

        GameObject client = randomizedClients[nextClientIndex++];
        lastClient = client;
        ClientSelected?.Invoke(client);
        return client;
    }

    /// <summary>
    /// Instantiates the next client at an empty operating chair.
    /// </summary>
    public GameObject SpawnNextClient(Transform chair)
    {
        if (chair == null)
        {
            Debug.LogWarning("Cannot spawn a client without an operating chair.", this);
            return null;
        }

        GameObject clientPrefab = GetNextClient();
        return clientPrefab == null
            ? null
            : Instantiate(clientPrefab, chair.position, chair.rotation, chair);
    }

    /// <summary>Spawns a client and gives that specific client an independent task.</summary>
    public GameObject SpawnNextClient(Transform chair, ClientTaskList taskList)
    {
        GameObject clientObject = SpawnNextClient(chair);
        if (clientObject == null || taskList == null)
            return clientObject;

        ClientTaskHolder taskHolder = clientObject.GetComponent<ClientTaskHolder>();
        if (taskHolder == null)
        {
            Debug.LogWarning($"{clientObject.name} needs a ClientTaskHolder component.", clientObject);
            return clientObject;
        }

        taskList.AssignRandomTask(taskHolder);
        return clientObject;
    }

    [ContextMenu("Shuffle Clients")]
    public void ShuffleClients()
    {
        randomizedClients.Clear();

        foreach (GameObject client in clientPrefabs)
        {
            if (client != null)
                randomizedClients.Add(client);
        }

        for (int i = randomizedClients.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (randomizedClients[i], randomizedClients[randomIndex]) =
                (randomizedClients[randomIndex], randomizedClients[i]);
        }

        // Do not show the same client twice at the boundary between rounds.
        if (randomizedClients.Count > 1 && randomizedClients[0] == lastClient)
        {
            int swapIndex = UnityEngine.Random.Range(1, randomizedClients.Count);
            (randomizedClients[0], randomizedClients[swapIndex]) =
                (randomizedClients[swapIndex], randomizedClients[0]);
        }

        nextClientIndex = 0;
    }
}
