using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project asset containing task-generation settings shared by every scene.
/// Create one with Assets > Create > GMTK26 > Client Task Database.
/// </summary>
[CreateAssetMenu(fileName = "ClientTaskDatabase", menuName = "GMTK26/Client Task Database")]
public class ClientTaskDatabase : ScriptableObject
{
    [Header("Limits")]
    [SerializeField, Range(1, 6)] private int maxPartsPerTask = 6;
    [SerializeField, Range(1, 3)] private int maxDifferentPartTypes = 2;

    [Header("Available body parts")]
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

    [Header("Optional hand-made tasks")]
    [SerializeField] private List<ClientTask> taskTemplates = new();

    public int MaxPartsPerTask => Mathf.Clamp(maxPartsPerTask, 1, 6);
    public int MaxDifferentPartTypes => Mathf.Clamp(maxDifferentPartTypes, 1, 3);
    public IReadOnlyList<BodyPartType> AvailableBodyParts => availableBodyParts;
    public IReadOnlyList<ClientTask> TaskTemplates => taskTemplates;

    private void OnValidate()
    {
        maxPartsPerTask = Mathf.Clamp(maxPartsPerTask, 1, 6);
        maxDifferentPartTypes = Mathf.Clamp(maxDifferentPartTypes, 1, 3);
    }
}
