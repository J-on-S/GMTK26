using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ClientTaskQueueEntry
{
    [SerializeField] private GameObject clientPrefab;
    [SerializeField] private ClientTask task;
    [SerializeField] private GameObject spawnedClient;

    public GameObject ClientPrefab => clientPrefab;
    public ClientTask Task => task;
    public GameObject SpawnedClient => spawnedClient;
    public bool IsSpawned => spawnedClient != null;

    public ClientTaskQueueEntry(GameObject clientPrefab, ClientTask task)
    {
        this.clientPrefab = clientPrefab;
        this.task = task;
    }

    public void SetSpawnedClient(GameObject client)
    {
        spawnedClient = client;
    }
}

/// <summary>
/// Pre-generates an in-memory list of client prefabs and their tasks.
/// Client GameObjects are only instantiated when a chair requests the next entry.
/// Completed clients are removed from both the active list and task list.
/// </summary>
public class RandomizedClientList : MonoBehaviour
{
    [Header("Client prefabs")]
    [SerializeField] private List<GameObject> clientPrefabs = new();
    [SerializeField] private bool reshuffleWhenEmpty = true;

    [Header("Pre-generated task list")]
    [SerializeField] private ClientTaskList taskGenerator;
    [SerializeField, Min(1)] private int clientsToPrepare = 6;
    [SerializeField] private bool useHandMadeTasks = true;
    [SerializeField] private bool prepareOnStart = true;
    [SerializeField] private List<ClientTaskQueueEntry> generatedTaskList = new();

    [Header("Runtime")]
    [SerializeField] private List<GameObject> activeClients = new();

    private readonly List<GameObject> randomizedClients = new();
    private int nextClientIndex;
    private GameObject lastClientPrefab;

    public IReadOnlyList<ClientTaskQueueEntry> GeneratedTaskList => generatedTaskList;
    public IReadOnlyList<GameObject> ActiveClients => activeClients;
    public int TaskListCount => generatedTaskList.Count;
    public int ActiveClientCount => activeClients.Count;
    public int ClientsToPrepare => clientsToPrepare;
    public int PendingClientCount
    {
        get
        {
            int count = 0;
            foreach (ClientTaskQueueEntry entry in generatedTaskList)
            {
                if (!entry.IsSpawned)
                    count++;
            }
            return count;
        }
    }

    public event Action<GameObject> ClientSelected;
    public event Action<GameObject> ClientSpawned;
    public event Action<GameObject> ClientRemoved;
    public event Action<ClientTaskQueueEntry> TaskListEntryCreated;
    public event Action<ClientTaskQueueEntry> TaskListEntryRemoved;

    // ---------------------------------------------------------------------
    // Simple game-facing API
    // ---------------------------------------------------------------------

    /// <summary>Returns the current pre-generated client/task list.</summary>
    public IReadOnlyList<ClientTaskQueueEntry> GetGeneratedList()
    {
        return generatedTaskList;
    }

    /// <summary>Returns the prefab name stored by a generated list entry.</summary>
    public string GetPersonName(ClientTaskQueueEntry entry)
    {
        return entry?.ClientPrefab != null
            ? entry.ClientPrefab.name
            : string.Empty;
    }

    /// <summary>Returns display-ready request text for a generated entry.</summary>
    public string GetTaskString(ClientTaskQueueEntry entry)
    {
        return entry?.Task != null
            ? entry.Task.GetDialogue()
            : string.Empty;
    }

    /// <summary>Generates the configured list without spawning any clients.</summary>
    public void GenerateList()
    {
        PregenerateClientTasks();
    }

    /// <summary>Generates a specific number of entries without spawning clients.</summary>
    public void GenerateList(int amount, ClientTaskList generator)
    {
        PregenerateClientTasks(amount, generator);
    }

    /// <summary>
    /// Removes a spawned person from the generated list and destroys its GameObject.
    /// </summary>
    public bool DespawnPerson(GameObject person)
    {
        return RemoveActiveClient(person);
    }

    private void Awake()
    {
        ShuffleClients();
    }

    private void Start()
    {
        if (prepareOnStart)
            PregenerateClientTasks();
    }

    /// <summary>
    /// Generates the configured number of client/task pairs without spawning them.
    /// </summary>
    [ContextMenu("Pregenerate Client Task List")]
    public void PregenerateClientTasks()
    {
        PregenerateClientTasks(clientsToPrepare, taskGenerator);
    }

    public void PregenerateClientTasks(ClientTaskList generator)
    {
        PregenerateClientTasks(clientsToPrepare, generator);
    }

