using System;
using UnityEngine;

[Serializable]
public class StateWeight
{
    [SerializeField] private State state;
    [SerializeField] private int weight = 1;
    public State GetState()
    {
        return state;
    }
    public int GetWeight()
    {
        return weight;
    }

}