using System.Collections.Generic;
using UnityEngine;

public class SpawnBodyPartCustomer : MonoBehaviour
{
    [SerializeField] private BodyPartSpawn earsSpawn;
    [SerializeField] private BodyPartSpawn nosesSpawn;
    [SerializeField] private BodyPartSpawn legsSpawn;
    [SerializeField] private BodyPartSpawn armsSpawn;
    [SerializeField] private BodyPartSpawn heartSpawn;

    [Header("Test")]
    [SerializeField] private BodyPartType TestBodyPart;
    public void Start()
    {
        earsSpawn.Initialize();
        nosesSpawn.Initialize();
        legsSpawn.Initialize();
        armsSpawn.Initialize();
        heartSpawn.Initialize();
    }

    [ContextMenu("AddBodyPart")]
    public void AddBodyPartTest()
    {
        AddBodyPart(TestBodyPart);
    }
    public void AddBodyPart(string newBodyPart)
    {
        switch (newBodyPart)
        {
            case "Leg":
                RandomBodyPartPos(legsSpawn);
                break;
            case "Ear":
                RandomBodyPartPos(earsSpawn);
                break;
            case "Arm":
                RandomBodyPartPos(armsSpawn);
                break;
            case "Heart":
                RandomBodyPartPos(heartSpawn);
                break;
            case "Nose":
                RandomBodyPartPos(nosesSpawn);
                break;
            default:
                Debug.LogError("Hey");
                break;
        }
    }
    public void AddBodyPart(BodyPartType newBodyPart)
    {
        switch (newBodyPart)
        {
            case BodyPartType.Leg:
                RandomBodyPartPos(legsSpawn);
                break;
            case BodyPartType.Ear:
                RandomBodyPartPos(earsSpawn);
                break;
            case BodyPartType.Arm:
                RandomBodyPartPos(armsSpawn);
                break;
            case BodyPartType.Heart:
                RandomBodyPartPos(heartSpawn);
                break;
            case BodyPartType.Nose:
                RandomBodyPartPos(nosesSpawn);
                break;
            default:
                Debug.LogError("Hey");
                break;
        }
    }
    public void RandomBodyPartPos(BodyPartSpawn bodyPartSpawn)
    {
        List<BodyPartPos> bodiesPartPos = bodyPartSpawn.bodiesPos;
        if (CheckFullBodyPart(bodiesPartPos))
        {
            return;
        }
        int indexBodyPart = Random.Range(0, bodiesPartPos.Count);
        BodyPartPos currentBodyPos = bodiesPartPos[indexBodyPart];
        while (currentBodyPos.hasBodyPart)
        {
            indexBodyPart = Random.Range(0, bodiesPartPos.Count);
            currentBodyPos = bodiesPartPos[indexBodyPart];
        }
        bodiesPartPos[indexBodyPart].hasBodyPart = true;
        Instantiate(bodyPartSpawn.bodyPartPrefab, currentBodyPos.bodyPartPos.position, currentBodyPos.bodyPartPos.rotation, transform);
    }
    public bool CheckFullBodyPart(List<BodyPartPos> bodiesPartPos)
    {
        foreach(BodyPartPos bodyPartPos in bodiesPartPos)
        {
            if (!bodyPartPos.hasBodyPart)
            {
                return false;
            }
        }

        return true;
    }
}
