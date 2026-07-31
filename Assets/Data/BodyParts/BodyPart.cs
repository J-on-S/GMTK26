using System;
using UnityEngine;
[Serializable]
[CreateAssetMenu(fileName = "BodyPart", menuName = "Scriptable Objects/BodyPart")]
public class BodyPart : Item//ScriptableObject
{
    [SerializeField] private BodyPartType bodyPartType;
    [SerializeField] private GameObject bodyPartPrefab;
    [SerializeField] private GameObject BodyPart2DPrefab;
    public TimeRange bodyPartTime = new TimeRange(50f);
    public Vector3 rotation;
    public float size;
    public Sprite bodyPartImg;
    //maybe mat
    public BodyPartType BodyPartType => bodyPartType;
    public GameObject BodyPartPrefab => bodyPartPrefab;
    private void OnValidate()
    {
        itemType = ItemType.BodyPart;
    }
}