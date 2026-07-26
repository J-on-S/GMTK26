using System;
using UnityEngine;
using UnityEngine.Events;
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
    [SerializeField] private bool lastBlackMarketTaskSucceeded;

    [Header("Temporary fallback/end condition")]
    // Used only when the scene has no HealthScript singleton.
    [SerializeField] private int numberOfLives = 4;
    [SerializeField] private float countdownRemaining = 0f;

    [Header("End of day")]
    [Tooltip(
        "Invoked when the day ends. Connect the black-market camera or UI " +
        "entry function here.")]
    [SerializeField] private UnityEvent enterBlackMarketRequested = new();

    public int CurrentDay => currentDay;
    public GameplayDayState State => state;
    public BlackMarketTask CurrentBlackMarketTask => currentBlackMarketTask;
    public RandomizedClientList ClientList => RandomizedClientList.Instance;
    public HealthScript Health => HealthScript.Instance;
    public bool RequireAssetValidation => requireAssetValidation;
    public int NumberOfLives =>
        Health != null ? HealthScript.HP : numberOfLives;
    public float CountdownRemaining => countdownRemaining;
    public bool LastBlackMarketTaskSucceeded =>
        lastBlackMarketTaskSucceeded;
    public UnityEvent EnterBlackMarketRequested =>
        enterBlackMarketRequested;

    public event Action<int> DayStarted;
    public event Action<BlackMarketTask> BlackMarketTaskGenerated;
    public event Action<bool> BlackMarketTaskResolved;
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
        BodyPartRunSummary.Instance.ResetCounts();
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

        lastBlackMarketTaskSucceeded =
            blackMarketGenerator != null &&
            blackMarketGenerator.IsSucceedBlackMarket();

        BlackMarketTaskResolved?.Invoke(
            lastBlackMarketTaskSucceeded);

        Debug.Log(
            $"[Day {currentDay}] Black-market task " +
            (lastBlackMarketTaskSucceeded
                ? "completed."
                : "failed."),
            this);

        SetState(GameplayDayState.Ended);
        enterBlackMarketRequested?.Invoke();
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
        lastBlackMarketTaskSucceeded = false;
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
        int currentLives = NumberOfLives;
        bool playerIsAlive = currentLives > 0;
        bool countdownIsValid = countdownRemaining >= 0f;

        if (state == GameplayDayState.InProgress &&
            playerIsAlive &&
            countdownIsValid)
        {
            Debug.Log(
                $"[Day {currentDay}] Client list reached zero with " +
                $"{currentLives} health and {countdownRemaining:0.##} " +
                "seconds remaining. Resolving the black-market task, " +
                "then ending the day.",
                this);
            EndDay();
            return;
        }

        Debug.LogWarning(
            $"[Day {currentDay}] Client list reached zero, but the normal " +
            "end conditions were not satisfied. This state should not " +
            $"normally be reachable. State={state}, Health={currentLives}, " +
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
