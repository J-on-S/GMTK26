using System;
using System.Collections.Generic;
using UnityEngine;



public class DisabledDuringMinigame : MonoBehaviour
{
    public List<MonoBehaviour> ToDeactivate;
    public static bool IsMinigameActive { get; private set; }

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
        IsMinigameActive = true;
        ToDeactivate.ForEach(mb => mb.enabled = false);
    }

    void Resume(CuttingManager cm)
    {
        IsMinigameActive = false;
        ToDeactivate.ForEach(mb => mb.enabled = true);
    }


}