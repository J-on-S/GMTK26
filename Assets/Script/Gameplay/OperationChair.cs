using System;
using UnityEngine;

/// <summary>
/// Owns one operation-chair slot. It spawns a client when the day begins and
/// replaces its occupant when that specific client leaves.
/// </summary>
public class OperationChair : MonoBehaviour
{
    [Header("Required references")]
    [SerializeField] private RandomizedClientList clientList;
    [SerializeField] private GameplayManager gameplayManager;
    [SerializeField] private Transform clientSpawnPoint;

    [Header("Runtime")]
    [SerializeField] private GameObject currentClient;

    public GameObject CurrentClient => currentClient;
    public bool IsOccupied => currentClient != null;
    public RandomizedClientList ClientList => clientList;
    public GameplayManager GameplayManager => gameplayManager;
    public Transform ClientSpawnPoint => clientSpawnPoint;

    public event Action<OperationChair, GameObject> ClientPlaced;
    public event Action<OperationChair, GameObject> ClientLeft;

    private void Reset()
    {
        clientSpawnPoint = transform;
    }

    public bool ValidateConfiguration(
        GameplayManager expectedManager,
        RandomizedClientList expectedClientList,
        out string error)
    {
        if (clientList == null)
        {
            error = $"{name} has no Client List assigned.";
            return false;
        }

        if (gameplayManager == null)
        {
            error = $"{name} has no Gameplay Manager assigned.";
            return false;
        }

        if (clientSpawnPoint == null)
        {
            error = $"{name} has no Client Spawn Point assigned.";
            return false;
        }

        if (expectedManager != null && gameplayManager != expectedManager)
        {
            error = $"{name} references a different Gameplay Manager.";
            return false;
        }

        if (expectedClientList != null && clientList != expectedClientList)
        {
            error = $"{name} references a different Client List.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void OnEnable()
    {
        if (clientList != null)
            clientList.ClientRemoved += HandleClientRemoved;

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
        if (clientList != null)
            clientList.ClientRemoved -= HandleClientRemoved;

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

        if (clientList == null)
        {
            Debug.LogWarning(
                $"{name} needs a RandomizedClientList reference.",
                this);
            return false;
        }

        if (gameplayManager != null &&
            gameplayManager.State != GameplayDayState.InProgress)
        {
            return false;
        }

        if (clientList.PendingClientCount == 0)
        {
            Debug.Log(
                $"{name} is empty because no pending clients remain.",
                this);
            return false;
        }

        Transform spawnPoint =
            clientSpawnPoint != null ? clientSpawnPoint : transform;

        currentClient = clientList.SpawnNextClient(spawnPoint);
        if (currentClient == null)
            return false;

        Debug.Log(
            $"{name} placed {currentClient.name} on the operation chair.",
            this);
        ClientPlaced?.Invoke(this, currentClient);
        return true;
    }

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
