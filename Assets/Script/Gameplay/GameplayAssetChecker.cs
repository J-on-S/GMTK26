using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validates required scene references before GameplayManager starts a day.
/// Scene objects are checked through explicit Inspector references rather than
/// tags or global scene searches.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GameplayManager))]
public class GameplayAssetChecker : MonoBehaviour
{
    public const int RequiredBedCount = 2;

    [Header("Required scene objects")]
    [SerializeField] private OperationChair[] operationChairs =
        new OperationChair[RequiredBedCount];

    [Header("Required world assets")]
    [Tooltip("The room's trapdoor/chute GameObject.")]
    [SerializeField] private GameObject trapdoor;
    [Tooltip("The room's body-part storage GameObject.")]
    [SerializeField] private GameObject storage;
    [Tooltip("At least one scalpel, knife, scissors, or other cutting tool.")]
    [SerializeField] private GameObject[] cuttingTools = new GameObject[1];

    [Header("Optional world assets")]
    [Tooltip("Optional world-space poster that displays the generated client list.")]
    [SerializeField] private GameObject clientListPoster;

    public IReadOnlyList<OperationChair> OperationChairs => operationChairs;
    public GameObject Trapdoor => trapdoor;
    public GameObject Storage => storage;
    public IReadOnlyList<GameObject> CuttingTools => cuttingTools;
    public GameObject ClientListPoster => clientListPoster;

    public bool ValidateSetup()
    {
        return ValidateSetup(GetComponent<GameplayManager>(), true);
    }

    [ContextMenu("Validate Gameplay Setup")]
    private void ValidateFromContextMenu()
    {
        ValidateSetup();
    }

    public bool ValidateSetup(
        GameplayManager gameplayManager,
        bool logErrors = true)
    {
        List<string> errors = new();
        List<string> warnings = new();

        if (gameplayManager == null)
            errors.Add("GameplayAssetChecker requires GameplayManager.");

        if (trapdoor == null)
            errors.Add("A trapdoor/chute asset is required.");

        if (storage == null)
            errors.Add("A body-part storage asset is required.");

        if (!HasAtLeastOneCuttingTool())
            errors.Add("At least one cutting-tool asset is required.");

        if (clientListPoster == null)
        {
            warnings.Add(
                "Optional client-list poster is not assigned.");
        }

        if (operationChairs == null ||
            operationChairs.Length != RequiredBedCount)
        {
            int actualCount =
                operationChairs == null ? 0 : operationChairs.Length;
            errors.Add(
                $"Exactly {RequiredBedCount} operation chairs are required; " +
                $"{actualCount} assigned.");
        }
        else
        {
            RandomizedClientList expectedClientList =
                gameplayManager != null ? gameplayManager.ClientList : null;

            for (int i = 0; i < operationChairs.Length; i++)
            {
                OperationChair chair = operationChairs[i];
                if (chair == null)
                {
                    errors.Add($"Operation Chair slot {i + 1} is empty.");
                    continue;
                }

                if (!chair.ValidateConfiguration(
                        gameplayManager,
                        expectedClientList,
                        out string chairError))
                {
                    errors.Add(chairError);
                }
            }

            if (operationChairs[0] != null &&
                operationChairs[0] == operationChairs[1])
            {
                errors.Add(
                    "The two Operation Chair slots reference the same bed.");
            }
        }

        if (logErrors && warnings.Count > 0)
        {
            Debug.LogWarning(
                "Gameplay setup warnings:\n- " +
                string.Join("\n- ", warnings),
                this);
        }

        if (errors.Count == 0)
        {
            Debug.Log(
                "Gameplay setup validated: beds and required world assets are configured.",
                this);
            return true;
        }

        if (logErrors)
        {
            Debug.LogError(
                "Gameplay setup is invalid:\n- " +
                string.Join("\n- ", errors),
                this);
        }

        return false;
    } 

    private bool HasAtLeastOneCuttingTool()
    {
        if (cuttingTools == null || cuttingTools.Length == 0)
            return false;

        foreach (GameObject cuttingTool in cuttingTools)
        {
            if (cuttingTool != null)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        // Provides immediate feedback while wiring the scene in the Inspector.
        ValidateSetup(GetComponent<GameplayManager>(), true);
    }
}
