using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores designer-made tasks and can also generate random client requests.
/// A task can never ask for more than Max Parts Per Task in total.
/// </summary>
public class ClientTaskList : MonoBehaviour
{
    [SerializeField] private ClientTaskDatabase database;
    [SerializeField] private BodyParts bodyParts;
    public ClientTaskDatabase Database => database;
    public ClientTask CurrentTask { get; private set; }
    public event Action<ClientTask> TaskAssigned;
    public event Action<ClientTask> TaskCompleted;

    public ClientTask AssignRandomTask(ClientTaskHolder client, bool useHandMadeTasks = true)
    {
        if (client == null)
        {
            Debug.LogWarning("Cannot assign a task to a missing client.", this);
            return null;
        }

        ClientTask task = CreateTask(useHandMadeTasks);
        if (task == null)
            return null;

        SetCurrentTask(task);
        client.AssignTask(task);
        return task;
    }

    /// <summary>
    /// Creates task data without assigning it to a client or spawning anything.
    /// This is used when preparing the client/task queue ahead of time.
    /// </summary>
    public ClientTask CreateTask(bool useHandMadeTasks = true)
    {
        if (!HasDatabase())
            return null;

        if (!useHandMadeTasks || database.TaskTemplates.Count == 0)
            return CreateGeneratedTask();

        ClientTask template =
            database.TaskTemplates[UnityEngine.Random.Range(0, database.TaskTemplates.Count)];

        if (template == null)
        {
            Debug.LogWarning(
                "The selected task template is empty; generating a random task instead.",
                database);
            return CreateGeneratedTask();
        }

        ClientTask filteredTask = ClampAndCopy(template);
        if (filteredTask.TotalParts > 0)
            return filteredTask;

        Debug.LogWarning(
            "The selected hand-made task only contains globally disabled " +
            "body parts. Generating a random enabled task instead.",
            database);
        return CreateGeneratedTask();
    }

    public bool DeliverBodyPart(BodyPartType bodyPart)
    {
        if (CurrentTask == null || !CurrentTask.TryDeliver(bodyPart))
            return false;

        if (CurrentTask.IsComplete)
            TaskCompleted?.Invoke(CurrentTask);

        return true;
    }

    /// <summary>Chooses and copies a hand-made task, clamped to six total parts.</summary>
    public ClientTask GetRandomTaskFromList()
    {
        ClientTask task = CreateTask(true);
        SetCurrentTask(task);
        return task;
    }

    /// <summary>Creates a request with unique body-part types and at most six parts.</summary>
    public ClientTask GenerateRandomTask()
    {
        ClientTask task = CreateTask(false);
        SetCurrentTask(task);
        return task;
    }

    private ClientTask CreateGeneratedTask()
    {
        List<BodyPartType> choices = GetUniqueAvailableParts();
        if (choices.Count == 0)
        {
            Debug.LogWarning("ClientTaskList has no available body parts.", this);
            return new ClientTask(Array.Empty<BodyPartRequest>());
        }

        Shuffle(choices);
        int maximumPossibleTotal = Mathf.Min(
            database.MaxPartsPerTask,
            database.MaxDifferentPartTypes * BodyPartRequest.MaxAmount,
            choices.Count * BodyPartRequest.MaxAmount);
        int partTotal = UnityEngine.Random.Range(1, maximumPossibleTotal + 1);
        int minimumTypeCount =
            (partTotal + BodyPartRequest.MaxAmount - 1) /
            BodyPartRequest.MaxAmount;
        int maximumTypeCount = Mathf.Min(
            database.MaxDifferentPartTypes,
            partTotal,
            choices.Count);
        int typeCount = UnityEngine.Random.Range(
            minimumTypeCount,
            maximumTypeCount + 1);
        List<BodyPartRequest> requests = new();
        int remaining = partTotal;

        for (int i = 0; i < typeCount; i++)
        {
            int typesStillNeeded = typeCount - i - 1;
            int minimumAmount = Mathf.Max(
                1,
                remaining - typesStillNeeded * BodyPartRequest.MaxAmount);
            int maximumAmount = Mathf.Min(
                BodyPartRequest.MaxAmount,
                remaining - typesStillNeeded);
            int amount = UnityEngine.Random.Range(
                minimumAmount,
                maximumAmount + 1);
            requests.Add(new BodyPartRequest(choices[i], amount));
            remaining -= amount;
        }

        return new ClientTask(requests);
    }

    private void SetCurrentTask(ClientTask task)
    {
        CurrentTask = task;
        if (task != null)
            TaskAssigned?.Invoke(task);
    }

    private ClientTask ClampAndCopy(ClientTask source)
    {
        List<BodyPartRequest> result = new();
        int remaining = database.MaxPartsPerTask;

        foreach (BodyPartRequest request in source.Requests)
        {
            if (remaining <= 0)
                break;

            if (!IsTaskGenerationEnabled(request.BodyPartType))
                continue;

            int amount = Mathf.Clamp(
                request.Amount,
                1,
                Mathf.Min(BodyPartRequest.MaxAmount, remaining));
            result.Add(new BodyPartRequest(request.BodyPartType, amount));
            remaining -= amount;
        }

        return new ClientTask(result, source.ClientLine);
    }

    private List<BodyPartType> GetUniqueAvailableParts()
    {
        List<BodyPartType> uniqueParts = new();
        foreach (BodyPartType part in database.AvailableBodyParts)
        {
            if (IsTaskGenerationEnabled(part) &&
                !uniqueParts.Contains(part))
            {
                uniqueParts.Add(part);
            }
        }
        return uniqueParts;
    }

    private bool IsTaskGenerationEnabled(BodyPartType bodyPartType)
    {
        BodyParts generationRules =
            bodyParts != null ? bodyParts : BodyParts.Instance;
        return generationRules == null ||
               generationRules.IsTaskGenerationEnabled(bodyPartType);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }

    private bool HasDatabase()
    {
        if (database != null)
            return true;

        Debug.LogWarning("Assign a ClientTaskDatabase to ClientTaskList.", this);
        return false;
    }
}
