using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the black-market body-part task for the current day.
/// </summary>
public class BlackMarketGenerator :
    MonoBehaviour,
    IBlackMarketTaskGenerator
{
    [SerializeField, Range(1, 8)] private int maximumDifferentPartTypes = 3;
    [SerializeField] private int MaxPerType = 3;
    [SerializeField] private int minNbBodyParts = 1;
    [SerializeField] private int maxNbBodyParts = 3;
    [SerializeField] private Material notFoundMat; 
    [SerializeField] private int totalParts = 12;
    //[SerializeField, Range(1, 3)] private int maximumDifferentPartTypes = 3;
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
    [SerializeField] private BlackMarketTask currentBlackMarketTask;
    [SerializeField] private Transform bodyPartsPosParent;
    [ReadOnly] [SerializeField] private List<Transform> bodyPartsPos = new List<Transform>();
    private Dictionary<BodyPartType, List<BodyPartContain>> bodyPartsContainByType = new Dictionary<BodyPartType, List<BodyPartContain>>();
    
    [SerializeField] private BodyParts bodyParts;
    [Header("Test")]
    [SerializeField] private BodyPartType bodyPartTypeFound;
    public void Start()
    {
        foreach (Transform bodyPartsPosChild in bodyPartsPosParent)
        {
            bodyPartsPos.Add(bodyPartsPosChild);
        }
    }
    [ContextMenu("Generate Black Market")]
    public void GenerateTask()
    {
        currentBlackMarketTask = GenerateTask(1);
        GenerateBodyPartContains();
    }
    public BlackMarketTask GenerateTask(int dayNumber)
    {
        List<BodyPartType> choices = GetUniqueParts();

        if (choices.Count == 0)
        {
            Debug.LogWarning("No black-market body parts are configured.", this);
            return new BlackMarketTask(Array.Empty<BodyPartRequest>());
        }

        Shuffle(choices);

        
        List<BodyPartRequest> requests = new();
        int currentNbParts = totalParts;
        while (currentNbParts > 0)
        {
            // Pick a random body part type
            BodyPartType part = choices[UnityEngine.Random.Range(0, choices.Count)];

            // Find an existing request for that type
            BodyPartRequest request = requests.Find(r => r.BodyPart == part);

            if (request == null)
            {
                request = new BodyPartRequest(part, 0, MaxPerType);
                requests.Add(request);
                currentNbParts--;
                continue;
            }

            // Skip if this type has reached its limit
            if (request.Amount >= request.GetMaxAmount)
                continue;

            request.AddAmount();
            Debug.Log($"{request.BodyPart}: {request.Amount}");
            currentNbParts--;
            Debug.Log("currentNbParts: "+currentNbParts);
        }

        return new BlackMarketTask(requests);
    }
    public bool IsSucceedBlackMarket()
    {
        foreach (KeyValuePair<BodyPartType, List<BodyPartContain>> pair in bodyPartsContainByType)
        {
            BodyPartType type = pair.Key;
            List<BodyPartContain> bodyPartsContain = pair.Value;
            foreach(BodyPartContain bodyPartContain in bodyPartsContain)
            {
                if (!bodyPartContain.HasBodyPart)
                {
                    return false;
                }
            }
        }
        return true;
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
    public void GenerateBodyPartContains()
    {
        int pos_index = 0;
        bodyPartsContainByType = new Dictionary<BodyPartType, List<BodyPartContain>>();
        foreach (BodyPartRequest bodyPartRequest in currentBlackMarketTask.RequestedParts)
        {
            BodyPartType bodyPartType = bodyPartRequest.BodyPart;
            int amountBodyPartOfThisType = bodyPartRequest.Amount;
            BodyPart bodyPart = bodyParts.SearchBodyPart(bodyPartType);
            if(!bodyPart) continue;
            List<BodyPartContain> currentBodyPartTypeContains = new List<BodyPartContain>();

            for(int i=0; i < amountBodyPartOfThisType; i++)
            {
                //TODO: rotation of bodypart in black market
                Debug.Log("pos_index: "+pos_index);
                Transform bodyPartsTransform = bodyPartsPos[pos_index];
                // GameObject newBodyPartObj = Instantiate(bodyPart.BodyPartPrefab, bodyPartsTransform.position, Quaternion.identity, bodyPartsTransform);
                // newBodyPartObj.position = 
                GameObject newBodyPartObj = Instantiate(bodyPart.BodyPartPrefab, bodyPartsTransform);
                newBodyPartObj.transform.localPosition = Vector3.zero;
                newBodyPartObj.transform.localRotation = Quaternion.Euler(bodyPart.rotation);
                newBodyPartObj.transform.localScale = Vector3.one * bodyPart.size;
                Renderer renderer;
                if (newBodyPartObj.transform.childCount > 0)
                {
                    renderer = newBodyPartObj.transform.GetChild(0).GetComponent<Renderer>();
                }
                else
                {
                    renderer = newBodyPartObj.GetComponent<Renderer>();
                }
                 
                Material originalMat = renderer.material;
                if (renderer != null)
                {
                    renderer.material = notFoundMat;
                }
                pos_index++;
                BodyPartContain newBodyPartContain = new BodyPartContain(bodyPart, newBodyPartObj, originalMat);
                currentBodyPartTypeContains.Add(newBodyPartContain);
            }
            bodyPartsContainByType.Add(bodyPartType, currentBodyPartTypeContains);
        }
    }
    [ContextMenu("Add BodyPart in test")]
    public void TestAddBodyPartInBlackMarket()
    {
        AddBodyPartInBlackMarket(bodyPartTypeFound);
    }
    public void AddBodyPartInBlackMarket(BodyPartType newBodyPartType)
    {
        if (bodyPartsContainByType.TryGetValue(newBodyPartType, out var listBodyPartContain)) {
            foreach(BodyPartContain bodyPartContain in listBodyPartContain)
            {
                if (!bodyPartContain.HasBodyPart)
                {
                    GameObject spawnedBodyPartObj = bodyPartContain.SpawnedBodyPartObj;
                    Renderer renderer = spawnedBodyPartObj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = bodyPartContain.OriginalMat;
                    }
                    bodyPartContain.HasBodyPart = true;
                    return;
                }
            }
            Debug.LogWarning("This body part is already full or not needed");
        } else {
            Debug.LogWarning("This body part is already full or not needed");
        }
    }
}
