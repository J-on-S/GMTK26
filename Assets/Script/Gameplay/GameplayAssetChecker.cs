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

    public IReadOnlyList<OperationChair> OperationChairs => operationChairs;

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

        if (gameplayManager == null)
            errors.Add("GameplayAssetChecker requires GameplayManager.");

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

        if (errors.Count == 0)
        {
            Debug.Log(
                "Gameplay setup validated: exactly two beds are configured.",
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

    private void OnValidate()
    {
        // Provides immediate feedback while wiring the scene in the Inspector.
        ValidateSetup(GetComponent<GameplayManager>(), true);
    }
}
