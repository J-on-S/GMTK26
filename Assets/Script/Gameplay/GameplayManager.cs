using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("When enabled, GameplayAssetChecker must exist and pass before the day starts.")]
    [SerializeField] private bool requireAssetValidation;
    [FormerlySerializedAs("blackMarketTaskGenerator")]
    [SerializeField] private BlackMarketGenerator blackMarketGenerator;
    [SerializeField] private bool beginFirstDayOnStart = true;

    [Header("Runtime")]
    [SerializeField, Min(1)] private int currentDay = 1;
    [SerializeField] private GameplayDayState state = GameplayDayState.NotStarted;
    [SerializeField] private BlackMarketTask currentBlackMarketTask;

    [Header("Temporary end condition")]
    // TODO: These temporary global values might be replaced by the final
    // player-lives and countdown systems when their APIs exist.
    [SerializeField] private int numberOfLives = 4;
    [SerializeField] private float countdownRemaining = 0f;

    public int CurrentDay => currentDay;
    public GameplayDayState State => state;
    public BlackMarketTask CurrentBlackMarketTask => currentBlackMarketTask;
    public RandomizedClientList ClientList => RandomizedClientList.Instance;
    public bool RequireAssetValidation => requireAssetValidation;
    public int NumberOfLives => numberOfLives;
    public float CountdownRemaining => countdownRemaining;

    public event Action<int> DayStarted;
    public event Action<BlackMarketTask> BlackMarketTaskGenerated;
    public event Action<int> DayEnded;

    private void OnEnable()
    {
        if (ClientList != null)
            ClientList.TaskListEmptied += HandleClientTaskListEmptied;
    }

    private void OnDisable()
    {
        if (ClientList != null)
            ClientList.TaskListEmptied -= HandleClientTaskListEmptied;
    }

    private void Start()
    {
        if (beginFirstDayOnStart)
            BeginDay();
    }

    [ContextMenu("Begin Day")]
    public void BeginDay()
    {
        if (requireAssetValidation && !ValidateRequiredAssets())
        {
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

        if (ClientList == null)
        {
            Debug.LogError(
                "GameplayManager needs a RandomizedClientList singleton in the scene.",
                this);
            return;
        }

        if (ClientList.ActiveClientCount > 0)
        {
            Debug.LogError(
                "Cannot begin a new day while clients are still spawned.",
                this);
            return;
        }

        IBlackMarketTaskGenerator generator = blackMarketGenerator;

        if (generator == null)
        {
            Debug.LogError(
                "GameplayManager needs a BlackMarketGenerator.",
                this);
            return;
        }

        SetState(GameplayDayState.Preparing);
        Debug.Log($"[Day {currentDay}] Generating client/task list.", this);
        ClientList.GenerateList();

        if (ClientList.TaskListCount == 0)
        {
            Debug.LogError(
                $"[Day {currentDay}] Client/task list generation failed.",
                this);
            SetState(GameplayDayState.NotStarted);
            return;
        }

        Debug.Log(
            $"[Day {currentDay}] Generated " +
            $"{ClientList.TaskListCount} client/task entries.",
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

    private bool ValidateRequiredAssets()
    {
        GameplayAssetChecker assetChecker =
            GetComponent<GameplayAssetChecker>();

        if (assetChecker == null)
        {
            Debug.LogError(
                "Require Asset Validation is enabled, but " +
                "GameplayAssetChecker is missing.",
                this);
            return false;
        }

        if (assetChecker.ValidateSetup(this))
            return true;

        Debug.LogError(
            "Day startup stopped because required scene assets are missing.",
            this);
        return false;
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

    public void SetTemporaryLives(int lives)
    {
        numberOfLives = lives;
    }

    public void SetTemporaryCountdown(float secondsRemaining)
    {
        countdownRemaining = secondsRemaining;
    }

    private void HandleClientTaskListEmptied()
    {
        bool hasRequiredLives = numberOfLives > 3;
        bool countdownIsValid = countdownRemaining >= 0f;

        if (state == GameplayDayState.InProgress &&
            hasRequiredLives &&
            countdownIsValid)
        {
            Debug.Log(
                $"[Day {currentDay}] Client list reached zero with " +
                $"{numberOfLives} lives and {countdownRemaining:0.##} " +
                "seconds remaining. Ending the day.",
                this);
            EndDay();
            return;
        }

        Debug.LogWarning(
            $"[Day {currentDay}] Client list reached zero, but the normal " +
            "end conditions were not satisfied. This state should not " +
            $"normally be reachable. State={state}, Lives={numberOfLives}, " +
            $"Countdown={countdownRemaining:0.##}.",
            this);
    }

    private void SetState(GameplayDayState newState)
    {
        state = newState;
        Debug.Log(
            $"[Day {currentDay}] State changed to {state}.",
            this);
    }   
}
