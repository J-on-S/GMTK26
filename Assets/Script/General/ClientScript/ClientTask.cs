using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

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
            if (requests[i].BodyPartType != bodyPart || deliveredAmounts[i] >= requests[i].Amount)
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
            if (requests[i].BodyPartType == bodyPart)
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

    public string GetRemainingDialogue()
    {
        EnsureProgressExists();
        List<string> remainingRequests = new();

        for (int i = 0; i < requests.Count; i++)
        {
            int remaining = Mathf.Max(
                0,
                requests[i].Amount - deliveredAmounts[i]);

            if (remaining == 0)
                continue;

            remainingRequests.Add(
                $"{remaining} {GetPartName(requests[i].BodyPartType, remaining)}");
        }

        if (remainingRequests.Count == 0)
            return "Request complete.";

        string requestText = remainingRequests.Count == 1
            ? remainingRequests[0]
            : string.Join(
                ", ",
                remainingRequests.GetRange(0, remainingRequests.Count - 1)) +
              " and " +
              remainingRequests[remainingRequests.Count - 1];

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
            text.Append(GetPartName(request.BodyPartType, request.Amount));
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
