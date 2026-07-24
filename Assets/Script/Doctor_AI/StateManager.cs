using System.Collections.Generic;
using UnityEngine;

public class StateManager : MonoBehaviour
{
    [SerializeField] private State currentState;

    private void Start()
    {
        if (currentState != null)
        {
            currentState.Initialize(this);
            currentState.EnterState();
        }
    }

    private void Update()
    {
        RunStateMachine();
    }

    private void RunStateMachine()
    {
        if (currentState == null)
            return;

        State nextState = currentState.UpdateState();

        if (nextState != null && nextState != currentState)
        {
            SwitchState(nextState);
        }
    }

    public void SwitchState(State nextState)
    {
        currentState.ExitState();

        nextState.Initialize(this);

        currentState = nextState;

        currentState.EnterState();
    }

    public State RandomState(List<StateWeight> states)
{
    // Calculate total weight
    int totalWeight = 0;
    foreach (StateWeight state in states)
    {
        totalWeight += state.GetWeight();
    }

    // Pick a random value
    int random = Random.Range(0, totalWeight);

    // Find the selected state
    foreach (StateWeight state in states)
    {
        if (random < state.GetWeight())
        {
            return state.GetState();
        }

        random -= state.GetWeight();
    }

    // Should never happen
    return states[0].GetState();
}
}