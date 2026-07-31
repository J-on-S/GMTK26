using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class CheckState : State
{
    [SerializeField] private List<StateWeight> states = new List<StateWeight>();
    [SerializeField] private float minCheckTime;
    [SerializeField] private float maxCheckTime;
    [SerializeField] private bool isTestSawIllegal;
    [SerializeField] private float desiredStartCheckDuration = 3f;
    [SerializeField] private TextMeshProUGUI doctorDialogue;
    [SerializeField] private string doctorAngryDialogue;
    [SerializeField] private IdleState idleState;
    [ReadOnly] [SerializeField] private float checkTime;
    
    
    private float waitCheckTime;
    private bool checkIsLooping = false;
    private bool isFinishCheck = false;
    public Audio startCheckHintAudio;
    

    public override void EnterState()
    {
        Debug.Log("Doctor check you");
        checkTime = 0f;
        isFinishCheck = false;
        checkIsLooping = false;
        AudioEventChannel.Instance.Play(startCheckHintAudio);
        waitCheckTime = Random.Range(minCheckTime, maxCheckTime);
        stateManager.AdjustAnimationTime(anim, animName, desiredStartCheckDuration);
    }
    private bool HasSawYou()
    {
        if (!DisabledDuringMinigame.IsMinigameActive)
        {
            return false;
        }

        if(ToolRequestManager.currentRequest is BodyPartRequest bodyPartRequest)
        {
            return CuttingManager.currentGame.bodyPartType.BodyPartType!=bodyPartRequest.BodyPartType;
            
        }
        
        return true;
    }
    public override State UpdateState()
    {
        if (isFinishCheck)
        {
            return stateManager.RandomState(states);
        }

        if (checkIsLooping)
        {
            if (HasSawYou())
            {
                SawStealBodyPart();
                return idleState;
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
    private string previousDialogue;
    private void SawStealBodyPart()
    {
        Debug.LogError("Saw it");
        CameraSwitch.Instance.SwitchCamera(CameraType.Doctor);
        HealthScript.Instance.TakeDamage(1);
        previousDialogue = doctorDialogue.text;
        doctorDialogue.text = doctorAngryDialogue;
        StartCoroutine(WaitForSwitchBack());

    }
    [SerializeField] private float waitSwitchBackSecond;
    IEnumerator WaitForSwitchBack()
    {
        yield return new WaitForSeconds(waitSwitchBackSecond);
        doctorDialogue.text = previousDialogue;
        CameraSwitch.Instance.SwitchCamera();
    }

    public void SetGetCheckLooping()
    {
        checkIsLooping = true;
        AudioEventChannel.Instance.Stop(startCheckHintAudio);
    }
    public void SetFinishCheck()
    {
        isFinishCheck = true;
    }
    
    public override void ExitState()
    {
        //some issue with the exit state
        Debug.Log("Doctor finish check state.");
        /*AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName("doctor_checkEnd"))
        {
            anim.SetTrigger("EndCheck");
        }*/
    } 
    
}
