using UnityEngine;

[System.Serializable]
public class TimeRange
{
    public float min;
    public float max;
    [ReadOnly] public float currentValue;
    public float RandomValue()
    {
        currentValue = Random.Range(min, max);
        return currentValue;
    }
    public TimeRange(float defaultValue)
    {
        min = defaultValue;
        max = defaultValue;
    }
    public TimeRange(float minTime, float maxTime)
    {
        min = minTime;
        max = maxTime;
    }
}