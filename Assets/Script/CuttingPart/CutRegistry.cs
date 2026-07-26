using System.Collections.Generic;
using UnityEngine;

/// <summary>Which cuts belong to which body, cached so aiming at a body does not cost a scene search every frame.</summary>
/// <remarks>
/// A <see cref="CuttingManager"/> is not on the object it cuts -- it sits on a child, so one body
/// can carry several cuts -- which means a raycast hit gives you the <see cref="CuttableObject"/>
/// and nothing else. This maps back the other way.
/// <para>
/// Built lazily by one scene sweep and then held. Managers invalidate it as they enable and
/// disable, so the sweep runs again only when the set has actually changed, not per frame.
/// </para>
/// </remarks>
public static class CutRegistry
{
    /// <summary>Cuts by the body they remove a piece of. Null until the first lookup after an invalidation.</summary>
    private static Dictionary<CuttableObject, List<CuttingManager>> byBody;

    /// <summary>Empty list handed back for a body with no cuts, so callers never have to null-check.</summary>
    private static readonly List<CuttingManager> None = new();

    /// <summary>Drops the cache. Call whenever the set of cuts in the scene changes.</summary>
    public static void Invalidate()
    {
        byBody = null;
    }

    /// <summary>Every enabled cut that removes a piece of this body. Empty when there are none.</summary>
    public static List<CuttingManager> CutsOf(CuttableObject body)
    {
        if (body == null)
        {
            return None;
        }

        Rebuild();
        return byBody.TryGetValue(body, out List<CuttingManager> cuts) ? cuts : None;
    }

    /// <summary>Switches every cut of one body on or off in a single call, e.g. to close a body to cutting while a minigame owns the camera.</summary>
    /// <param name="body">Body whose cuts to switch. Cuts of every other body are left alone.</param>
    /// <param name="enabled">What to set each cut's <c>enabled</c> to.</param>
    /// <returns>How many cuts actually changed state.</returns>
    /// <remarks>
    /// Does its own scene sweep rather than going through <see cref="CutsOf"/>: that one answers
    /// with the ENABLED cuts, by construction -- so re-enabling through it would only ever find the
    /// cuts already on.
    /// <para>
    /// Toggles the component, not its GameObject. A cut whose GameObject is inactive stays inactive
    /// and does not run, whatever this sets; and the <see cref="LoopGuideBuilder"/> beside it keeps
    /// drawing its guide line, since that is a separate component with its own switch.
    /// </para>
    /// </remarks>
    public static int SetCutsEnabled(CuttableObject body, bool enabled)
    {
        if (body == null)
        {
            return 0;
        }

        // Include: a disabled cut is exactly what an enable call is looking for, and the default
        // sweep would skip it.
        CuttingManager[] managers = Object.FindObjectsByType<CuttingManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int changed = 0;
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i].GameObjectBeingCut != body || managers[i].enabled == enabled)
            {
                continue;
            }

            managers[i].enabled = enabled;
            changed++;
        }

        // The managers' own OnEnable/OnDisable already invalidate, but only for those on an active
        // GameObject -- no callback fires for the rest. One explicit drop covers both.
        if (changed > 0)
        {
            Invalidate();
        }

        return changed;
    }

    /// <summary>Sweeps the scene once, unless a usable cache is already in hand.</summary>
    private static void Rebuild()
    {
        if (byBody != null)
        {
            return;
        }

        byBody = new Dictionary<CuttableObject, List<CuttingManager>>();

        CuttingManager[] managers = Object.FindObjectsByType<CuttingManager>(FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            CuttableObject body = managers[i].GameObjectBeingCut;
            if (body == null)
            {
                continue;
            }

            if (!byBody.TryGetValue(body, out List<CuttingManager> cuts))
            {
                cuts = new List<CuttingManager>();
                byBody[body] = cuts;
            }
            cuts.Add(managers[i]);
        }
    }

    /// <summary>The cut whose removed piece a world point falls in, innermost first.</summary>
    /// <remarks>
    /// Regions nest: the hand is inside the piece a wrist cut takes and also inside the piece a
    /// shoulder cut takes. The winner is the most deeply nested candidate -- the one contained by
    /// the most other candidates -- so aiming at the hand picks the wrist, and aiming at the
    /// forearm, which only the shoulder region holds, picks the shoulder.
    /// </remarks>
    /// <returns><c>null</c> when the point is in no cut's region, i.e. it is on the upper hull.</returns>
    /// <summary>Scratch list of the cuts whose region holds the point, reused so aiming allocates nothing.</summary>
    private static readonly List<CuttingManager> Containing = new();

    public static CuttingManager CutAt(CuttableObject body, Vector3 worldPoint)
    {
        List<CuttingManager> cuts = CutsOf(body);

        // Collected in one pass first. Every containment test can cost a slice of the whole body,
        // so the nesting comparison below must never run for a point only one cut claims -- which
        // is the overwhelmingly common case.
        Containing.Clear();
        for (int i = 0; i < cuts.Count; i++)
        {
            if (cuts[i] != null && cuts[i].RegionContains(worldPoint))
            {
                Containing.Add(cuts[i]);
            }
        }

        if (Containing.Count <= 1)
        {
            return Containing.Count == 1 ? Containing[0] : null;
        }

        CuttingManager best = null;
        int bestDepth = -1;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < Containing.Count; i++)
        {
            CuttingManager candidate = Containing[i];

            // how many other containing regions this one sits inside; more = deeper in
            int depth = 0;
            for (int j = 0; j < Containing.Count; j++)
            {
                if (Containing[j].RegionContainsCutOf(candidate))
                {
                    depth++;
                }
            }

            // ties (two cuts neither of which contains the other) go to the nearer plane, which
            // is the smaller piece of the two.
            float distance = Mathf.Abs(candidate.SignedDistanceToPlane(worldPoint));
            if (depth > bestDepth || (depth == bestDepth && distance < bestDistance))
            {
                best = candidate;
                bestDepth = depth;
                bestDistance = distance;
            }
        }

        return best;
    }
}
