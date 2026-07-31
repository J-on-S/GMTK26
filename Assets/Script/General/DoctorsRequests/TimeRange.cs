// Never reference NUnit from this file, or any other runtime script: NUnit's Assert lives in
// nunit.framework, which ships with the test framework and is editor-only. A runtime class that
// uses it compiles fine in the editor and then kills every player build -- the IL2CPP linker
// cannot resolve the assembly and fails with IL1005. Use UnityEngine.Assertions if checks are
// wanted back here.
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
