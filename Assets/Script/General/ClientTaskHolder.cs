using System;
using UnityEngine;

/// <summary>Owns the independent runtime task belonging to one spawned client.</summary>
public class ClientTaskHolder : MonoBehaviour
{
    public ClientTask AssignedTask { get; private set; }
    public bool HasTask => AssignedTask != null;

    public event Action<ClientTask> TaskAssigned;
    public event Action<ClientTask> TaskCompleted;

    public void AssignTask(ClientTask task)
    {
        AssignedTask = task;
        TaskAssigned?.Invoke(task);
    }

    public bool GiveBodyPart(BodyPartType bodyPart)
    {
        if (AssignedTask == null || !AssignedTask.TryDeliver(bodyPart))
            return false;

        if (AssignedTask.IsComplete)
            TaskCompleted?.Invoke(AssignedTask);

        return true;
    }

    public void ClearTask()
    {
        AssignedTask = null;
    }
}
