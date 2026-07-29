using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "Tool", menuName = "Scriptable Objects/Tool")]
public class Tool: Item//ScriptableObject
{
    public ToolType toolType;
    public TimeRange toolTime = new TimeRange(30f);
    private void OnValidate()
    {
        itemType = ItemType.Tool;
    }
}