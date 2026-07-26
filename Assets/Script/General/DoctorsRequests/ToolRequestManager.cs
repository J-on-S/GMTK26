using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class ToolRequestManager : MonoBehaviour
{
    [System.Serializable]
    public struct ToolRequest
    {
        public string itemName;
        public ItemType itemType;
        public float timeLimit;
        public GameObject targetClient;
        public OperationChair targetChair;
    }

    // request stuff
    public List<ToolRequest> availableRequests = new List<ToolRequest>();   // this contains the active doctor's requests
    public List<ToolRequest> allTools = new List<ToolRequest>();            // this contains the list of ALL TOOLS that will be used to randomly select items for the doctor's requests
    public float timeBetweenRequests = 5f; // oooldown before next order from doctor
    public float numberOfRequests = 5;  // number of total requests that the doctor will ask for

    // states
    private enum State{Idle, ActiveRequest, Cooldown}
    [Header("Runtime debug")]
    [SerializeField] private State currentState = State.Idle;

    [SerializeField] private ToolRequest currentRequest;
    [SerializeField] private float remainingTime;
    [SerializeField] private float remainingCooldown;
    private RandomizedClientList subscribedClientList;
    private readonly HashSet<GameObject> clientsWithRequests = new();
    private bool isCompletingRequest;

    public event Action<ToolRequest> RequestStarted;
    public event Action<ToolRequest> RequestCompleted;
    public event Action<ToolRequest> RequestFailed;
    public event Action RequestQueueEmptied;

    // stuff for doctor request UI
    public TextMeshProUGUI myTextLabel;

    private void OnEnable()
    {
        SubscribeToClientList();
    }

    // request sound
    public PlayerHitSound confusedDoctor;

    private void Start()
    {
        SubscribeToClientList();
        RegisterAlreadySpawnedClients();
        StartCooldown();
    }

    private void OnDisable()
    {
        if (subscribedClientList == null)
            return;

        subscribedClientList.ClientSpawnedOnChair -= HandleClientSpawned;
        subscribedClientList.ClientRemoved -= HandleClientRemoved;
        subscribedClientList = null;
    }

    // Update is called once per frame

    void Update()
    {
        switch (currentState) 
        {
            case State.ActiveRequest:
                HandleActiveRequest();
                break;

            case State.Cooldown:
                HandleCooldown();
                break;

            case State.Idle:
                // just idle
                break;
        }
    }

    public void AddToDoctorRequestList(string bodyPartName)
    {
        AddToDoctorRequestList(bodyPartName, null, null);
    }

    public void AddToDoctorRequestList(
        string bodyPartName,
        GameObject targetClient,
        OperationChair targetChair)
    {
        availableRequests.Add(new ToolRequest
        {
            itemName = bodyPartName,
            itemType = ItemType.BodyPart,
            timeLimit = UnityEngine.Random.Range(6f, 9f),
            targetClient = targetClient,
            targetChair = targetChair
        });
    }

    public void FinishDoctorRequestList()
    {
        int minimumQueueSize =
            Mathf.Max(0, Mathf.RoundToInt(numberOfRequests));
        if (availableRequests.Count >= minimumQueueSize)
            return;

        if (allTools.Count == 0)
        {
            Debug.LogError(
                "ToolRequestManager cannot fill the queue because All Tools is empty.",
                this);
            return;
        }

        while (availableRequests.Count < minimumQueueSize)
        {
            int choice = Random.Range(0, allTools.Count);
            ToolRequest toolRequest = allTools[choice];
            toolRequest.targetClient = null;
            toolRequest.targetChair = null;
            availableRequests.Add(toolRequest);
        }
    }

    /// <summary>
    /// Adds every remaining body-part requirement for one spawned client.
    /// Requests retain the exact client and bed that they belong to.
    /// </summary>
    public void BuildRequestsForClient(
        OperationChair chair,
        GameObject client)
    {
        if (chair == null || client == null)
        {
            Debug.LogWarning(
                "A chair and spawned client are required to build doctor requests.",
                this);
            return;
        }

        if (!clientsWithRequests.Add(client))
            return;

        ClientTaskHolder holder = client.GetComponent<ClientTaskHolder>();
        if (holder == null || holder.AssignedTask == null)
        {
            clientsWithRequests.Remove(client);
            Debug.LogError(
                $"{client.name} needs ClientTaskHolder with an assigned task.",
                client);
            return;
        }

        ClientTask task = holder.AssignedTask;
        HashSet<BodyPartType> addedTypes = new();

        foreach (BodyPartRequest request in task.Requests)
        {
            if (!addedTypes.Add(request.BodyPart))
                continue;

            int remaining = task.GetRemainingAmount(request.BodyPart);
            for (int i = 0; i < remaining; i++)
            {
                AddToDoctorRequestList(
                    request.BodyPart.ToString(),
                    client,
                    chair);
            }
        }

        FinishDoctorRequestList();
        Debug.Log(
            $"Added doctor requests for {client.name} on {chair.name}. " +
            $"Queue count: {availableRequests.Count}.",
            this);
    }

    // countdown and then failure of request if not fulfilled within time
    private void HandleActiveRequest()
    {
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0)
            {
                FailRequest();
            }   
    }

    // cooldown between requests
    private void HandleCooldown()
    {
        remainingCooldown -= Time.deltaTime;
        if (remainingCooldown <= 0)
        {
            StartNewRandomRequest();
        }
    }

    // randomize the requests
    public void StartNewRandomRequest()
    {
        if (availableRequests.Count == 0)
        {
            currentState = State.Idle;
            remainingTime = 0f;
            RequestQueueEmptied?.Invoke();
            Debug.Log("The doctor request queue is empty.", this);
            return;
        }

        int index = Random.Range(0, availableRequests.Count);
        currentRequest = availableRequests[index];
        remainingTime = availableRequests[index].timeLimit;
        currentState = State.ActiveRequest;

        string itemCategory = currentRequest.itemType.ToString();
        string chairText = currentRequest.targetChair != null
            ? $" for {currentRequest.targetChair.name}"
            : string.Empty;
        Debug.Log(
            $"Hey, hand me a {itemCategory}: [{currentRequest.itemName}]" +
            $"{chairText} within {remainingTime:F1} seconds!",
            this);

        if (myTextLabel != null)
        {
            myTextLabel.text =
                "Hey, hand me a " + itemCategory +
                ":<color=\"red\"> " + currentRequest.itemName + "</color>" +
                chairText;
        }

       confusedDoctor.playAudio();
        availableRequests.Remove(currentRequest);
        RequestStarted?.Invoke(currentRequest);
        Debug.Log(
            $"Doctor requests remaining in queue: {availableRequests.Count}.",
            this);
    }

    // check if player submitted the tool correctly, returns true if correctly submitted
    public bool PlayerSubmittedTool(string submittedName, ItemType submittedType)
    {
        if (currentState != State.ActiveRequest)
            return false;

        if (submittedName == currentRequest.itemName && submittedType == currentRequest.itemType)
        {
            Debug.Log("Dude thanks for giving me that.");
            if (myTextLabel != null)
                myTextLabel.text = "Dude thanks for giving me that.";

            CompleteActiveRequest();
            return true;
        }
        else
        {   
            HealthScript.Instance.TakeDamage();
            Debug.Log($"Nah man wrong tool. I needed {currentRequest.itemType} named {currentRequest.itemName}, but you gave me {submittedType} named {submittedName}.");
            if (myTextLabel != null)
                myTextLabel.text = "Nah man wrong tool.";
            return false;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Force Complete Active Request")]
    private void DebugForceCompleteActiveRequest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before completing a doctor request.",
                this);
            return;
        }

        if (currentState != State.ActiveRequest)
        {
            Debug.LogWarning(
                "The doctor has no active request to complete.",
                this);
            return;
        }

        Debug.Log(
            $"Debug-completed doctor request: " +
            $"{currentRequest.itemType} [{currentRequest.itemName}].",
            this);

        if (myTextLabel != null)
            myTextLabel.text = "Debug-completed request.";

        CompleteActiveRequest();
    }
