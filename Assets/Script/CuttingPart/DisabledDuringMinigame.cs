using System;
using System.Collections.Generic;
using UnityEngine;



public class DisabledDuringMinigame : MonoBehaviour
{
    public List<MonoBehaviour> ToDeactivate;


    void OnEnable()
    {
        CuttingManager.OnMinigameEntered += Suspend;
        CuttingManager.OnMinigameExited += Resume;
    }

    void OnDisable()
    {
        CuttingManager.OnMinigameEntered -= Suspend;
        CuttingManager.OnMinigameExited -= Resume;
    }

    void Suspend(CuttingManager cm)
    {
        ToDeactivate.ForEach(mb => mb.enabled = false);
    }

    void Resume(CuttingManager cm)
    {
        ToDeactivate.ForEach(mb => mb.enabled = true);
    }


}