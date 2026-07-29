using NUnit.Framework;
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
        Assert.IsTrue(minTime <= maxTime, "minTime must be less than or equal to maxTime.");
        Assert.IsTrue(minTime >= 0, "minTime must more or equal to 0");
        Assert.IsTrue(maxTime >= 0, "maxTime must more or equal to 0");
        
        min = minTime;
        max = maxTime;
    }
}