using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BodyPartRunCount
{
    [SerializeField] private BodyPartType bodyPartType;
    [SerializeField] private int chuteCount;
    [SerializeField] private int fridgeCount;

    public BodyPartType BodyPartType => bodyPartType;
    public int ChuteCount => chuteCount;
    public int FridgeCount => fridgeCount;
    public int TotalCount => chuteCount + fridgeCount;

    public BodyPartRunCount(BodyPartType bodyPartType)
    {
        this.bodyPartType = bodyPartType;
    }

    public void AddChute()
    {
        chuteCount++;
    }

    public void AddFridge()
    {
        fridgeCount++;
    }

    public void RemoveFridge()
    {
        fridgeCount = Mathf.Max(0, fridgeCount - 1);
    }

    public void Reset()
    {
        chuteCount = 0;
        fridgeCount = 0;
    }
}

/// <summary>
/// Persists the current run's chute and fridge body-part totals when gameplay
/// transitions to the Win or Lost scene.
/// </summary>
[DefaultExecutionOrder(-2000)]
public class BodyPartRunSummary : MonoBehaviour
{
    private static BodyPartRunSummary instance;

    [SerializeField] private List<BodyPartRunCount> counts = new();

    public static BodyPartRunSummary Instance
    {
        get
        {
            if (instance != null)
                return instance;

            instance =
                FindFirstObjectByType<BodyPartRunSummary>();
            if (instance != null)
                return instance;

            GameObject summaryObject =
                new("Body Part Run Summary");
            instance =
                summaryObject.AddComponent<BodyPartRunSummary>();
            return instance;
        }
    }

    public IReadOnlyList<BodyPartRunCount> Counts => counts;
    public event Action CountsChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAllBodyPartTypesExist();
    }

    public void RecordChuteBodyPart(BodyPartType bodyPartType)
    {
        GetOrCreate(bodyPartType).AddChute();
        CountsChanged?.Invoke();
    }

    public void RecordFridgeBodyPartAdded(
        BodyPartType bodyPartType)
    {
        GetOrCreate(bodyPartType).AddFridge();
        CountsChanged?.Invoke();
    }

    public void RecordFridgeBodyPartRemoved(
        BodyPartType bodyPartType)
    {
        GetOrCreate(bodyPartType).RemoveFridge();
        CountsChanged?.Invoke();
    }

    public int GetChuteCount(BodyPartType bodyPartType)
    {
        return GetOrCreate(bodyPartType).ChuteCount;
    }

    public int GetFridgeCount(BodyPartType bodyPartType)
    {
        return GetOrCreate(bodyPartType).FridgeCount;
    }

    public int GetTotalCount(BodyPartType bodyPartType)
    {
        return GetOrCreate(bodyPartType).TotalCount;
    }

    public void ResetCounts()
    {
        EnsureAllBodyPartTypesExist();
        foreach (BodyPartRunCount count in counts)
            count.Reset();

        CountsChanged?.Invoke();
    }

    [ContextMenu("Debug/Print Body Part Run Summary")]
    private void DebugPrintSummary()
    {
        EnsureAllBodyPartTypesExist();
        foreach (BodyPartRunCount count in counts)
        {
            Debug.Log(
                $"[Run Summary] {count.BodyPartType}: " +
                $"{count.TotalCount} total " +
                $"({count.ChuteCount} chute + " +
                $"{count.FridgeCount} fridge).",
                this);
        }
    }

    private BodyPartRunCount GetOrCreate(
        BodyPartType bodyPartType)
    {
        BodyPartRunCount result =
            counts.Find(
                count => count.BodyPartType == bodyPartType);
        if (result != null)
            return result;

        result = new BodyPartRunCount(bodyPartType);
        counts.Add(result);
        return result;
    }

    private void EnsureAllBodyPartTypesExist()
    {
        foreach (BodyPartType bodyPartType in
                 Enum.GetValues(typeof(BodyPartType)))
        {
            GetOrCreate(bodyPartType);
        }
    }
}
