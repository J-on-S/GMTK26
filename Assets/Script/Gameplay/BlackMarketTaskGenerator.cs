using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Contract the future black-market system must implement.
/// GameplayManager only depends on this API, not on a specific implementation.
/// </summary>
public interface IBlackMarketTaskGenerator
{
    BlackMarketTask GenerateTask(int dayNumber);
}

[Serializable]
public class BlackMarketTask
{
    [SerializeField] private List<BodyPartRequest> requestedParts = new();

    public IReadOnlyList<BodyPartRequest> RequestedParts => requestedParts;

    public BlackMarketTask(IEnumerable<BodyPartRequest> requestedParts)
    {
        this.requestedParts = new List<BodyPartRequest>(requestedParts);
    }

    public string GetDescription()
    {
        StringBuilder text = new("Black market wants ");

        for (int i = 0; i < requestedParts.Count; i++)
        {
            if (i > 0)
                text.Append(i == requestedParts.Count - 1 ? " and " : ", ");

            BodyPartRequest request = requestedParts[i];
            string partName = request.BodyPart.Name.ToLowerInvariant();

            text.Append(request.Amount);
            text.Append(' ');
            text.Append(partName);

            if (request.Amount != 1)
                text.Append('s');
        }

        return text.Append('.').ToString();
    }
}
