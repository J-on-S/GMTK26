using System;
using UnityEngine;

public enum GameplayDayState
{
    NotStarted,
    Preparing,
    InProgress,
    Ended
}

/// <summary>
/// Coordinates the high-level day flow. The first prototype phase prepares
/// the client/task queue and black-market task at the beginning of each day.
/// </summary>
public class GameplayManager : MonoBehaviour
{
    [Header("Beginning of day")]
    [SerializeField] private RandomizedClientList clientList;
    [Tooltip("Must implement IBlackMarketTaskGenerator.")]
    [SerializeField] private MonoBehaviour blackMarketTaskGenerator;
    [SerializeField] private bool beginFirstDayOnStart = true;

    [Header("Runtime")]
    [SerializeField, Min(1)] private int currentDay = 1;
    [SerializeField] private GameplayDayState state = GameplayDayState.NotStarted;
    [SerializeField] private BlackMarketTask currentBlackMarketTask;

    public int CurrentDay => currentDay;
    public GameplayDayState State => state;
    public BlackMarketTask CurrentBlackMarketTask => currentBlackMarketTask;
    public RandomizedClientList ClientList => clientList;

    public event Action<int> DayStarted;
    public event Action<BlackMarketTask> BlackMarketTaskGenerated;
    public event Action<int> DayEnded;

    private void Start()
    {
        if (beginFirstDayOnStart)
            BeginDay();
    }

    [ContextMenu("Begin Day")]
    public void BeginDay()
    {
        GameplayAssetChecker assetChecker =
            GetComponent<GameplayAssetChecker>();

        if (assetChecker == null)
        {
            Debug.LogError(
                "GameplayManager requires GameplayAssetChecker and will not start.",
                this);
            return;
        }

        if (!assetChecker.ValidateSetup(this))
        {
            Debug.LogError(
                "Day startup stopped because required scene assets are missing.",
                this);
            return;
        }

        if (state == GameplayDayState.Preparing ||
            state == GameplayDayState.InProgress)
        {
            Debug.LogWarning(
                $"Cannot begin day {currentDay}; the day is already {state}.",
                this);
            return;
        }

        if (clientList == null)
        {
            Debug.LogError(
                "GameplayManager needs a RandomizedClientList.",
                this);
            return;
        }

        if (clientList.ActiveClientCount > 0)
        {
            Debug.LogError(
                "Cannot begin a new day while clients are still spawned.",
                this);
            return;
        }

        IBlackMarketTaskGenerator generator =
            blackMarketTaskGenerator as IBlackMarketTaskGenerator;

        if (generator == null)
        {
            Debug.LogError(
                "Black Market Task Generator must implement " +
                "IBlackMarketTaskGenerator.",
                this);
            return;
        }

        SetState(GameplayDayState.Preparing);
        Debug.Log($"[Day {currentDay}] Generating client/task list.", this);
        clientList.GenerateList();

        if (clientList.TaskListCount == 0)
        {
            Debug.LogError(
                $"[Day {currentDay}] Client/task list generation failed.",
                this);
            SetState(GameplayDayState.NotStarted);
            return;
        }

        Debug.Log(
            $"[Day {currentDay}] Generated " +
            $"{clientList.TaskListCount} client/task entries.",
            this);

        Debug.Log($"[Day {currentDay}] Generating black-market task.", this);
        currentBlackMarketTask = generator.GenerateTask(currentDay);

        if (currentBlackMarketTask == null ||
            currentBlackMarketTask.RequestedParts.Count == 0)
        {
            Debug.LogError(
                $"[Day {currentDay}] Black-market task generation failed.",
                this);
            SetState(GameplayDayState.NotStarted);
            return;
        }

        BlackMarketTaskGenerated?.Invoke(currentBlackMarketTask);
        Debug.Log(
            $"[Day {currentDay}] " +
            currentBlackMarketTask.GetDescription(),
            this);

        SetState(GameplayDayState.InProgress);
        DayStarted?.Invoke(currentDay);
        Debug.Log($"[Day {currentDay}] Day started.", this);
    }

    [ContextMenu("End Day")]
    public void EndDay()
    {
        if (state != GameplayDayState.InProgress)
        {
            Debug.LogWarning("No active day can be ended.", this);
            return;
        }

        SetState(GameplayDayState.Ended);
        DayEnded?.Invoke(currentDay);
        Debug.Log($"[Day {currentDay}] Day ended.", this);
    }

    public void AdvanceToNextDay()
    {
        if (state != GameplayDayState.Ended)
        {
            Debug.LogWarning(
                "End the current day before advancing.",
                this);
            return;
        }

        currentDay++;
        state = GameplayDayState.NotStarted;
        currentBlackMarketTask = null;
        BeginDay();
    }

    private void SetState(GameplayDayState newState)
    {
        state = newState;
        Debug.Log(
            $"[Day {currentDay}] State changed to {state}.",
            this);
    }   
}
