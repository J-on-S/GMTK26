using System.Collections.Generic;
using UnityEngine;

public class SurgeryState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    private Animator anim;
    [SerializeField] private float minSurgeryTime;
    [SerializeField] private float maxSurgeryTime;
    private float surgeryTime;
    private float waitSurgeryTime;
    private void Start()
    {
        anim = transform.parent.parent.GetComponent<Animator>();
    }
    public override void EnterState()
    {
        anim.SetTrigger("doSurgery");
        surgeryTime = 0;
        Debug.Log("Doctor starts idle.");
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
