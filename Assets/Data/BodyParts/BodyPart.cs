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

    /// <summary>What this part is called: its authored <see cref="Item.Name"/>, or its <see cref="BodyPartType"/> when that was left blank.</summary>
    /// <remarks>The one place a part's name is decided, so a severed piece, the doctor's line and the
    /// HUD cannot disagree. The type is the fallback rather than an empty string: every part has one,
    /// which makes an unnamed asset read as "Arm" instead of as nothing at all.</remarks>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? bodyPartType.ToString() : Name;
    private void OnValidate()
    {
        itemType = ItemType.BodyPart;
    }
}