    public void PregenerateClientTasks(int count, ClientTaskList generator)
    {
        if (activeClients.Count > 0)
        {
            Debug.LogWarning(
                "Cannot replace the task list while spawned clients are active.",
                this);
            return;
        }

        if (generator == null)
        {
            Debug.LogWarning("Assign a ClientTaskList task generator.", this);
            return;
        }

        taskGenerator = generator;
        generatedTaskList.Clear();

        int safeCount = Mathf.Max(0, count);
        for (int i = 0; i < safeCount; i++)
        {
            GameObject clientPrefab = GetNextClientPrefab();
            ClientTask task = generator.CreateTask(useHandMadeTasks);

            if (clientPrefab == null || task == null)
                break;

            ClientTaskQueueEntry entry = new(clientPrefab, task);
            generatedTaskList.Add(entry);
            TaskListEntryCreated?.Invoke(entry);
        }

        Debug.Log(
            $"Prepared {generatedTaskList.Count} clients and tasks without spawning them.",
            this);
    }

    /// <summary>
    /// Spawns the first pending pre-generated entry and assigns its existing task.
    /// No task generation happens here.
    /// </summary>
    public GameObject SpawnNextClient(Transform chair)
    {
        if (chair == null)
        {
            Debug.LogWarning("Cannot spawn a client without an operation chair.", this);
            return null;
        }

        ClientTaskQueueEntry entry = GetNextPendingEntry();
        if (entry == null)
        {
            Debug.LogWarning(
                "The pre-generated client task list has no pending clients.",
                this);
            return null;
        }

        // The chair supplies position and rotation only. Keeping the client
        // unparented prevents a scaled chair hierarchy from changing its size.
        GameObject clientObject = Instantiate(
            entry.ClientPrefab,
            chair.position,
            chair.rotation);

        ClientTaskHolder taskHolder = clientObject.GetComponent<ClientTaskHolder>();
        if (taskHolder == null)
        {
            Debug.LogError(
                $"{clientObject.name} needs a ClientTaskHolder component.",
                clientObject);
            Destroy(clientObject);
            return null;
        }

        entry.SetSpawnedClient(clientObject);
        activeClients.Add(clientObject);
        taskHolder.AssignTask(entry.Task);
        taskHolder.TaskCompletedWithOwner += HandleClientTaskCompleted;
        ClientSpawned?.Invoke(clientObject);
        return clientObject;
    }

    /// <summary>
    /// Backwards-compatible overload. The generator must be used to pre-generate
    /// the list before this function is called.
    /// </summary>
    public GameObject SpawnNextClient(Transform chair, ClientTaskList generator)
    {
        if (taskGenerator == null)
            taskGenerator = generator;

        return SpawnNextClient(chair);
    }

    /// <summary>
    /// Removes the exact spawned client and its associated task-list entry.
    /// </summary>
    public bool RemoveActiveClient(GameObject client, bool destroyClient = true)
    {
        if (client == null)
            return false;

        ClientTaskQueueEntry entry = FindEntry(client);
        bool wasActive = activeClients.Remove(client);

        if (!wasActive && entry == null)
            return false;

        ClientTaskHolder holder = client.GetComponent<ClientTaskHolder>();
        if (holder != null)
            holder.TaskCompletedWithOwner -= HandleClientTaskCompleted;

        if (entry != null)
        {
            generatedTaskList.Remove(entry);
            TaskListEntryRemoved?.Invoke(entry);
        }

        ClientRemoved?.Invoke(client);

        if (destroyClient)
            Destroy(client);

        return true;
    }

    private void HandleClientTaskCompleted(ClientTaskHolder holder, ClientTask task)
    {
        RemoveActiveClient(holder.gameObject);
    }

    private ClientTaskQueueEntry GetNextPendingEntry()
    {
        foreach (ClientTaskQueueEntry entry in generatedTaskList)
        {
            if (!entry.IsSpawned)
                return entry;
        }

        return null;
    }

    private ClientTaskQueueEntry FindEntry(GameObject spawnedClient)
    {
        foreach (ClientTaskQueueEntry entry in generatedTaskList)
        {
            if (entry.SpawnedClient == spawnedClient)
                return entry;
        }

        return null;
    }

    private GameObject GetNextClientPrefab()
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
        lastClientPrefab = client;
        ClientSelected?.Invoke(client);
        return client;
    }

    [ContextMenu("Shuffle Client Prefabs")]
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

        if (randomizedClients.Count > 1 &&
            randomizedClients[0] == lastClientPrefab)
        {
            int swapIndex = UnityEngine.Random.Range(1, randomizedClients.Count);
            (randomizedClients[0], randomizedClients[swapIndex]) =
                (randomizedClients[swapIndex], randomizedClients[0]);
        }

        nextClientIndex = 0;
    }
}
