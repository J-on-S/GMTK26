using System.Collections.Generic;
using UnityEngine;

/// <summary>Copies whole cut minigames from one body onto another, rebound so they cut the new one.</summary>
/// <remarks>
/// Runtime code, not an editor tool: the menu item is a thin caller, so a spawned client that needs
/// the same cuts as a template body can ask for them at load time without duplicating this logic.
/// <para>
/// What a cut knows about its body is only ever one of two things, and this class exists because the
/// two are handled differently:
/// </para>
/// <list type="bullet">
/// <item><description><b>Placement</b> -- the cut root's local transform, the <see cref="CutPlane"/>'s
/// transform, and the plane's window box. All of it is expressed relative to the body, so it copies
/// verbatim and lands in the same place on a body with the same shape. Nothing here recomputes it,
/// except for a plane kept outside the cut's own hierarchy: that one is cloned and re-placed through
/// the body's space, since its local values are relative to something the copy does not hang under.</description></item>
/// <item><description><b>References</b> -- <see cref="CuttingManager.GameObjectBeingCut"/>,
/// <see cref="CutPlane.target"/>, <see cref="CutFinisher.manager"/> and
/// <see cref="CuttingManager.scalpelFollow"/>. These are the only things that have to change, and a copy
/// that skips them silently keeps cutting the body it came from.</description></item>
/// </list>
/// <para><see cref="CutFinisher"/>'s framed shot needs neither: it is stored in the body's own space
/// (<see cref="CutFinisher.ShotSpace"/>), so rebinding the manager is what moves it.</para>
/// <para>Bodies are assumed to be near-identical in shape. The loop is re-extracted from the target's
/// own triangles, so a body that differs near a cut can produce a different loop, or none -- the caller
/// is expected to check <see cref="CuttingManager.MissingWiring"/> and look at the guide lines.</para>
/// </remarks>
public static class CutCopier
{
    /// <summary>Every cut of this body, including disabled ones and ones on inactive objects.</summary>
    /// <remarks>
    /// Its own sweep rather than <see cref="CutRegistry.CutsOf"/>, which by construction answers with the
    /// enabled cuts only -- copying a body has to bring the switched-off cuts along too.
    /// <para>A body that is not in a loaded scene is inside a prefab asset, where a scene sweep finds
    /// nothing: its cuts are then whatever sits in the same prefab and names it.</para>
    /// </remarks>
    public static List<CuttingManager> CutsOn(CuttableObject body)
    {
        var found = new List<CuttingManager>();
        if (body == null) return found;

        CuttingManager[] managers = body.gameObject.scene.IsValid()
            ? Object.FindObjectsByType<CuttingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : body.transform.root.GetComponentsInChildren<CuttingManager>(true);

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i].GameObjectBeingCut == body)
            {
                found.Add(managers[i]);
            }
        }
        return found;
    }

    /// <summary>Every cut inside one hierarchy, whatever body each one names. Works on a prefab asset as well as a scene object.</summary>
    /// <remarks>
    /// The template form: a prefab holding an authored body and its cuts is copied whole, so a cut in it
    /// counts whether or not its <c>GameObjectBeingCut</c> resolves to anything useful. That matters
    /// because a prefab's cuts can name a body that only exists inside that prefab -- a reference the
    /// copies must not keep, and <see cref="RebindToBody"/> replaces it.
    /// </remarks>
    public static List<CuttingManager> CutsIn(GameObject root)
    {
        var found = new List<CuttingManager>();
        if (root == null) return found;

        found.AddRange(root.GetComponentsInChildren<CuttingManager>(true));
        return found;
    }

    /// <summary>Copies every cut of one body onto another, in place, and hands back the copies.</summary>
    /// <param name="replaceExisting">Destroys the target's own cuts first. Off, the copies are added to whatever it already has.</param>
    /// <returns>The new cuts, in the order they were copied. Empty when the source has none.</returns>
    public static List<CuttingManager> CopyCuts(CuttableObject from, CuttableObject to, bool replaceExisting = false)
    {
        var copies = new List<CuttingManager>();

        if (from == null || to == null)
        {
            Debug.LogWarning("CutCopier: need both a source body and a target body.");
            return copies;
        }
        if (from == to)
        {
            Debug.LogWarning($"CutCopier: {from.name} is both source and target; nothing to do.", from);
            return copies;
        }

        // the source list is taken before anything is destroyed or created, so neither the removal
        // below nor the copies themselves can turn up in it.
        return CopyEach(CutsOn(from), to, replaceExisting);
    }

    /// <summary>Copies every cut inside a prefab -- or any other hierarchy -- onto a body.</summary>
    /// <param name="template">Prefab asset or scene object holding the authored cuts. Left untouched.</param>
    /// <param name="replaceExisting">Destroys the target's own cuts first. Off, the copies are added to whatever it already has.</param>
    /// <remarks>
    /// The prefab is a library of cuts, not a body: every cut in it is taken, and each copy is rebound to
    /// <paramref name="to"/> whatever body it used to name. Placement still travels through the body
    /// spaces, so a cut authored against the prefab's own body lands in the same place on the target.
    /// <para>The copies are plain objects, not prefab instances. A cut is per-body wiring by nature --
    /// keeping the link would only offer to push one body's plane placement back onto the template.</para>
    /// </remarks>
    public static List<CuttingManager> CopyCutsFrom(GameObject template, CuttableObject to, bool replaceExisting = false)
    {
        var copies = new List<CuttingManager>();

        if (template == null || to == null)
        {
            Debug.LogWarning("CutCopier: need both a template to copy from and a body to copy to.");
            return copies;
        }
        if (to.transform.IsChildOf(template.transform))
        {
            Debug.LogWarning($"CutCopier: {to.name} is inside {template.name}; copying it onto itself would nest the cuts forever.", to);
            return copies;
        }

        return CopyEach(CutsIn(template), to, replaceExisting);
    }

    /// <summary>Copies every cut of one body onto several bodies at once.</summary>
    /// <returns>Every copy made, across all targets, in target order.</returns>
    /// <remarks>The source list is read once and reused, so a run over ten bodies costs one scene sweep
    /// rather than ten. A null or repeated target is skipped rather than fatal: this is normally handed a
    /// selection, and one bad entry should not lose the other nine.</remarks>
    public static List<CuttingManager> CopyCuts(CuttableObject from, IEnumerable<CuttableObject> targets, bool replaceExisting = false)
    {
        return CopyToEach(from != null ? CutsOn(from) : null, from, null, targets, replaceExisting);
    }

    /// <summary>Copies every cut inside a prefab -- or any other hierarchy -- onto several bodies at once.</summary>
    /// <returns>Every copy made, across all targets, in target order.</returns>
    public static List<CuttingManager> CopyCutsFrom(GameObject template, IEnumerable<CuttableObject> targets, bool replaceExisting = false)
    {
        return CopyToEach(
            template != null ? CutsIn(template) : null,
            null,
            template != null ? template.transform : null,
            targets,
            replaceExisting);
    }

    /// <summary>Runs one prepared source list over a set of targets, skipping the ones that cannot take it.</summary>
    /// <param name="insideTemplate">Root the sources came from, when they came from a hierarchy. A target within it is skipped: copying a template onto its own body nests the cuts forever.</param>
    private static List<CuttingManager> CopyToEach(
        List<CuttingManager> sources, CuttableObject sourceBody, Transform insideTemplate,
        IEnumerable<CuttableObject> targets, bool replaceExisting)
    {
        var copies = new List<CuttingManager>();

        if (sources == null || targets == null)
        {
            Debug.LogWarning("CutCopier: need something to copy and somewhere to copy it to.");
            return copies;
        }

        // a body handed in twice would otherwise get the cuts twice, and with replaceExisting on the
        // second pass would delete what the first one just made.
        var done = new HashSet<CuttableObject>();

        foreach (CuttableObject target in targets)
        {
            if (target == null || !done.Add(target)) continue;
            if (target == sourceBody)
            {
                Debug.LogWarning($"CutCopier: {target.name} is the source; skipped.", target);
                continue;
            }
            if (insideTemplate != null && target.transform.IsChildOf(insideTemplate))
            {
                Debug.LogWarning($"CutCopier: {target.name} is inside the template it would be copied from; skipped.", target);
                continue;
            }

            copies.AddRange(CopyEach(sources, target, replaceExisting));
        }

        return copies;
    }

    /// <summary>Copies a list of cuts onto a body: the shared tail of every entry point.</summary>
    private static List<CuttingManager> CopyEach(List<CuttingManager> sources, CuttableObject to, bool replaceExisting)
    {
        var copies = new List<CuttingManager>();

        if (replaceExisting)
        {
            RemoveCuts(to);
        }

        for (int i = 0; i < sources.Count; i++)
        {
            CuttingManager copy = CopyCut(sources[i], to);
            if (copy != null) copies.Add(copy);
        }

        WarnIfBodyCannotBeAimedAt(to);

        CutRegistry.Invalidate();
        return copies;
    }

    /// <summary>Reports the things a body needs before any cut on it can be aimed at or sliced.</summary>
    /// <remarks>
    /// A copy can be perfectly wired and still do nothing, because what makes a cut reachable lives on
    /// the BODY, not on the cut, and copying cuts never puts it there:
    /// <list type="bullet">
    /// <item><description>a <c>Collider</c> on the body's own GameObject -- <c>Interactor</c> reads the
    /// <see cref="CuttableObject"/> (its interact and its aim highlight both) off the collider it hits
    /// with <c>TryGetComponent</c>, so one on a child resolves to nothing;</description></item>
    /// <item><description>a <c>MeshFilter</c> with a mesh -- the severed preview is sliced from it, and
    /// with no preview every region test says no, so aiming highlights nothing and clicking enters
    /// nothing.</description></item>
    /// </list>
    /// Warnings rather than a refusal: the cuts themselves are correct, and this is as likely to be a
    /// body that is not finished yet as it is a mistake.
    /// </remarks>
    private static void WarnIfBodyCannotBeAimedAt(CuttableObject body)
    {
        if (body == null) return;

        if (!body.TryGetComponent<Collider>(out _))
        {
            Debug.LogWarning($"{body.name} has no Collider on its own GameObject, so aiming resolves no body and its cuts can never be entered. A collider on a child does not count.", body);
        }

        bool hasMesh = body.TryGetComponent<MeshFilter>(out MeshFilter filter) && filter.sharedMesh != null;
        if (!hasMesh)
        {
            Debug.LogWarning($"{body.name} has no MeshFilter mesh, so no severed piece can be previewed: its cuts will neither highlight nor open.", body);
        }
    }

    /// <summary>Copies one cut onto a body, keeping its placement and rebinding it to cut that body.</summary>
    /// <returns>The copied cut, or <c>null</c> when the arguments were unusable.</returns>
    public static CuttingManager CopyCut(CuttingManager source, CuttableObject to)
    {
        if (source == null || to == null) return null;

        Transform sourceRoot = source.transform;

        // instantiateInWorldSpace false: the cut's placement is local to the body, which is exactly
        // what has to be preserved. The three values are then written explicitly -- the pose is the
        // whole point of the copy, and it should not depend on reading an overload's default right.
        GameObject copy = Object.Instantiate(sourceRoot.gameObject, to.transform, false);
        copy.name = sourceRoot.name;
        copy.transform.SetLocalPositionAndRotation(sourceRoot.localPosition, sourceRoot.localRotation);
        copy.transform.localScale = sourceRoot.localScale;

        var cut = copy.GetComponent<CuttingManager>();
        if (cut == null)
        {
            Debug.LogWarning($"CutCopier: {sourceRoot.name} has no CuttingManager on its root; copied as-is and left unbound.", copy);
            return null;
        }

        // before the rebind: it takes a plane, a finisher and a scalpel that are already in place as
        // this cut's own and leaves them.
        CopyPlanes(cut, source, source.GameObjectBeingCut, to);
        CopyFinisher(cut, source, source.GameObjectBeingCut, to);
        CopyScalpel(cut, source, source.GameObjectBeingCut, to);

        RebindToBody(cut, to);

        // CuttingManager.phase is serialized, so a source left mid-phase -- an editor preview that was
        // interrupted by a recompile, a scene saved during one -- hands the copy a phase it never
        // entered. canEnterMinigame() demands Free, so the copy would refuse to start and give no
        // reason. A fresh copy is by definition at the beginning of its cut.
        cut.phase = CuttingManager.RigPhase.Free;
        cut.currentAngle = cut.StartAngle;

        return cut;
    }

    /// <summary>Gives the copy a plane of its own for every plane the source cut uses, wherever the source kept it.</summary>
    /// <remarks>
    /// A cut authored by the setup menu keeps its <see cref="CutPlane"/> as a child, and
    /// <c>Instantiate</c> duplicates it with the rest of the hierarchy and repoints the copy's guide at
    /// the duplicate -- there is nothing to do. A plane parented anywhere else is not part of the copied
    /// subtree, so the copy's guide comes out still pointing at the ORIGINAL plane: two cuts sharing one
    /// plane, where moving it to fix one body moves the cut on the other. Those are cloned here.
    /// <para>The clone is placed by its pose <b>in the source body's space</b>, not by its local
    /// transform: an external plane's local values are relative to whatever it hung under, which the
    /// copy no longer is. Its scale is carried the same way, since the window box is measured in the
    /// plane's own units and a rescaled plane cuts a different window.</para>
    /// <para>Guides are paired by index. The copy is an <c>Instantiate</c> of the source, so
    /// <c>GetComponentsInChildren</c> walks both hierarchies in the same order; a length mismatch means
    /// something else edited the copy, and pairing then would assign planes to the wrong guides.</para>
    /// </remarks>
    private static void CopyPlanes(CuttingManager copy, CuttingManager source, CuttableObject fromBody, CuttableObject toBody)
    {
        LoopGuideBuilder[] sourceGuides = source.GetComponentsInChildren<LoopGuideBuilder>(true);
        LoopGuideBuilder[] copyGuides = copy.GetComponentsInChildren<LoopGuideBuilder>(true);

        if (sourceGuides.Length != copyGuides.Length)
        {
            Debug.LogWarning($"CutCopier: {copy.name} came out with a different set of loop guides than {source.name}; its planes were left for Auto-wire to resolve.", copy);
            return;
        }

        // one clone per plane, so two guides pointing at the same source plane still point at the same
        // plane afterwards rather than at two copies drifting apart.
        Dictionary<CutPlane, CutPlane> clones = null;

        for (int i = 0; i < sourceGuides.Length; i++)
        {
            CutPlane sourcePlane = sourceGuides[i].plane;
            if (sourcePlane == null) continue;

            // already duplicated with the hierarchy, and the copy's guide already points at it
            if (IsUnder(sourcePlane.transform, source.transform)) continue;

            clones ??= new Dictionary<CutPlane, CutPlane>();
            if (!clones.TryGetValue(sourcePlane, out CutPlane clone))
            {
                clone = ClonePlaneOntoBody(sourcePlane, copy.transform, fromBody, toBody);
                clones[sourcePlane] = clone;
            }

            copyGuides[i].plane = clone;
        }
    }

    /// <summary>Gives the copy a finisher of its own when the source's is kept outside the cut's hierarchy.</summary>
    /// <remarks>
    /// The finisher holds the close-up camera pose, and it holds it <b>in the body's space</b>
    /// (<see cref="CutFinisher.ShotSpace"/> is the manager's <c>GameObjectBeingCut</c>). That is what makes
    /// a framed shot follow a copy onto a new body -- but only if the copy has its own finisher pointing
    /// at its own manager. Sharing the source's means <c>ShotSpace</c> resolves through the SOURCE manager
    /// to the OLD body, and the close-up frames the wrong client while the cut itself runs on the right one.
    /// <para>Nothing is converted: <c>shotLocalPosition</c> and <c>shotLocalEuler</c> are copied verbatim and
    /// come out right because they are read in whichever body the clone's manager names.</para>
    /// </remarks>
    private static void CopyFinisher(CuttingManager copy, CuttingManager source, CuttableObject fromBody, CuttableObject toBody)
    {
        CutFinisher sourceFinisher = source.finisher;
        if (sourceFinisher == null) return;

        // already duplicated with the hierarchy, and RebindToBody points the copy's manager at it
        if (IsUnder(sourceFinisher.transform, source.transform)) return;

        GameObject clone = Object.Instantiate(sourceFinisher.gameObject, copy.transform, false);
        clone.name = sourceFinisher.name;

        // its own transform only matters as the fallback shot space and for gizmos, but placing it the
        // same way as a plane keeps the scene readable rather than stacking clones at the cut's origin.
        PlaceThroughBody(clone.transform, sourceFinisher.transform, copy.transform, fromBody, toBody);

        var finisher = clone.GetComponent<CutFinisher>();
        if (finisher == null) return;

        finisher.manager = copy;
        copy.finisher = finisher;
    }

    /// <summary>Gives the copy a scalpel of its own when the source's is kept outside the cut's hierarchy.</summary>
    /// <remarks>
    /// The scalpel is per-cut: its <see cref="ScalpelSurfaceDriver"/> snaps the transform it sits on onto
    /// the body and appends that cut's trail to its own <c>LineRenderer</c>. Shared, there is one trace
    /// line for every cut in the scene, and whichever cut pushed last owns it -- so a copy on a second
    /// body would draw its trail over the first body's scalpel.
    /// <para>Only the running cut's driver is live: each parks itself when play starts and
    /// <c>CuttingManager.SetScalpelTrace</c> switches the entered one on, so several scalpels in a scene
    /// cost nothing while nobody is cutting.</para>
    /// <para>This is also the migration path for a scene built when the scalpel was scene-wide: the
    /// original cut keeps the shared one, and every copy gets a scalpel of its own.</para>
    /// </remarks>
    private static void CopyScalpel(CuttingManager copy, CuttingManager source, CuttableObject fromBody, CuttableObject toBody)
    {
        CameraFollow sourceScalpel = source.scalpelFollow;
        if (sourceScalpel == null) return;

        // already duplicated with the hierarchy: Instantiate repointed the copy at its own
        if (IsUnder(sourceScalpel.transform, source.transform)) return;

        GameObject clone = Object.Instantiate(sourceScalpel.gameObject, copy.transform, false);
        clone.name = sourceScalpel.name;

        // where it stands is the cut's business from the first frame -- the orbit and the surface snap
        // both write it -- so this only decides where it sits in the scene view before a run.
        PlaceThroughBody(clone.transform, sourceScalpel.transform, copy.transform, fromBody, toBody);

        var follow = clone.GetComponent<CameraFollow>();
        if (follow == null) return;

        copy.scalpelFollow = follow;

        // a cloned trail is the source cut's line, drawn on the source body
        if (clone.TryGetComponent(out ScalpelSurfaceDriver driver))
        {
            driver.ResetTrace();
        }
    }

    /// <summary>Clones one plane under a cut, standing where it stood on the body it was placed against.</summary>
    private static CutPlane ClonePlaneOntoBody(CutPlane source, Transform parent, CuttableObject fromBody, CuttableObject toBody)
    {
        GameObject copy = Object.Instantiate(source.gameObject, parent, false);
        copy.name = source.name;

        PlaceThroughBody(copy.transform, source.transform, parent, fromBody, toBody);
        return copy.GetComponent<CutPlane>();
    }

    /// <summary>Puts a clone where its source stood, measured in the body each one belongs to.</summary>
    /// <remarks>
    /// For anything the source kept outside the cut, local values are relative to whatever it hung under
    /// -- which the clone does not hang under -- so they cannot be copied across. Read in the source
    /// body's space and written in the target's, the clone lands in the same place on the new body,
    /// including when the two bodies sit at different places or sizes in the scene.
    /// <para>Scale travels the same way, because a window box is measured in its plane's own units: a
    /// plane at half the scale cuts half the window.</para>
    /// </remarks>
    private static void PlaceThroughBody(Transform clone, Transform source, Transform parent, CuttableObject fromBody, CuttableObject toBody)
    {
        Transform fromSpace = fromBody != null ? fromBody.transform : source.parent;
        Transform toSpace = toBody != null ? toBody.transform : parent;

        if (fromSpace == null || toSpace == null)
        {
            // nothing to express the pose in; the world pose is the best answer left
            clone.SetPositionAndRotation(source.position, source.rotation);
            clone.localScale = source.localScale;
            return;
        }

        clone.SetPositionAndRotation(
            toSpace.TransformPoint(fromSpace.InverseTransformPoint(source.position)),
            toSpace.rotation * (Quaternion.Inverse(fromSpace.rotation) * source.rotation));

        // the same size relative to the new body as it had on the old one, then expressed against
        // whatever the clone now hangs under.
        Vector3 relativeToBody = Divide(source.lossyScale, fromSpace.lossyScale);
        Vector3 wanted = Scale(relativeToBody, toSpace.lossyScale);
        clone.localScale = parent != null ? Divide(wanted, parent.lossyScale) : wanted;
    }

    private static Vector3 Scale(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    // a zero-scaled space has no size to divide out; keeping the value beats an infinity
    private static Vector3 Divide(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            Mathf.Approximately(divisor.x, 0f) ? value.x : value.x / divisor.x,
            Mathf.Approximately(divisor.y, 0f) ? value.y : value.y / divisor.y,
            Mathf.Approximately(divisor.z, 0f) ? value.z : value.z / divisor.z);
    }

    /// <summary>Points a cut and everything under it at a body, leaving its placement alone.</summary>
    /// <remarks>
    /// Public on its own because it is also the fix for a cut moved by hand: parent it under the new
    /// body, call this, done.
    /// <para>Every reference is repaired the same way -- kept when it already points inside this cut,
    /// re-resolved when it points outside it. <c>Instantiate</c> remaps references within the copied
    /// hierarchy for us, so the outside case only arises for a cut whose guide or finisher was authored
    /// somewhere else entirely; it is cheap to handle and expensive to debug.</para>
    /// </remarks>
    public static void RebindToBody(CuttingManager cut, CuttableObject body)
    {
        if (cut == null || body == null) return;

        cut.GameObjectBeingCut = body;

        // Replaced only when this cut has one of its own to replace it with. A reference pointing
        // outside the cut is wrong on a fresh copy -- it is the original's component -- but it is a
        // legitimate hand-wiring on a cut that was only moved, and overwriting it with null would
        // break a working cut to fix a hypothetical one.
        if (cut.loopGuide == null || !IsUnder(cut.loopGuide.transform, cut.transform))
        {
            LoopGuideBuilder own = cut.GetComponentInChildren<LoopGuideBuilder>(true);
            if (own != null) cut.loopGuide = own;
        }

        if (cut.loopGuide != null && (cut.loopGuide.plane == null || !IsUnder(cut.loopGuide.plane.transform, cut.transform)))
        {
            CutPlane own = cut.GetComponentInChildren<CutPlane>(true);
            if (own != null) cut.loopGuide.plane = own;
        }

        // the explicit target wins over CutPlane's walk up the hierarchy, so a copied plane with one
        // set would keep windowing the body it came from while sitting on this one.
        CutPlane[] planes = cut.GetComponentsInChildren<CutPlane>(true);
        for (int i = 0; i < planes.Length; i++)
        {
            if (planes[i].target != null) planes[i].target = body;
        }

        // the finisher's framed shot is stored in the body's space and needs no conversion -- but it
        // reads that space through the manager, so this assignment is what puts the shot on this body.
        CutFinisher[] finishers = cut.GetComponentsInChildren<CutFinisher>(true);
        for (int i = 0; i < finishers.Length; i++)
        {
            if (finishers[i].manager != null && !IsUnder(finishers[i].manager.transform, cut.transform))
            {
                finishers[i].manager = cut;
            }
        }

        if ((cut.finisher == null || !IsUnder(cut.finisher.transform, cut.transform)) && finishers.Length > 0)
        {
            cut.finisher = finishers[0];
        }

        // one scalpel per cut: it is the transform the driver snaps onto the body and the line that
        // cut's trail is drawn into, so pointing at another cut's leaves two cuts sharing one blade
        // and one trace.
        if (cut.scalpelFollow == null || !IsUnder(cut.scalpelFollow.transform, cut.transform))
        {
            ScalpelSurfaceDriver own = cut.GetComponentInChildren<ScalpelSurfaceDriver>(true);
            if (own != null && own.TryGetComponent(out CameraFollow ownFollow))
            {
                cut.scalpelFollow = ownFollow;
            }
        }

        // fills the scene-wide slots this copy may be missing and pushes the tuning into the guide,
        // which is also what rewrites LoopGuideBuilder.meshFollow from GameObjectBeingCut.
        cut.AutoWire();

        CutRegistry.Invalidate();
    }

    /// <summary>Destroys every cut of this body. Returns how many went.</summary>
    public static int RemoveCuts(CuttableObject body)
    {
        List<CuttingManager> cuts = CutsOn(body);
        for (int i = 0; i < cuts.Count; i++)
        {
            if (cuts[i] == null) continue;
            Destroy(cuts[i].gameObject);
        }

        if (cuts.Count > 0) CutRegistry.Invalidate();
        return cuts.Count;
    }

    /// <summary>True when <paramref name="candidate"/> is <paramref name="root"/> or sits under it.</summary>
    private static bool IsUnder(Transform candidate, Transform root)
    {
        if (candidate == null || root == null) return false;
        return candidate == root || candidate.IsChildOf(root);
    }

    /// <summary>Destroys an object from code that runs in both modes.</summary>
    /// <remarks><c>Destroy</c> is deferred to the end of the frame and never runs at all in edit mode,
    /// where nothing is stepping the player loop; the caller here needs the object gone before the
    /// copies are counted.</remarks>
    private static void Destroy(GameObject go)
    {
        if (Application.isPlaying)
        {
            Object.Destroy(go);
        }
        else
        {
            Object.DestroyImmediate(go);
        }
    }
}
