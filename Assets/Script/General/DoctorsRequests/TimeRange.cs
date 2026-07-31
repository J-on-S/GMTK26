// UnityEngine.Assertions, not NUnit: NUnit's Assert lives in nunit.framework, which ships with the
// test framework and is editor-only. A runtime class referencing it compiles in the editor and then
// kills every player build -- the IL2CPP linker cannot resolve the assembly and fails with IL1005.
using UnityEngine;
using UnityEngine.Assertions;

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