using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
[CreateAssetMenu(fileName = "BodyParts", menuName = "Scriptable Objects/BodyParts")]
public class BodyParts : ScriptableObject
{
    private static BodyParts instance;
    public static BodyParts Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<BodyParts>("BodyParts");

            return instance;
        }
    }
    [SerializeField] List<BodyPart> bodyParts = new List<BodyPart>();

    [Header("Generation rules")]
    [Tooltip(
        "These types are excluded from generated client tasks, hand-made " +
        "client templates, and black-market tasks.")]
    [SerializeField]
    private List<BodyPartType> disabledTaskBodyParts = new();

    public IReadOnlyList<BodyPartType> DisabledTaskBodyParts =>
        disabledTaskBodyParts;

    public bool IsTaskGenerationEnabled(BodyPartType bodyPartType)
    {
        return !disabledTaskBodyParts.Contains(bodyPartType);
    }

    public BodyPart SearchBodyPart(BodyPartType bodyPartType)
    {
        foreach(BodyPart bodyPart in bodyParts)
        {
            if(bodyPart.BodyPartType == bodyPartType)
            {
                return bodyPart;
            }
        }
        Debug.LogError("We didn't found BodyPart, need to assign in the scriptable: "+bodyPartType);
        return null;
    }

    private void OnValidate()
    {
        for (int i = disabledTaskBodyParts.Count - 1; i >= 0; i--)
        {
            if (disabledTaskBodyParts.IndexOf(
                    disabledTaskBodyParts[i]) != i)
            {
                disabledTaskBodyParts.RemoveAt(i);
            }
        }
    }
}