#endif

    private void CompleteActiveRequest()
    {
        ToolRequest completedRequest = currentRequest;
        isCompletingRequest = true;

        if (completedRequest.itemType == ItemType.BodyPart)
        {
            ApplyBodyPartRequest(completedRequest);

            if (completedRequest.targetClient != null)
            {
                SpawnBodyPartCustomer bodyPartVisual =
                    completedRequest.targetClient
                        .GetComponent<SpawnBodyPartCustomer>();
                bodyPartVisual?.AddBodyPart(completedRequest.itemName);
            }
        }

        isCompletingRequest = false;
        remainingTime = 0f;
        RequestCompleted?.Invoke(completedRequest);
        StartCooldown();
    }

    private void ApplyBodyPartRequest(ToolRequest request)
    {
        if (request.targetClient == null)
        {
            Debug.LogWarning(
                $"Body-part request [{request.itemName}] has no target client.",
                this);
            return;
        }

        RandomizedClientList clientList = RandomizedClientList.Instance;
        if (clientList == null ||
            !clientList.RemoveOneFromTask(
                request.targetClient,
                request.itemName))
        {
            Debug.LogWarning(
                $"Could not apply [{request.itemName}] to its target client.",
                this);
        }
    }

    private void FailRequest()
    {
        ToolRequest failedRequest = currentRequest;
        Debug.Log("Time is up! You failed the request.");
        if (myTextLabel != null)
            myTextLabel.text = "Ran out of time";

        // A failed body-part request must remain available or the associated
        // patient could become impossible to complete.
        if (failedRequest.itemType == ItemType.BodyPart &&
            failedRequest.targetClient != null)
        {
            availableRequests.Add(failedRequest);
        }

        remainingTime = 0f;
        RequestFailed?.Invoke(failedRequest);
        HealthScript.Instance.TakeDamage();
        StartCooldown();
    }

    // deals with cooldown state and timer
    private void StartCooldown()
    {
        remainingCooldown = timeBetweenRequests;
        currentState = State.Cooldown;
        Debug.Log($"Waiting for the next request. Cooldown active for {timeBetweenRequests} seconds");
    }

    public float timeRemaining()
    {
        return remainingTime;
    }

    private void SubscribeToClientList()
    {
        RandomizedClientList clientList = RandomizedClientList.Instance;
        if (clientList == null || subscribedClientList == clientList)
            return;

        if (subscribedClientList != null)
        {
            subscribedClientList.ClientSpawnedOnChair -= HandleClientSpawned;
            subscribedClientList.ClientRemoved -= HandleClientRemoved;
        }

        subscribedClientList = clientList;
        subscribedClientList.ClientSpawnedOnChair += HandleClientSpawned;
        subscribedClientList.ClientRemoved += HandleClientRemoved;
    }

    private void RegisterAlreadySpawnedClients()
    {
        if (subscribedClientList == null)
            return;

        foreach (ClientTaskQueueEntry entry in
                 subscribedClientList.GetGeneratedList())
        {
            if (entry.IsSpawned && entry.AssignedChair != null)
            {
                BuildRequestsForClient(
                    entry.AssignedChair,
                    entry.SpawnedClient);
            }
        }
    }

    private void HandleClientSpawned(
        OperationChair chair,
        GameObject client)
    {
        BuildRequestsForClient(chair, client);
    }

    private void HandleClientRemoved(GameObject client)
    {
        clientsWithRequests.Remove(client);
        availableRequests.RemoveAll(
            request => request.targetClient == client);

        if (!isCompletingRequest &&
            currentState == State.ActiveRequest &&
            currentRequest.targetClient == client)
        {
            Debug.LogWarning(
                $"Cancelled the active doctor request because " +
                $"{client.name} left.",
                this);
            remainingTime = 0f;
            StartCooldown();
        }
    }
}
