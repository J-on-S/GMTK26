using System.Collections.Generic;
using UnityEngine;

public class SurgeryState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    [SerializeField] private float minSurgeryTime;
    [SerializeField] private float maxSurgeryTime;
    [ReadOnly] [SerializeField] private float surgeryTime;
    private float waitSurgeryTime;

    public override void EnterState()
    {
        anim.Play(animName);
        surgeryTime = 0;
        waitSurgeryTime = Random.Range(minSurgeryTime, maxSurgeryTime);
        Debug.LogWarning("Doctor starts surgery.");
    }
    public override State UpdateState()
    {   
        surgeryTime += Time.deltaTime;

        if (surgeryTime > waitSurgeryTime)
        {
            return stateManager.RandomState(states);
        }
        return this;
    }
}
