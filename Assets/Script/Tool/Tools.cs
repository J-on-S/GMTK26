using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "Tools", menuName = "Scriptable Objects/Tools")]
public class Tools: ScriptableObject
{
    private static Tools instance;
    public static Tools Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<Tools>("Tools");

            return instance;
        }
    }
    public List<Tool> tools = new List<Tool>();
    public Tool RandomTool()
    {
        int randomIndex = UnityEngine.Random.Range(0, tools.Count);
        return tools[randomIndex];
    }
    public int Count => tools.Count;
}