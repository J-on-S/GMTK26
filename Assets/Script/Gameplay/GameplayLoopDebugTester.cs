using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Debug-only harness for testing the gameplay loop after cutting has occurred.
/// It simulates the doctor's accepted result; it does not simulate tools,
/// cutting, physics, or doctor decision-making.
/// </summary>
public class GameplayLoopDebugTester : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] private GameplayManager gameplayManager;

    [Header("Doctor acceptance test")]
    [SerializeField, Range(0, GameplayAssetChecker.RequiredBedCount - 1)]
    private int selectedChairIndex;
    [SerializeField] private BodyPartType acceptedBodyPart = BodyPartType.Eye;

    [Header("Runtime debug list")]
    [SerializeField, TextArea(12, 30)] private string debugList;

    private RandomizedClientList clientList;
    private GameplayAssetChecker assetChecker;
    private bool isFastForwarding;

    public string DebugList => debugList;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private IEnumerator Start()
    {
        // GameplayManager starts and generates the day normally. This tester
        // only waits one frame before showing what was generated.
        yield return null;
        RefreshDebugList();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    [ContextMenu("1. Refresh Debug List")]
    public void RefreshDebugList()
    {
        ResolveReferences();

        if (gameplayManager == null || clientList == null)
        {
            debugList = "GameplayManager or ClientList is missing.";
            Debug.LogWarning(debugList, this);
            return;
        }

        StringBuilder text = new();
        text.AppendLine($"Day: {gameplayManager.CurrentDay}");
        text.AppendLine($"State: {gameplayManager.State}");
        text.AppendLine($"Lives (temporary): {gameplayManager.NumberOfLives}");
        text.AppendLine(
            $"Countdown (temporary): {gameplayManager.CountdownRemaining:0.##}");
        text.AppendLine($"Generated tasks: {clientList.TaskListCount}");
        text.AppendLine($"Pending clients: {clientList.PendingClientCount}");
        text.AppendLine($"Active clients: {clientList.ActiveClientCount}");

        if (gameplayManager.CurrentBlackMarketTask != null)
        {
            text.AppendLine(
                gameplayManager.CurrentBlackMarketTask.GetDescription());
        }

        text.AppendLine();
        text.AppendLine("CHAIRS");

        if (assetChecker != null)
        {
            for (int i = 0; i < assetChecker.OperationChairs.Count; i++)
            {
                OperationChair chair = assetChecker.OperationChairs[i];
                string occupant = chair != null && chair.CurrentClient != null
                    ? chair.CurrentClient.name
                    : "EMPTY";
                text.AppendLine($"{i}: {chair?.name ?? "MISSING"} -> {occupant}");
            }
        }

        text.AppendLine();
        text.AppendLine("CLIENT TASK LIST");

        int entryNumber = 1;
        foreach (ClientTaskQueueEntry entry in clientList.GetGeneratedList())
        {
            text.AppendLine(
                $"{entryNumber}. {clientList.GetPersonName(entry)} " +
                $"[{(entry.IsSpawned ? "SPAWNED" : "PENDING")}] " +
                $"Chair: {entry.AssignedChairName}");
            text.AppendLine($"   {clientList.GetRemainingTaskString(entry)}");

            foreach (BodyPartRequest request in entry.Task.Requests)
            {
                int remaining =
                    entry.Task.GetRemainingAmount(request.BodyPart);
                text.AppendLine(
                    $"   - {request.BodyPart}: {remaining}/{request.Amount} remaining");
            }

            entryNumber++;
        }

        debugList = text.ToString();
        Debug.Log(debugList, this);
    }

    [ContextMenu("2. Set Valid Temporary End Conditions")]
    public void SetValidTemporaryEndConditions()
    {
        if (!RequirePlayMode() || gameplayManager == null)
            return;

        gameplayManager.SetTemporaryLives(4);
        gameplayManager.SetTemporaryCountdown(0f);
        RefreshDebugList();
    }

    [ContextMenu("3. Set Unreachable Temporary End Conditions")]
    public void SetUnreachableTemporaryEndConditions()
    {
        if (!RequirePlayMode() || gameplayManager == null)
            return;

        gameplayManager.SetTemporaryLives(3);
        gameplayManager.SetTemporaryCountdown(-1f);
        RefreshDebugList();
    }

    [ContextMenu("4. Doctor Accepts Selected Body Part")]
    public void DoctorAcceptsSelectedBodyPart()
    {
        OperationChair chair = GetSelectedChair();
        if (chair == null || chair.CurrentClient == null)
        {
            Debug.LogWarning("The selected chair has no client.", this);
            return;
        }

        bool changed = clientList.RemoveOneFromTask(
            chair.CurrentClient,
            acceptedBodyPart);

        if (!changed)
        {
            Debug.LogWarning(
                $"{chair.CurrentClient.name} does not currently need " +
                $"{acceptedBodyPart}.",
                this);
        }

        RefreshDebugList();
    }

    [ContextMenu("5. Doctor Accepts Next Needed Part")]
    public void DoctorAcceptsNextNeededPart()
    {
        OperationChair chair = GetSelectedChair();
        if (chair == null || chair.CurrentClient == null)
        {
            Debug.LogWarning("The selected chair has no client.", this);
            return;
        }

        ClientTask task =
            chair.CurrentClient.GetComponent<ClientTaskHolder>()?.AssignedTask;

        if (task == null)
        {
            Debug.LogWarning("Selected client has no assigned task.", this);
            return;
        }

        foreach (BodyPartRequest request in task.Requests)
        {
            if (task.GetRemainingAmount(request.BodyPart) <= 0)
                continue;

            acceptedBodyPart = request.BodyPart;
            clientList.RemoveOneFromTask(
                chair.CurrentClient,
                request.BodyPart);
            RefreshDebugList();
            return;
        }

        Debug.LogWarning("Selected client has no remaining requirements.", this);
    }

    [ContextMenu("6. Complete Selected Client Task")]
    public void CompleteSelectedClientTask()
    {
        OperationChair chair = GetSelectedChair();
        if (chair == null || chair.CurrentClient == null)
        {
            Debug.LogWarning("The selected chair has no client.", this);
            return;
        }

        GameObject targetClient = chair.CurrentClient;
        ClientTask task =
            targetClient.GetComponent<ClientTaskHolder>()?.AssignedTask;

        if (task == null)
            return;

        isFastForwarding = true;

        foreach (BodyPartRequest request in task.Requests)
        {
            while (task.GetRemainingAmount(request.BodyPart) > 0)
            {
                if (!clientList.RemoveOneFromTask(
                        targetClient,
                        request.BodyPart))
                {
                    break;
                }
            }
        }

        isFastForwarding = false;
        RefreshDebugList();
    }

    [ContextMenu("7. Complete All Remaining Client Tasks")]
    public void CompleteAllRemainingClientTasks()
    {
        if (!RequirePlayMode() || clientList == null)
            return;

        List<ClientTaskQueueEntry> snapshot =
            new(clientList.GetGeneratedList());
        isFastForwarding = true;

        foreach (ClientTaskQueueEntry entry in snapshot)
        {
            foreach (BodyPartRequest request in entry.Task.Requests)
            {
                while (entry.Task.GetRemainingAmount(request.BodyPart) > 0)
                {
                    if (!clientList.RemoveOneFromTask(
                            entry,
                            request.BodyPart))
                    {
                        break;
                    }
                }
            }
        }

        isFastForwarding = false;
        RefreshDebugList();
    }

    [ContextMenu("8. Force End Day")]
    public void ForceEndDay()
    {
        if (!RequirePlayMode() || gameplayManager == null)
            return;

        gameplayManager.EndDay();
        RefreshDebugList();
    }

    private OperationChair GetSelectedChair()
    {
        if (!RequirePlayMode())
            return null;

        ResolveReferences();

        if (assetChecker == null ||
            selectedChairIndex < 0 ||
            selectedChairIndex >= assetChecker.OperationChairs.Count)
        {
            Debug.LogWarning("Selected operation chair is unavailable.", this);
            return null;
        }

        return assetChecker.OperationChairs[selectedChairIndex];
    }

    private void ResolveReferences()
    {
        if (gameplayManager == null)
            gameplayManager = GetComponent<GameplayManager>();

        if (gameplayManager == null)
            return;

        clientList = gameplayManager.ClientList;
        assetChecker = gameplayManager.GetComponent<GameplayAssetChecker>();
    }

    private void Subscribe()
    {
        if (gameplayManager != null)
        {
            gameplayManager.DayStarted += HandleDayChanged;
            gameplayManager.DayEnded += HandleDayChanged;
        }

        if (clientList != null)
        {
            clientList.TaskRequirementChanged += HandleRequirementChanged;
            clientList.TaskListEntryCreated += HandleEntryChanged;
            clientList.TaskListEntryRemoved += HandleEntryChanged;
        }
    }

    private void Unsubscribe()
    {
        if (gameplayManager != null)
        {
            gameplayManager.DayStarted -= HandleDayChanged;
            gameplayManager.DayEnded -= HandleDayChanged;
        }

        if (clientList != null)
        {
            clientList.TaskRequirementChanged -= HandleRequirementChanged;
            clientList.TaskListEntryCreated -= HandleEntryChanged;
            clientList.TaskListEntryRemoved -= HandleEntryChanged;
        }
    }

    private void HandleDayChanged(int dayNumber)
    {
        RefreshUnlessFastForwarding();
    }

    private void HandleEntryChanged(ClientTaskQueueEntry entry)
    {
        RefreshUnlessFastForwarding();
    }

    private void HandleRequirementChanged(
        ClientTaskQueueEntry entry,
        BodyPartType bodyPart,
        int remaining)
    {
        RefreshUnlessFastForwarding();
    }

    private void RefreshUnlessFastForwarding()
    {
        if (!isFastForwarding)
            RefreshDebugList();
    }

    private bool RequirePlayMode()
    {
        if (Application.isPlaying)
            return true;

        Debug.LogWarning(
            "Enter Play Mode before using gameplay debug actions.",
            this);
        return false;
    }
}
