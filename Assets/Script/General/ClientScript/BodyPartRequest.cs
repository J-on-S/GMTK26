
using System;
using UnityEngine;

[Serializable]
public class BodyPartRequest: Request
{
    //flexible one
    [SerializeField] private int maxAmount = 3;
    public const int MaxAmount = 3;
    [SerializeField] private BodyPart bodyPart;
    [Tooltip("Be careful to not exceeded the MaxAmount")]
    [SerializeField, Range(1, 10)] private int amount = 1;
    public BodyPart BodyPart => bodyPart;
    public BodyPartType BodyPartType => bodyPart.BodyPartType;
    public int GetMaxAmount => maxAmount;
    public int Amount => amount;
    public void AddAmount()
    {
        amount++;
    }
    public BodyPartRequest(BodyPartType bodyPartType, int amount, int maxAmount): base(ItemType.BodyPart)
    {
        this.bodyPart = BodyParts.Instance.SearchBodyPart(bodyPartType);
        this.amount = Mathf.Clamp(amount, 1, maxAmount);
        this.maxAmount = maxAmount;
        ItemName = bodyPart.Name;
    }

    public BodyPartRequest(BodyPartType bodyPartType, int amount): base(ItemType.BodyPart)
    {
        this.bodyPart = BodyParts.Instance.SearchBodyPart(bodyPartType);;
        this.amount = Mathf.Clamp(amount, 1, MaxAmount);
        maxAmount = MaxAmount;
        ItemName = bodyPart.Name;
    }

    public void SetAmount(int value)
    {
        amount = Mathf.Clamp(value, 1, maxAmount);
    }
    public bool RemoveAmount()
    {
        if (amount - 1 < 0)
        {
            return false;
        }
        amount--;
        return true;
    }
}