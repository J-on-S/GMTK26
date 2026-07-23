using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum BodyPartType
{
    Eye,
    Leg,
    Heart,
    Arm,
    Ear,
    Hand,
    Nose
}

[Serializable]
public class BodyPartRequest
{
    [SerializeField] private BodyPartType bodyPart;
    [SerializeField, Min(1)] private int amount = 1;

    public BodyPartType BodyPart => bodyPart;
    public int Amount => amount;

    public BodyPartRequest(BodyPartType bodyPart, int amount)
    {
        this.bodyPart = bodyPart;
        this.amount = Mathf.Max(1, amount);
    }

    public void SetAmount(int value)
    {
        amount = Mathf.Max(1, value);
    }
}

[Serializable]
public class ClientTask
{
    [SerializeField] private string clientLine = "Oh, I want {request}.";
    [SerializeField] private List<BodyPartRequest> requests = new();

    [NonSerialized] private List<int> deliveredAmounts;

    public IReadOnlyList<BodyPartRequest> Requests => requests;
    public string ClientLine => clientLine;
    public bool IsComplete
    {
        get
        {
            EnsureProgressExists();
            for (int i = 0; i < requests.Count; i++)
            {
                if (deliveredAmounts[i] < requests[i].Amount)
                    return false;
            }
            return requests.Count > 0;
        }
    }

    public int TotalParts
    {
        get
        {
            int total = 0;
            foreach (BodyPartRequest request in requests)
                total += request.Amount;
            return total;
        }
    }

    public ClientTask(IEnumerable<BodyPartRequest> requests, string clientLine = "Oh, I want {request}.")
    {
        this.requests = new List<BodyPartRequest>(requests);
        this.clientLine = clientLine;
    }

    /// <summary>Delivers one part. Returns false if this task does not need it.</summary>
    public bool TryDeliver(BodyPartType bodyPart)
    {
        EnsureProgressExists();

        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i].BodyPart != bodyPart || deliveredAmounts[i] >= requests[i].Amount)
                continue;

            deliveredAmounts[i]++;
            return true;
        }

        return false;
    }

    public int GetRemainingAmount(BodyPartType bodyPart)
    {
        EnsureProgressExists();
        int remaining = 0;

        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i].BodyPart == bodyPart)
                remaining += Mathf.Max(0, requests[i].Amount - deliveredAmounts[i]);
        }

        return remaining;
    }

    public string GetDialogue()
    {
        string requestText = BuildRequestText();
        return string.IsNullOrWhiteSpace(clientLine)
            ? requestText
            : clientLine.Replace("{request}", requestText);
    }

    private string BuildRequestText()
    {
        StringBuilder text = new();

        for (int i = 0; i < requests.Count; i++)
        {
            if (i > 0)
                text.Append(i == requests.Count - 1 ? " and " : ", ");

            BodyPartRequest request = requests[i];
            text.Append(request.Amount);
            text.Append(' ');
            text.Append(GetPartName(request.BodyPart, request.Amount));
        }

        return text.ToString();
    }

    private static string GetPartName(BodyPartType part, int amount)
    {
        string name = part.ToString().ToLowerInvariant();
        if (amount == 1)
            return name;

        return part == BodyPartType.Nose ? "noses" : name + "s";
    }

    private void EnsureProgressExists()
    {
        if (deliveredAmounts != null && deliveredAmounts.Count == requests.Count)
            return;

        deliveredAmounts = new List<int>(requests.Count);
        for (int i = 0; i < requests.Count; i++)
            deliveredAmounts.Add(0);
    }
}

/// <summary>
/// Stores designer-made tasks and can also generate random client requests.
/// A task can never ask for more than Max Parts Per Task in total.
/// </summary>
public class ClientTaskList : MonoBehaviour
{
    [SerializeField] private ClientTaskDatabase database;

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

        ClientTask task = useHandMadeTasks ? GetRandomTaskFromList() : GenerateRandomTask();
        client.AssignTask(task);
        return task;
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
        if (!HasDatabase())
            return null;

        if (database.TaskTemplates.Count == 0)
            return GenerateRandomTask();

        ClientTask template = database.TaskTemplates[UnityEngine.Random.Range(0, database.TaskTemplates.Count)];
        if (template == null)
        {
            Debug.LogWarning("The selected task template is empty; generating a random task instead.", database);
            return GenerateRandomTask();
        }

        CurrentTask = ClampAndCopy(template);
        TaskAssigned?.Invoke(CurrentTask);
        return CurrentTask;
    }

    /// <summary>Creates a request with unique body-part types and at most six parts.</summary>
    public ClientTask GenerateRandomTask()
    {
        if (!HasDatabase())
            return null;

        List<BodyPartType> choices = GetUniqueAvailableParts();
        if (choices.Count == 0)
        {
            Debug.LogWarning("ClientTaskList has no available body parts.", this);
            CurrentTask = new ClientTask(Array.Empty<BodyPartRequest>());
            return CurrentTask;
        }

        Shuffle(choices);
        int partTotal = UnityEngine.Random.Range(1, database.MaxPartsPerTask + 1);
        int typeCount = UnityEngine.Random.Range(1, Mathf.Min(database.MaxDifferentPartTypes, partTotal, choices.Count) + 1);
        List<BodyPartRequest> requests = new();
        int remaining = partTotal;

        for (int i = 0; i < typeCount; i++)
        {
            int typesStillNeeded = typeCount - i - 1;
            int amount = i == typeCount - 1
                ? remaining
                : UnityEngine.Random.Range(1, remaining - typesStillNeeded + 1);
            requests.Add(new BodyPartRequest(choices[i], amount));
            remaining -= amount;
        }

        CurrentTask = new ClientTask(requests);
        TaskAssigned?.Invoke(CurrentTask);
        return CurrentTask;
    }

    private ClientTask ClampAndCopy(ClientTask source)
    {
        List<BodyPartRequest> result = new();
        int remaining = database.MaxPartsPerTask;

        foreach (BodyPartRequest request in source.Requests)
        {
            if (remaining <= 0)
                break;

            int amount = Mathf.Clamp(request.Amount, 1, remaining);
            result.Add(new BodyPartRequest(request.BodyPart, amount));
            remaining -= amount;
        }

        return new ClientTask(result, source.ClientLine);
    }

    private List<BodyPartType> GetUniqueAvailableParts()
    {
        List<BodyPartType> uniqueParts = new();
        foreach (BodyPartType part in database.AvailableBodyParts)
        {
            if (!uniqueParts.Contains(part))
                uniqueParts.Add(part);
        }
        return uniqueParts;
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
