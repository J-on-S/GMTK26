using System;
using UnityEngine;

[Serializable]
public class StateWeight
{
    [SerializeField] private State state;
    [SerializeField] private float weight = 1;
    public State GetState()
    {
        return state;
    }
    public float GetWeight()
    {
        return weight;
    }

}