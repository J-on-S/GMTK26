using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ClientTaskQueueEntry
{
    [SerializeField] private GameObject clientPrefab;
    [SerializeField] private ClientTask task;
    [SerializeField] private GameObject spawnedClient;
    [SerializeField] private OperationChair assignedChair;

    public GameObject ClientPrefab => clientPrefab;
    public ClientTask Task => task;
    public GameObject SpawnedClient => spawnedClient;
    public OperationChair AssignedChair => assignedChair;
    public string AssignedChairName =>
        assignedChair != null ? assignedChair.name : "Unassigned";
    public bool IsSpawned => spawnedClient != null;

    public ClientTaskQueueEntry(GameObject clientPrefab, ClientTask task)
    {
        this.clientPrefab = clientPrefab;
        this.task = task;
    }

    public void SetSpawnedClient(
        GameObject client,
        OperationChair chair = null)
    {
        spawnedClient = client;
        assignedChair = chair;
    }
}

/// <summary>
/// Pre-generates an in-memory list of client prefabs and their tasks.
/// Client GameObjects are only instantiated when a chair requests the next entry.
/// Completed clients are removed from both the active list and task list.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class RandomizedClientList : MonoBehaviour
{
    public static RandomizedClientList Instance { get; private set; }

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
    public event Action<OperationChair, GameObject> ClientSpawnedOnChair;
    public event Action<GameObject> ClientRemoved;
    public event Action<ClientTaskQueueEntry> TaskListEntryCreated;
    public event Action<ClientTaskQueueEntry> TaskListEntryRemoved;
    public event Action<ClientTaskQueueEntry, BodyPartType, int>
        TaskRequirementChanged;
    public event Action TaskListEmptied;

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

    /// <summary>
    /// Returns the name of the bed assigned when this entry was spawned.
    /// Pending entries return "Unassigned".
    /// </summary>
    public string GetAssignedChairName(ClientTaskQueueEntry entry)
    {
        return entry?.AssignedChairName ?? "Unassigned";
    }

    /// <summary>Returns display-ready request text for a generated entry.</summary>
    public string GetTaskString(ClientTaskQueueEntry entry)
    {
        return entry?.Task != null
            ? entry.Task.GetDialogue()
            : string.Empty;
    }

    /// <summary>Returns task text using the quantities still required.</summary>
    public string GetRemainingTaskString(ClientTaskQueueEntry entry)
    {
        return entry?.Task != null
            ? entry.Task.GetRemainingDialogue()
            : string.Empty;
    }

    public int GetRemainingAmount(
        ClientTaskQueueEntry entry,
        BodyPartType bodyPart)
    {
        return entry?.Task != null
            ? entry.Task.GetRemainingAmount(bodyPart)
            : 0;
    }

    /// <summary>
    /// Removes one required body part from a specific spawned client's task.
    /// Call this only after the doctor has accepted the delivered body part.
    /// </summary>
    public bool RemoveOneFromTask(
        GameObject targetClient,
        BodyPartType bodyPart)
    {
        if (targetClient == null)
        {
            Debug.LogWarning(
                "A target client is required when updating a task.",
                this);
            return false;
        }

        ClientTaskQueueEntry entry = FindEntry(targetClient);
        return RemoveOneFromTask(entry, bodyPart);
    }

    /// <summary>
    /// String overload for the current doctor-order API. Names are matched
    /// case-insensitively against BodyPartType values such as Eye or Heart.
    /// </summary>
    public bool RemoveOneFromTask(
        GameObject targetClient,
        string bodyPartName)
    {
        if (!Enum.TryParse(
                bodyPartName,
                true,
                out BodyPartType bodyPart))
        {
            Debug.LogWarning(
                $"'{bodyPartName}' is not a valid body-part name.",
                this);
            return false;
        }

        return RemoveOneFromTask(targetClient, bodyPart);
    }

    /// <summary>
    /// Removes one required body part from a specific generated task entry.
    /// This works for both pending and spawned entries.
    /// </summary>
    public bool RemoveOneFromTask(
        ClientTaskQueueEntry entry,
        BodyPartType bodyPart)
    {
        if (entry == null || !generatedTaskList.Contains(entry))
        {
            Debug.LogWarning(
                "Cannot update a task that is not in the generated client list.",
                this);
            return false;
        }

        bool accepted;

        if (entry.IsSpawned)
        {
            ClientTaskHolder holder =
                entry.SpawnedClient.GetComponent<ClientTaskHolder>();

            if (holder == null)
            {
                Debug.LogError(
                    $"{entry.SpawnedClient.name} has no ClientTaskHolder.",
                    entry.SpawnedClient);
                return false;
            }

            accepted = holder.GiveBodyPart(bodyPart);
        }
        else
        {
            accepted = entry.Task.TryDeliver(bodyPart);

            if (accepted && entry.Task.IsComplete)
                RemoveTaskListEntry(entry);
        }

        if (!accepted)
            return false;

        int remaining = entry.Task.GetRemainingAmount(bodyPart);
        TaskRequirementChanged?.Invoke(entry, bodyPart, remaining);
        return true;
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
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "Only one RandomizedClientList may exist in a scene. " +
                $"Keeping {Instance.name} and removing {name}.",
                this);
            Destroy(this);
            return;
        }

        Instance = this;
        ShuffleClients();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
    public GameObject SpawnNextClient(Transform spawnPose)
    {
        return SpawnNextClientInternal(spawnPose, null);
    }

    /// <summary>
    /// Spawns the next client using a chair's pose proxy and records that chair
    /// on the queue entry.
    /// </summary>
    public GameObject SpawnNextClient(OperationChair chair)
    {
        if (chair == null)
        {
            Debug.LogWarning(
                "Cannot spawn a client without an operation chair.",
                this);
            return null;
        }

        return SpawnNextClientInternal(chair.ClientPoseProxy, chair);
    }

    private GameObject SpawnNextClientInternal(
        Transform spawnPose,
        OperationChair assignedChair)
    {
        if (spawnPose == null)
        {
            Debug.LogWarning(
                "Cannot spawn a client without a spawn-pose transform.",
                this);
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

        // Keep the client unparented, then copy the proxy's complete world
        // pose. Because the clone has no parent, localScale is its world scale.
        GameObject clientObject = Instantiate(entry.ClientPrefab);
        Transform clientTransform = clientObject.transform;
        clientTransform.SetPositionAndRotation(
            spawnPose.position,
            spawnPose.rotation);
        clientTransform.localScale = spawnPose.lossyScale;

        ClientTaskHolder taskHolder = clientObject.GetComponent<ClientTaskHolder>();
        if (taskHolder == null)
        {
            Debug.LogError(
                $"{clientObject.name} needs a ClientTaskHolder component.",
                clientObject);
            Destroy(clientObject);
            return null;
        }

        entry.SetSpawnedClient(clientObject, assignedChair);
        activeClients.Add(clientObject);
        taskHolder.AssignTask(entry.Task);
        taskHolder.TaskCompletedWithOwner += HandleClientTaskCompleted;
        ClientSpawned?.Invoke(clientObject);
        if (assignedChair != null)
            ClientSpawnedOnChair?.Invoke(assignedChair, clientObject);
        return clientObject;
    }

    /// <summary>
    /// Backwards-compatible overload. The generator must be used to pre-generate
    /// the list before this function is called.
    /// </summary>
    public GameObject SpawnNextClient(
        Transform spawnPose,
        ClientTaskList generator)
    {
        if (taskGenerator == null)
            taskGenerator = generator;

        return SpawnNextClientInternal(spawnPose, null);
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
            RemoveTaskListEntry(entry);

        ClientRemoved?.Invoke(client);

        if (destroyClient)
            Destroy(client);

        return true;
    }

    private void HandleClientTaskCompleted(ClientTaskHolder holder, ClientTask task)
    {
        RemoveActiveClient(holder.gameObject);
    }

    private void RemoveTaskListEntry(ClientTaskQueueEntry entry)
    {
        if (entry == null || !generatedTaskList.Remove(entry))
            return;

        TaskListEntryRemoved?.Invoke(entry);

        if (generatedTaskList.Count == 0)
            TaskListEmptied?.Invoke();
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
