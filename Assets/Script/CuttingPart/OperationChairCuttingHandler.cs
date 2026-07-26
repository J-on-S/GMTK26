using System.Collections.Generic;
using UnityEngine;

/// <summary>Gives every client placed in an operation chair the cuts authored for its body shape.</summary>
/// <remarks>
/// Clients are spawned from <c>RandomizedClientList</c> at runtime, so their cuts cannot be authored in
/// the scene: nothing exists to author against until a chair spawns one. This listens for that moment
/// and copies the cuts in from a prefab, through <see cref="CutCopier"/>.
/// <para>
/// No matching: every placed client takes the cuts of the first assigned template, whatever mesh its
/// body wears. This assumes the clients share the shape the template's cuts were authored against.
/// </para>
/// </remarks>
[DisallowMultipleComponent]
public class OperationChairCuttingHandler : MonoBehaviour
{
    [Tooltip("Chairs to watch. Left empty, every OperationChair in the scene is watched.")]
    public List<OperationChair> chairs = new();

    [Tooltip("Prefabs holding authored cuts. Every placed client takes the cuts of the first assigned prefab -- no mesh matching.")]
    public List<GameObject> cutTemplates = new();

    [Tooltip("Delete the cuts a client already carries before copying. On, since a client that arrives with cuts of its own would otherwise end up with both sets over each other.")]
    public bool replaceExistingCuts = true;

    /// <summary>Chairs this component is currently subscribed to, so it unsubscribes from exactly those.</summary>
    /// <remarks>Kept separately from <see cref="chairs"/>: that list can be edited, and unsubscribing
    /// from the edited version would leave a live subscription on a chair dropped from it.</remarks>
    private readonly List<OperationChair> subscribed = new();

    void OnEnable()
    {
        foreach (OperationChair chair in ChairsToWatch())
        {
            if (chair == null || subscribed.Contains(chair)) continue;

            chair.ClientPlaced += OnClientPlaced;
            subscribed.Add(chair);
        }

        if (subscribed.Count == 0)
        {
            Debug.LogWarning($"{name}: no OperationChair to watch, so no client will be given cuts.", this);
        }
        if (cutTemplates.Count == 0)
        {
            Debug.LogWarning($"{name}: no cut templates assigned, so placed clients will have nothing to cut.", this);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < subscribed.Count; i++)
        {
            if (subscribed[i] != null) subscribed[i].ClientPlaced -= OnClientPlaced;
        }
        subscribed.Clear();
    }

    /// <summary>The chairs named in the inspector, or every chair in the scene when none are.</summary>
    /// <remarks>Include: a chair on an inactive object is still going to spawn clients once something
    /// switches it on, and finding it only when it happens to be active makes the wiring depend on the
    /// order objects wake up in.</remarks>
    private IEnumerable<OperationChair> ChairsToWatch()
    {
        if (chairs.Count > 0) return chairs;
        return FindObjectsByType<OperationChair>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    /// <summary>Copies the matching template's cuts onto every cuttable part of a freshly placed client.</summary>
    private void OnClientPlaced(OperationChair chair, GameObject client)
    {
        if (client == null) return;
        
        CuttableObject[] bodies = client.GetComponentsInChildren<CuttableObject>(true);
        if (bodies.Length == 0)
        {
            Debug.LogError($"{name}: {client.name} was placed in {chair.name} with no CuttableObject, so it has nothing to cut.", client);
            return;
        }

        for (int i = 0; i < bodies.Length; i++)
        {
            GameObject template = TemplateFor(bodies[i]);
            if (template == null) continue;

            CutCopier.CopyCutsFrom(template, bodies[i], replaceExistingCuts);
        }
    }

    /// <summary>The template to copy cuts from: the first assigned one. No mesh matching -- every body takes the same template's cuts.</summary>
    /// <returns><c>null</c> only when there are no templates at all.</returns>
    private GameObject TemplateFor(CuttableObject body)
    {
        for (int i = 0; i < cutTemplates.Count; i++)
        {
            if (cutTemplates[i] != null) return cutTemplates[i];
        }

        Debug.LogError($"{name}: {body.name} was placed with no cut templates assigned; it will have nothing to cut.", this);
        return null;
    }
}
