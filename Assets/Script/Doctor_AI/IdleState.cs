using System.Collections.Generic;
using UnityEngine;
public class IdleState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    [SerializeField] private WalkState walkState;
    [SerializeField] private float minIdleTime;
    [SerializeField] private float maxIdleTime;
    [ReadOnly] [SerializeField] private float idleTime;
    private float waitIdleTime;

    public override void EnterState()
    {
        idleTime = 0f;
        waitIdleTime = Random.Range(minIdleTime, maxIdleTime);
        Debug.Log("Doctor starts idle.");
        anim.Play(animName);
    }

    public override State UpdateState()
    {
        idleTime += Time.deltaTime;

        if (idleTime > waitIdleTime)
        {
            return stateManager.RandomState(states);
        }
        return this;
    }

    public override void ExitState()
    {
        Debug.Log("Doctor found a task.");
    }
}