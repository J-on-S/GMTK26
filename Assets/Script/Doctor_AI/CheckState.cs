using System.Collections.Generic;
using UnityEngine;

public class CheckState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    [SerializeField] private float minCheckTime;
    [SerializeField] private float maxCheckTime;
    [SerializeField] private bool isTestSawIllegal;
    [ReadOnly] [SerializeField] private float checkTime;
    
    private float waitCheckTime;
    private bool checkIsLooping = false;
    private bool isFinishCheck = false;
    
    public override void EnterState()
    {
        checkTime = 0f;
        isFinishCheck = false;
        checkIsLooping = false;
        waitCheckTime = Random.Range(minCheckTime, maxCheckTime);
        Debug.Log("Doctor check you");
        anim.Play(animName);
    }
    
    public override State UpdateState()
    {
        if (isFinishCheck)
        {
            return stateManager.RandomState(states);
        }

        if (checkIsLooping)
        {
            if (isTestSawIllegal)
            {
                return stateManager.RandomState(states);
            }

            checkTime += Time.deltaTime;
            if (checkTime > waitCheckTime)
            {
                anim.SetTrigger("endChecking");
                checkIsLooping = false;
                //then when it finish it can call switch state
            }
        }
        return this;
    }
    public void SetGetCheckLooping()
    {
        checkIsLooping = true;
    }
    public void SetFinishCheck()
    {
        isFinishCheck = true;
    }
    


    public override void ExitState()
    {
        //some issue with the exit state
        Debug.Log("Doctor found a task.");
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("doctor_checkEnd"))
        {
            anim.SetTrigger("EndCheck");
        }
    } 
    
}
