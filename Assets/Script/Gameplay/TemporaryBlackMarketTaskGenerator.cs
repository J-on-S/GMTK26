using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary implementation used until the full BlackMarketManager exists.
/// Replace this component with any MonoBehaviour implementing
/// IBlackMarketTaskGenerator.
/// </summary>
public class TemporaryBlackMarketTaskGenerator :
    MonoBehaviour,
    IBlackMarketTaskGenerator
{
    [SerializeField, Range(1, 3)] private int maximumDifferentPartTypes = 3;
    [SerializeField] private List<BodyPartType> availableBodyParts = new()
    {
        BodyPartType.Eye,
        BodyPartType.Leg,
        BodyPartType.Heart,
        BodyPartType.Arm,
        BodyPartType.Ear,
        BodyPartType.Hand,
        BodyPartType.Nose
    };

    public BlackMarketTask GenerateTask(int dayNumber)
    {
        List<BodyPartType> choices = GetUniqueParts();
        if (choices.Count == 0)
        {
            Debug.LogWarning("No black-market body parts are configured.", this);
            return new BlackMarketTask(Array.Empty<BodyPartRequest>());
        }

        Shuffle(choices);
        int typeCount = UnityEngine.Random.Range(
            1,
            Mathf.Min(maximumDifferentPartTypes, choices.Count) + 1);
        List<BodyPartRequest> requests = new();

        for (int i = 0; i < typeCount; i++)
        {
            int amount = UnityEngine.Random.Range(
                1,
                BodyPartRequest.MaxAmount + 1);
            requests.Add(new BodyPartRequest(choices[i], amount));
        }

        return new BlackMarketTask(requests);
    }

    private List<BodyPartType> GetUniqueParts()
    {
        List<BodyPartType> result = new();

        foreach (BodyPartType part in availableBodyParts)
        {
            if (!result.Contains(part))
                result.Add(part);
        }

        return result;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }
}
