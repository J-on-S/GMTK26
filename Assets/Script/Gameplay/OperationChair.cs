using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Owns one operation-chair slot. It spawns a client when the day begins and
/// replaces its occupant when that specific client leaves.
/// </summary>
public class OperationChair : MonoBehaviour
{
    [Header("Required references")]
    [SerializeField] private GameplayManager gameplayManager;
    [Tooltip(
        "A preset character or proxy positioned on this chair. " +
        "Spawned clients copy its world position, rotation, and scale.")]
    [FormerlySerializedAs("clientSpawnPoint")]
    [SerializeField] private Transform clientPoseProxy;
    [Tooltip("Hide the proxy when Play Mode starts so only spawned clients are visible.")]
    [SerializeField] private bool hidePoseProxyAtRuntime = true;

    [Header("Runtime")]
    [SerializeField] private GameObject currentClient;

    public GameObject CurrentClient => currentClient;
    public bool IsOccupied => currentClient != null;
    public RandomizedClientList ClientList => RandomizedClientList.Instance;
    public GameplayManager GameplayManager => gameplayManager;
    public Transform ClientPoseProxy => clientPoseProxy;
    [Obsolete("Use ClientPoseProxy instead.")]
    public Transform ClientSpawnPoint => clientPoseProxy;

    public event Action<OperationChair, GameObject> ClientPlaced;
    public event Action<OperationChair, GameObject> ClientLeft;

    private void Awake()
    {
        if (hidePoseProxyAtRuntime &&
            clientPoseProxy != null &&
            clientPoseProxy != transform)
        {
            clientPoseProxy.gameObject.SetActive(false);
        }
    }

    public bool ValidateConfiguration(
        GameplayManager expectedManager,
        RandomizedClientList expectedClientList,
        out string error)
    {
        if (ClientList == null)
        {
            error = "The scene has no RandomizedClientList singleton.";
            return false;
        }

        if (gameplayManager == null)
        {
            error = $"{name} has no Gameplay Manager assigned.";
            return false;
        }

        if (clientPoseProxy == null)
        {
            error = $"{name} has no Client Pose Proxy assigned.";
            return false;
        }

        if (clientPoseProxy == transform)
        {
            error =
                $"{name} must use a separate character object as its Client Pose Proxy.";
            return false;
        }

        if (expectedManager != null && gameplayManager != expectedManager)
        {
            error = $"{name} references a different Gameplay Manager.";
            return false;
        }

        if (expectedClientList != null && ClientList != expectedClientList)
        {
            error = $"{name} references a different Client List.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void OnEnable()
    {
        if (ClientList != null)
            ClientList.ClientRemoved += HandleClientRemoved;

        if (gameplayManager != null)
            gameplayManager.DayStarted += HandleDayStarted;
    }

    private void Start()
    {
        // Supports entering Play Mode while a generated list or active day
        // already exists.
        TrySpawnNextClient();
    }

    private void OnDisable()
    {
        if (ClientList != null)
            ClientList.ClientRemoved -= HandleClientRemoved;

        if (gameplayManager != null)
            gameplayManager.DayStarted -= HandleDayStarted;
    }

    /// <summary>
    /// Spawns the next pending pre-generated client if this chair is empty.
    /// </summary>
    [ContextMenu("Try Spawn Next Client")]
    public bool TrySpawnNextClient()
    {
        if (IsOccupied)
            return false;

        if (ClientList == null)
        {
            Debug.LogWarning(
                $"{name} needs a RandomizedClientList singleton in the scene.",
                this);
            return false;
        }

        if (gameplayManager != null &&
            gameplayManager.State != GameplayDayState.InProgress)
        {
            return false;
        }

        if (ClientList.PendingClientCount == 0)
        {
            Debug.Log(
                $"{name} is empty because no pending clients remain.",
                this);
            return false;
        }

        if (clientPoseProxy == null || clientPoseProxy == transform)
        {
            Debug.LogError(
                $"{name} needs a separate character object assigned as its " +
                "Client Pose Proxy.",
                this);
            return false;
        }

        currentClient = ClientList.SpawnNextClient(this);
        if (currentClient == null)
            return false;

        Debug.Log(
            $"{name} placed {currentClient.name} on the operation chair.",
            this);
        ClientPlaced?.Invoke(this, currentClient);
        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Complete Current Client Task")]
    private void DebugCompleteCurrentClientTask()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before completing a client task.",
                this);
            return;
        }

        GameObject clientToComplete = currentClient;
        if (clientToComplete == null)
        {
            Debug.LogWarning(
                $"{name} has no client to complete.",
                this);
            return;
        }

        ClientTaskHolder holder =
            clientToComplete.GetComponent<ClientTaskHolder>();
        if (holder == null || holder.AssignedTask == null)
        {
            Debug.LogError(
                $"{clientToComplete.name} has no assigned client task.",
                clientToComplete);
            return;
        }

        if (ClientList == null)
        {
            Debug.LogError(
                "The scene has no RandomizedClientList singleton.",
                this);
            return;
        }

        ClientTask task = holder.AssignedTask;
        foreach (BodyPartRequest request in task.Requests)
        {
            int remaining =
                task.GetRemainingAmount(request.BodyPartType);

            for (int i = 0; i < remaining; i++)
            {
                if (ClientList.RemoveOneFromTask(
                        clientToComplete,
                        request.BodyPartType))
                {
                    continue;
                }

                Debug.LogError(
                    $"Could not debug-complete {request.BodyPart} for " +
                    $"{clientToComplete.name}.",
                    this);
                return;
            }
        }

        Debug.Log(
            $"Debug-completed the task for {clientToComplete.name}.",
            this);
    }
#endif

    private void HandleDayStarted(int dayNumber)
    {
        TrySpawnNextClient();
    }

    private void HandleClientRemoved(GameObject removedClient)
    {
        if (removedClient != currentClient)
            return;

        GameObject previousClient = currentClient;
        currentClient = null;

        Debug.Log(
            $"{name} is now empty after {previousClient.name} left.",
            this);
        ClientLeft?.Invoke(this, previousClient);

        // The completed entry has already been removed by RandomizedClientList,
        // so this consumes the next pending entry.
        TrySpawnNextClient();
    }
}
