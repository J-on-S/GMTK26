using System.Collections.Generic;
using UnityEngine;

public class CheckState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    private Animator anim;
    [SerializeField] private float minCheckTime;
    [SerializeField] private float maxCheckTime;
    private float checkTime;
    private float waitCheckTime;
    private bool checkIsLooping = false;
    [SerializeField] private bool isTestSawIllegal;

    private void Start()
    {
        anim = transform.parent.parent.GetComponent<Animator>();
    }
    private bool hasAlreadyExit;
    public override void EnterState()
    {
        checkTime = 0f;
        checkIsLooping = false;
        waitCheckTime = Random.Range(minCheckTime, maxCheckTime);
        Debug.Log("Doctor check you");
        anim.SetTrigger("StartCheck");
    }
    
    public override State UpdateState()
    {
        if (checkIsLooping)
        {
            if (isTestSawIllegal)
            {
                return stateManager.RandomState(states);
            }

            checkTime += Time.deltaTime;
            if (checkTime > waitCheckTime)
            {
                anim.SetTrigger("EndCheck");
                checkIsLooping = false;
                //then when it finish it can call switch state
            }
        }
        return this;
    }
    
    public override void ExitState()
    {
        //some issue with the exit state
        Debug.Log("Doctor found a task.");
        anim.SetTrigger("EndCheck");
    } 
    
}
