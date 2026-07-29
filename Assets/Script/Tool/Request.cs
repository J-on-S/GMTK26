using System;

[Serializable]
public class Request
{
    public float RequestTime { get; set; }
    //Todo for bodypart requestTime;
    public ItemType ItemType { get; }
    public string ItemName {get; set; }
    public Request(ItemType itemType)
    {
        ItemType = itemType;
    }
    public Request(ItemType itemType, string itemName, float requestTime)
    {
        ItemType = itemType;
        RequestTime = requestTime;
        ItemName = itemName;
    }
}