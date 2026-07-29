using UnityEngine;

[System.Serializable]
public class IntRange
{
    public int min;
    public int max;
    [ReadOnly] public int currentValue;
    public int RandomValue()
    {
        currentValue = Random.Range(min, max);
        return currentValue;
    }
    public IntRange(int defaultValue)
    {
        min = defaultValue;
        max = defaultValue;
    }
}