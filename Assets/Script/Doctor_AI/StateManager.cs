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
        int stateIndex = Random.Range(0, states.Count);
        State randomState = states[stateIndex].GetState();
        return randomState;
    }
}