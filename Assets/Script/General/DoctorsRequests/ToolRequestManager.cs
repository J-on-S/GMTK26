using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ToolRequestManager : MonoBehaviour
{
    // request stuff
    [ReadOnly] public DoctorRequest doctorCurrentRequests;// this contains the active doctor's requests
    public TimeRange timeBetweenRequests = new TimeRange(5f);
    public IntRange numberOfRequests = new IntRange(5);  // minimum size of the focused client's batch

    [Header("Request timing")]
    [Tooltip("Every doctor request receives a random limit within this range.")]
    [SerializeField] private TimeRange requestTime = new TimeRange(50f, 60f);

    [Header("Early completion reward")]
    [Tooltip(
        "When a request succeeds, add its remaining request time to the " +
        "next cooldown.")]
    [SerializeField] private bool rewardEarlyCompletion = true;
    [SerializeField, Min(0f)]
    private float earlyCompletionBonusMultiplier = 1f;
    [Tooltip(
        "Prevents a long request from creating an equally long cooldown.")]
    [SerializeField, Min(0f)]
    private float maximumEarlyCompletionBonus = 10f;

    [Header("Client processing order")]
    [Tooltip("Assign Bed A first, then Bed B. If empty, chairs are found and sorted by name.")]
    [SerializeField] private List<OperationChair> operationChairs = new();

    // states
    private enum State{Idle, ActiveRequest, Cooldown}

    [Header("Runtime debug")]
    [SerializeField] private State currentState = State.Idle;

    public static Request currentRequest;
    [SerializeField] private float remainingTime;
    [SerializeField] private float remainingCooldown;
    [SerializeField] private float lastRequestElapsedTime;
    [SerializeField] private float lastEarlyCompletionBonus;
    [SerializeField] private OperationChair focusedChair;
    [SerializeField] private GameObject focusedClient;
    [SerializeField] private int focusedChairIndex = -1;
    private RandomizedClientList subscribedClientList;
    private bool isCompletingFocusedClient;
    private bool completeFocusedClientAfterCooldown;

    public event Action<Request> RequestStarted;
    public event Action<Request> RequestCompleted;
    public event Action<Request> RequestFailed;
    public event Action<float> EarlyCompletionBonusAwarded;
    public event Action RequestQueueEmptied;
    public OperationChair FocusedChair => focusedChair;
    public GameObject FocusedClient => focusedClient;
    public float RemainingRequestTime => remainingTime;
    public float RemainingCooldownTime => remainingCooldown;
    public float LastEarlyCompletionBonus =>
        lastEarlyCompletionBonus;
    public bool IsRequestActive =>
        currentState == State.ActiveRequest;
    public bool IsCooldownActive =>
        currentState == State.Cooldown;
    [Tooltip("Where a delivered body part gets stuck onto the customer. Found in the scene when left empty.")]
    [SerializeField] private SpawnBodyPartCustomer spawnBodyPartCustomer;


    // stuff for doctor request UI
    public TextMeshProUGUI myTextLabel;

    private void OnEnable()
    {
        SubscribeToClientList();
    }

    // request sound
    public AudioSet DoctorRequestAudio;

    private void Awake()
    {
        InitializeChairOrder();
        currentRequest = default;
    }

    private void Start()
    {
        // the field is serialized, so a scene can wire it; found here only when it was left empty.
        if (spawnBodyPartCustomer == null)
        {
            spawnBodyPartCustomer = FindFirstObjectByType<SpawnBodyPartCustomer>();
            if (spawnBodyPartCustomer == null)
            {
                Debug.LogWarning($"{name}: no SpawnBodyPartCustomer in the scene, so delivered body parts will not be attached to the customer.", this);
            }
        }

        if (myTextLabel == null)
        {
            Debug.LogError($"{name}: no myTextLabel assigned, so the doctor's requests cannot be shown.", this);
        }

        SubscribeToClientList();
        RegisterAlreadySpawnedClients();
    }

    private void OnDisable()
    {
        if (subscribedClientList == null)
            return;

        subscribedClientList.ClientSpawnedOnChair -= HandleClientSpawned;
        subscribedClientList.ClientRemoved -= HandleClientRemoved;
        subscribedClientList = null;
    }

    /// <summary>Writes the doctor's line, when there is a label to write it on.</summary>
    private void SetRequestText(string text)
    {
        if (myTextLabel == null) return;
        myTextLabel.text = text;
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

    public void GenerateDoctorRequestsTools()
    {
        int minimumQueueSize =
            Mathf.Max(0, Mathf.RoundToInt(numberOfRequests.RandomValue()));
        if (doctorCurrentRequests.Count >= minimumQueueSize)
            return;

        if (Tools.Instance.tools.Count == 0)
        {
            Debug.LogError(
                "ToolRequestManager cannot fill the queue because All Tools is empty.",
                this);
            return;
        }

        while (doctorCurrentRequests.Count < minimumQueueSize)
        {
            Tool currentTool = Tools.Instance.RandomTool();
            float timeLimit = currentTool.toolTime.RandomValue();
            ToolRequest currentToolRequest = new ToolRequest(currentTool, timeLimit);
            doctorCurrentRequests.AddRequest(currentToolRequest);
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

        ClientTaskHolder holder = client.GetComponent<ClientTaskHolder>();
        if (holder == null || holder.AssignedTask == null)
        {
            Debug.LogError(
                $"{client.name} needs ClientTaskHolder with an assigned task.",
                client);
            return;
        }

        focusedChair = chair;
        focusedClient = client;
        doctorCurrentRequests = new DoctorRequest(focusedClient, focusedChair); 

        //Add Body Part
        ClientTask task = holder.AssignedTask;
        HashSet<BodyPartType> addedTypes = new();

        foreach (BodyPartRequest bodyPartRequest in task.Requests)
        {
            if (!addedTypes.Add(bodyPartRequest.BodyPartType))
                continue;
            
            int remaining = task.GetRemainingAmount(bodyPartRequest.BodyPartType);
            for (int i = 0; i < remaining; i++)
            {
                bodyPartRequest.RequestTime = bodyPartRequest.BodyPart.bodyPartTime.RandomValue();
                doctorCurrentRequests.AddRequest(bodyPartRequest);
            }
        }

        GenerateDoctorRequestsTools();
        Debug.Log(
            $"Doctor is now processing {client.name} on {chair.name}. " +
            $"Batch size: {doctorCurrentRequests.Count}.",
            this);

        StartCooldown();
    }
    private void GenerateDoctorCurrentRequests()
    {
        
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
        if (remainingCooldown > 0)
            return;

        remainingCooldown = 0f;

        if (!completeFocusedClientAfterCooldown)
        {
            StartNewRandomRequest();
            return;
        }

        completeFocusedClientAfterCooldown = false;
        CompleteFocusedClientAndAdvance();
    }

    // randomize the requests
    public void StartNewRandomRequest()
    {
        if (doctorCurrentRequests.Count == 0)
        {
            currentState = State.Idle;
            remainingTime = 0f;
            RequestQueueEmptied?.Invoke();
            CompleteFocusedClientAndAdvance();
            return;
        }

        currentRequest = doctorCurrentRequests.ChosenRequest();

        // currentRequest.tool.timeLimit = ClampRequestTimeLimit(
        //     currentRequest.tool.timeLimit);
        remainingTime = currentRequest.RequestTime;
        currentState = State.ActiveRequest;

        string itemCategory = currentRequest.ItemType.ToString();
        string chairText = doctorCurrentRequests.targetChair != null
            ? $" for {doctorCurrentRequests.targetChair.name}"
            : string.Empty;
        Debug.Log(
            $"Hey, hand me a {itemCategory}: [{currentRequest.ItemName}]" +
            $"{chairText} within {remainingTime:F1} seconds!",
            this);

        if (myTextLabel != null)
        {
            myTextLabel.text =
                "Hey, hand me a " + itemCategory +
                ":<color=\"red\"> " + currentRequest.ItemName + "</color>" +
                chairText;
        }

        AudioEventChannel.Instance.Play(DoctorRequestAudio);
        RequestStarted?.Invoke(currentRequest);
        Debug.Log(
            $"Doctor requests remaining in queue: {doctorCurrentRequests.Count}.",
            this);
    }

    // check if player submitted the tool correctly, returns true if correctly submitted
    public bool PlayerSubmittedTool(Item receivedItem)
    {
        if (currentState != State.ActiveRequest)
            return false;
        if(currentRequest.ItemType != receivedItem.Type) return false;

        if (receivedItem is BodyPart bodyPart)
        {
            if(currentRequest is BodyPartRequest bodyPartRequest)
            {
                if(bodyPart.BodyPartType == bodyPartRequest.BodyPartType) return Succeed(receivedItem);
            }
            
        }else if(receivedItem is Tool tool)
        {
            if(currentRequest is ToolRequest toolRequest)
            {
                if(tool.toolType == toolRequest.Tool.toolType) return Succeed(receivedItem);
            }
        }
        return Failure(receivedItem);
    }
    private bool Succeed(Item receivedItem)
    {
        //Debug.Log("Dude thanks for giving me that.");
        SetRequestText(DoctorDialogueTexts.getRandomAcceptingText(currentRequest, receivedItem));
        CompleteActiveRequest();
        return true;
    }
    private bool Failure(Item receivedItem)
    {
        Debug.Log($"Nah man wrong tool. I needed {currentRequest.ItemType} named {currentRequest.ItemName}, but you gave me {receivedItem.Type} named {receivedItem.Name}.");
        SetRequestText(DoctorDialogueTexts.getRandomFailureText(currentRequest, receivedItem));
        return false;
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
            $"{currentRequest.ItemType} [{currentRequest.ItemName}].",
            this);

        if (myTextLabel != null)
            myTextLabel.text = "Debug-completed request.";

        CompleteActiveRequest();
    }
#endif

    private void CompleteActiveRequest()
    {
        Request completedRequest = currentRequest;
        float safeRemainingTime = Mathf.Max(0f, remainingTime);
        lastRequestElapsedTime = Mathf.Max(
            0f,
            completedRequest.RequestTime - safeRemainingTime);
        lastEarlyCompletionBonus = rewardEarlyCompletion
            ? Mathf.Min(
                safeRemainingTime *
                Mathf.Max(0f, earlyCompletionBonusMultiplier),
                Mathf.Max(0f, maximumEarlyCompletionBonus))
            : 0f;

        if (completedRequest is BodyPartRequest bodyRequest)
        {
            if (doctorCurrentRequests.targetClient != null)
            {
                SpawnBodyPartCustomer bodyPartVisual =
                    doctorCurrentRequests.targetClient
                        .GetComponent<SpawnBodyPartCustomer>();
                if (bodyPartVisual == null)
                    bodyPartVisual = spawnBodyPartCustomer;
                bodyPartVisual?.AddBodyPart(bodyRequest.BodyPartType);
            }
        }
        else if (completedRequest is ToolRequest toolRequest)
        {
            Debug.Log(toolRequest.Tool);
        }
        else
        {
            Debug.LogError("Request is Request type");
        }

        remainingTime = 0f;
        RequestCompleted?.Invoke(completedRequest);

        if (lastEarlyCompletionBonus > 0f)
        {
            EarlyCompletionBonusAwarded?.Invoke(
                lastEarlyCompletionBonus);
        }

        bool finishedClientBatch =
            doctorCurrentRequests.Count == 0;
        if (finishedClientBatch)
            RequestQueueEmptied?.Invoke();

        StartCooldown(
            lastEarlyCompletionBonus,
            finishedClientBatch);
    }

    private void FailRequest()
    {
        Request failedRequest = currentRequest;
        Debug.Log("Time is up! You failed the request.");
        SetRequestText("Ran out of time");

        if (myTextLabel != null)
            myTextLabel.text = "Ran out of time";

        // Every request stays in this client's batch until it succeeds.
        doctorCurrentRequests.AddRequest(failedRequest);

        remainingTime = 0f;
        lastEarlyCompletionBonus = 0f;
        RequestFailed?.Invoke(failedRequest);
        HealthScript.Instance.TakeDamage();     
        StartCooldown();
    }

    // deals with cooldown state and timer
    private void StartCooldown(
        float bonusTime = 0f,
        bool finishClientAfterCooldown = false)
    {
        float baseCooldown = Mathf.Max(0f, timeBetweenRequests.RandomValue());
        float safeBonus = Mathf.Max(0f, bonusTime);
        remainingCooldown = baseCooldown + safeBonus;
        completeFocusedClientAfterCooldown =
            finishClientAfterCooldown;
        currentState = State.Cooldown;
        string requestTiming = safeBonus > 0f ||
                               finishClientAfterCooldown
            ? $" Request used {lastRequestElapsedTime:0.##} seconds."
            : string.Empty;
        Debug.Log(
            $"Doctor cooldown: {baseCooldown:0.##} base + " +
            $"{safeBonus:0.##} early-completion bonus = " +
            $"{remainingCooldown:0.##} seconds." +
            requestTiming,
            this);
    }

    public float timeRemaining()
    {
        if (IsRequestActive)
            return remainingTime;

        if (IsCooldownActive)
            return remainingCooldown;

        return 0f;
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
        TryStartNextClientBatch();
    }

    private void HandleClientSpawned(
        OperationChair chair,
        GameObject client)
    {
        InitializeChairOrder();

        if (focusedClient != null)
            return;

        int spawnedChairIndex = operationChairs.IndexOf(chair);
        if (spawnedChairIndex < 0)
            return;

        if (focusedChairIndex < 0)
        {
            if (spawnedChairIndex != 0)
                return;

            focusedChairIndex = 0;
            BuildRequestsForClient(chair, client);
            return;
        }

        int expectedChairIndex =
            (focusedChairIndex + 1) % operationChairs.Count;
        if (spawnedChairIndex == expectedChairIndex)
        {
            focusedChairIndex = spawnedChairIndex;
            BuildRequestsForClient(chair, client);
            return;
        }

        TryStartNextClientBatch();
    }

    private void HandleClientRemoved(GameObject client)
    {
        doctorCurrentRequests = null;
        // currentRequests.RemoveAll(
        //     request => request.targetClient == client);

        if (client != focusedClient || isCompletingFocusedClient)
            return;

        if (currentState == State.ActiveRequest)
        {
            Debug.LogWarning(
                $"Cancelled the active doctor request because " +
                $"{client.name} left.",
                this);
            remainingTime = 0f;
        }

        focusedClient = null;
        focusedChair = null;
        currentRequest = default;
        remainingCooldown = 0f;
        completeFocusedClientAfterCooldown = false;
        currentState = State.Idle;
        StartCoroutine(StartNextClientBatchNextFrame());
    }

    private void InitializeChairOrder()
    {
        operationChairs.RemoveAll(chair => chair == null);

        for (int i = operationChairs.Count - 1; i >= 0; i--)
        {
            if (operationChairs.IndexOf(operationChairs[i]) != i)
                operationChairs.RemoveAt(i);
        }

        if (operationChairs.Count > 0)
            return;

        OperationChair[] discoveredChairs =
            FindObjectsByType<OperationChair>(
                FindObjectsSortMode.None);
        operationChairs.AddRange(discoveredChairs);
        operationChairs.Sort(
            (left, right) => string.Compare(
                left.name,
                right.name,
                StringComparison.Ordinal));
    }

    private bool TryStartNextClientBatch()
    {
        if (focusedClient != null)
            return false;

        InitializeChairOrder();
        if (operationChairs.Count == 0)
        {
            Debug.LogWarning(
                "ToolRequestManager cannot find any OperationChair.",
                this);
            return false;
        }

        // The first batch strictly waits for the first configured chair
        // (Bed A). Later batches skip empty chairs while preserving rotation.
        if (focusedChairIndex < 0)
        {
            OperationChair firstChair = operationChairs[0];
            if (firstChair == null || firstChair.CurrentClient == null)
                return false;

            focusedChairIndex = 0;
            BuildRequestsForClient(
                firstChair,
                firstChair.CurrentClient);
            return true;
        }

        for (int offset = 1; offset <= operationChairs.Count; offset++)
        {
            int chairIndex =
                (focusedChairIndex + offset) % operationChairs.Count;
            OperationChair chair = operationChairs[chairIndex];
            if (chair == null || chair.CurrentClient == null)
                continue;

            focusedChairIndex = chairIndex;
            BuildRequestsForClient(chair, chair.CurrentClient);
            return true;
        }

        Debug.Log(
            "The doctor is idle because no bed currently has a client.",
            this);
        return false;
    }

    private void CompleteFocusedClientAndAdvance()
    {
        GameObject clientToComplete = focusedClient;
        OperationChair completedChair = focusedChair;

        if (clientToComplete == null || completedChair == null)
        {
            focusedClient = null;
            focusedChair = null;
            TryStartNextClientBatch();
            return;
        }

        string completedClientName = clientToComplete.name;
        ClientTaskHolder holder =
            clientToComplete.GetComponent<ClientTaskHolder>();
        if (holder == null || holder.AssignedTask == null)
        {
            Debug.LogError(
                $"{clientToComplete.name} has no assigned client task.",
                clientToComplete);
            return;
        }

        isCompletingFocusedClient = true;
        bool completed = CompleteAllClientRequirements(
            clientToComplete,
            holder.AssignedTask);
        isCompletingFocusedClient = false;

        if (!completed)
        {
            Debug.LogError(
                $"Could not complete the focused client on " +
                $"{completedChair.name}.",
                this);
            BuildRequestsForClient(
                completedChair,
                clientToComplete);
            return;
        }

        Debug.Log(
            $"Finished the doctor batch and completed " +
            $"{completedClientName} on {completedChair.name}.",
            this);

        focusedClient = null;
        focusedChair = null;
        currentRequest = default;
        remainingCooldown = 0f;
        completeFocusedClientAfterCooldown = false;
        currentState = State.Idle;
        TryStartNextClientBatch();
    }

    private IEnumerator StartNextClientBatchNextFrame()
    {
        yield return null;
        TryStartNextClientBatch();
    }

    private static bool CompleteAllClientRequirements(
        GameObject client,
        ClientTask task)
    {
        RandomizedClientList clientList =
            RandomizedClientList.Instance;
        if (clientList == null)
            return false;

        HashSet<BodyPartType> completedTypes = new();
        foreach (BodyPartRequest request in task.Requests)
        {
            if (!completedTypes.Add(request.BodyPartType))
                continue;

            int remaining =
                task.GetRemainingAmount(request.BodyPartType);
            for (int i = 0; i < remaining; i++)
            {
                if (!clientList.RemoveOneFromTask(
                        client,
                        request.BodyPartType))
                {
                    return false;
                }
            }
        }

        return task.IsComplete;
    }
}
