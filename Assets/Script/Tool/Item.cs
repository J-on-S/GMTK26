using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item/Item")]
public class Item: ScriptableObject
{
    [SerializeField] protected string itemName;
    [SerializeField] protected ItemType itemType;
    public string Name => itemName;
    public ItemType Type=> itemType;
    public void SetType(ItemType newItemType) => itemType = newItemType;
    public void SetName(string newName) => itemName = newName;

}
