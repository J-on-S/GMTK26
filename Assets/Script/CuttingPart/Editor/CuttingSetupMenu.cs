using UnityEditor;
using UnityEngine;

/// <summary>Menu items that build a cutting minigame's GameObjects already wired, so authoring a cut is a click instead of a dozen drags.</summary>
/// <remarks>
/// Deliberately builds the hierarchy in code rather than instantiating a prefab: the shared
/// hardware it has to find (camera, scalpel, speed driver) differs per scene, and a prefab would
/// carry stale references to whichever scene it was authored in.
/// </remarks>
public static class CuttingSetupMenu
{
    /// <summary>Material asset the guide lines use, created on first use.</summary>
    private const string GuideMaterialPath = "Assets/Script/CuttingPart/CutGuideLine.mat";

    /// <summary>Material asset the scalpel trace line uses, created on first use. One shared asset for every cut, since the trace always looks the same.</summary>
    private const string ScalpelTraceMaterialPath = "Assets/Script/CuttingPart/ScalpelTraceLine.mat";

    [MenuItem("GameObject/Cutting/New Cut Minigame", false, 10)]
    private static void CreateCutMinigame(MenuCommand command)
    {
        // MenuCommand.context is the object the item was right-clicked on; when the item comes
        // from the top menu bar it is null and we fall back to the selection.
        GameObject picked = command.context as GameObject;
        if (picked == null) picked = Selection.activeGameObject;

        CuttableObject target = picked != null ? picked.GetComponentInParent<CuttableObject>() : null;
        if (target == null)
        {
            target = Object.FindFirstObjectByType<CuttableObject>(FindObjectsInactive.Include);
        }

        GameObject root = new GameObject(target != null ? $"Cut ({target.name})" : "Cut");
        CuttingManager manager = root.AddComponent<CuttingManager>();
        LoopGuideBuilder guide = root.AddComponent<LoopGuideBuilder>();

        LineRenderer guideLine = CreateLine("GuideLine", root.transform);
        LineRenderer flatLine = CreateLine("FlatLine", root.transform);

        // per-cut wiring, all of it: the manager only has to be told what it is cutting.
        guide.meshFollow = target != null ? target : null;
        guide.loopLine = guideLine;
        guide.flatLine = flatLine;
        guide.showCurvedLoop = true;
        guide.showFlatLoop = false;

        manager.loopGuide = guide;
        manager.GameObjectBeingCut = target;

        // the orbit the cut drives. Before AutoWire, so the PushParameters it runs has somewhere
        // to write this cut's loop guide and startAngle.
        // not assigned to the manager -- it reads the camera's own component. Called for the side
        // effect: the component has to exist for the cut to have an orbit to drive.
        EnsureCameraFollow(manager);

        // the aim highlight now rides on each CuttableObject (IHoverable), so there is no scene-wide
        // free-look component to create or wire here anymore.

        // this cut's own scalpel and surface driver, built as a child so it travels with the cut.
        EnsureScalpelDriver(manager);

        if (target != null)
        {
            // sit the cut under its target, so a CutPlane added later resolves its own body and
            // the whole cut travels with the part.
            root.transform.SetParent(target.transform, false);
        }

        // after the reparent above, so the plane can be dropped at the body's centre in WORLD space
        // and stay there -- SetParent(.., false) keeps the local pose and would drag it off.
        EnsureCutPlane(guide, target);

        // every cut ends on the close-up chop, so a finisher is not optional. Created enabled and
        // wired; its shot and tool are still the author's to set (a scene-specific choice).
        EnsureFinisher(manager);

        // cut defaults: this body's own seeds first, then the project's shared presets, then empty.
        // Only body-universal things (sounds, freshness) -- the camera move is per part, so it is never
        // seeded and stays the author's to tune on each cut.
        WireCutDefaults(manager, root, target);

        // fills anything the steps above could not resolve, and pushes the tuning down.
        manager.AutoWire();

        Undo.RegisterCreatedObjectUndo(root, "Create Cut Minigame");
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        var missing = manager.MissingWiring();
        if (missing.Count > 0)
        {
            Debug.LogWarning($"{root.name}: created, still missing {string.Join(", ", missing)}. Add a CutPlane under it, place it where the cut should go, then press Auto-wire on the manager.", root);
        }
    }

    /// <summary>Seeds a new cut's body-universal defaults: cutting sounds, severed-piece sounds and freshness. The body's own defaults win; failing that, the project's shared presets; failing that, the slot is left empty and the component keeps its inline fallback.</summary>
    /// <remarks>
    /// Body first is the point of this: a body can carry its own feel (a robot cuts unlike flesh) and a
    /// cut authored on it should start with that, not the project average. Only things that are the same
    /// wherever you cut this body are seeded -- the camera move and framing differ per part, so they are
    /// never touched here and stay per-cut.
    /// <para>An already-assigned slot is never overwritten -- a hand-picked preset survives re-running
    /// the tool. Grabbable sounds go onto every <see cref="GrabbableObject"/> in the new cut (a scalpel
    /// authored as one, say), since that is the component that plays them.</para>
    /// </remarks>
    private static void WireCutDefaults(CuttingManager manager, GameObject root, CuttableObject body)
    {
        // cutting sounds: this body's default, else the project's shared one.
        if (manager.soundPreset == null)
        {
            CutSoundPreset cutSounds = body != null && body.defaultSoundPreset != null
                ? body.defaultSoundPreset
                : FindFirstAsset<CutSoundPreset>();
            if (cutSounds != null)
            {
                manager.soundPreset = cutSounds;
                Debug.Log($"Wired cut sounds '{cutSounds.name}' into {manager.name}.", cutSounds);
            }
        }

        // severed-piece sounds and freshness live on the SeveredPiece sibling now.
        SeveredPiece piece = manager.SeveredPieceOutfitter;
        if (piece != null)
        {
            if (piece.audioPreset == null)
            {
                AudioGrappablePreset severedAudio = body != null && body.defaultSeveredPieceAudio != null
                    ? body.defaultSeveredPieceAudio
                    : FindFirstAsset<AudioGrappablePreset>();
                if (severedAudio != null)
                {
                    piece.audioPreset = severedAudio;
                    Debug.Log($"Wired severed-piece sounds '{severedAudio.name}' into {manager.name}.", severedAudio);
                }
            }

            // 0 on the body means "no per-body freshness", so the piece keeps its own default.
            if (body != null && body.defaultSeveredPieceHealth > 0f)
            {
                piece.health = body.defaultSeveredPieceHealth;
            }
        }

        // scalpel/held grabbable sounds: body default first, then project.
        AudioGrappablePreset grabSounds = null;
        bool grabResolved = false;
        foreach (GrabbableObject grabbable in root.GetComponentsInChildren<GrabbableObject>(true))
        {
            if (grabbable.audioPreset != null) continue;

            // resolved once, only when there is actually a grabbable to give it to.
            if (!grabResolved)
            {
                grabSounds = body != null && body.defaultSeveredPieceAudio != null
                    ? body.defaultSeveredPieceAudio
                    : FindFirstAsset<AudioGrappablePreset>();
                grabResolved = true;
            }
            if (grabSounds == null) break;

            grabbable.audioPreset = grabSounds;
            Debug.Log($"Wired grabbable sounds '{grabSounds.name}' into {grabbable.name}.", grabbable);
        }
    }

    /// <summary>The first project asset of type <typeparamref name="T"/>, or null when the project has none.</summary>
    /// <remarks>
    /// One shared preset per kind is the norm here, so "first" is normally "the only one". When a project
    /// carries several, the pick is arbitrary and reported, so a surprise is a log line to follow rather
    /// than a silent wrong wire.
    /// </remarks>
    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (guids.Length > 1 && asset != null)
        {
            Debug.Log($"{guids.Length} {typeof(T).Name} assets in the project; used '{asset.name}'. Assign another by hand if this is the wrong one.", asset);
        }
        return asset;
    }

    /// <summary>This cut's finisher, added to the cut object and wired to the manager either way.</summary>
    /// <remarks>
    /// A finisher is mandatory -- every cut ends on the close-up chop -- so the tool creates one rather
    /// than leaving it for the author to remember. Put on the cut object itself (not a child), where the
    /// finisher's own <c>Manager</c> lookup up the hierarchy resolves it, and wired both ways so neither
    /// side depends on that lookup.
    /// <para>The shot and the tool are left empty on purpose: where the camera watches from and what
    /// swings are scene-specific choices this tool cannot make. The validator flags both until they are
    /// set, which is the intended next step.</para>
    /// <para>An already-assigned finisher is left alone, so re-running the tool on a hand-wired cut does
    /// not stamp a second one.</para>
    /// </remarks>
    private static CutFinisher EnsureFinisher(CuttingManager manager)
    {
        if (manager.finisher == null)
        {
            manager.finisher = manager.GetComponentInChildren<CutFinisher>(true);
        }

        if (manager.finisher == null)
        {
            manager.finisher = Undo.AddComponent<CutFinisher>(manager.gameObject);
        }

        manager.finisher.enableFinisher = true;
        manager.finisher.manager = manager;

        return manager.finisher;
    }

    /// <summary>This cut's plane, built as a primitive cube under the cut and wired into the guide.</summary>
    /// <remarks>
    /// A <c>PrimitiveType.Cube</c>, so it arrives with the mesh, renderer and BoxCollider a plane wants:
    /// the box IS the cut window (<see cref="CutPlane.WindowBox"/>), and the cube mesh makes the plane
    /// something you can see and drag rather than an invisible transform. <see cref="CutPlane"/>'s own
    /// <c>Reset</c>/<c>OnValidate</c> then park the collider out of physics.
    /// <para>Placed at the body's centre -- a starting point to move, not an answer. Where the cut goes
    /// is still the author's call; a cube sitting on the part is only easier to grab than nothing.</para>
    /// <para>Left alone when the guide already has a plane, so re-running the tool on a hand-placed cut
    /// does not stamp a second cube over it.</para>
    /// </remarks>
    private static CutPlane EnsureCutPlane(LoopGuideBuilder guide, CuttableObject target)
    {
        if (guide.plane != null)
        {
            return guide.plane;
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "plane";
        go.transform.SetParent(guide.transform, false);

        // a body-part-sized start: a 1m cube would dwarf a limb and read as nothing but grey. Thin on Y
        // because a cut is a plane -- the window is the X/Z box, and Y is dropped by CutPlane anyway.
        go.transform.localScale = new Vector3(0.05f, 0.001f, 0.05f);

        // at the body's centre in world space, so the cube lands ON the part rather than at the cut
        // object's own origin. Renderer bounds, not the transform: a body's pivot is often at its feet.
        if (target != null && target.TryGetComponent(out Renderer bodyRenderer))
        {
            go.transform.position = bodyRenderer.bounds.center;
        }

        CutPlane plane = go.AddComponent<CutPlane>();
        plane.target = target;
        guide.plane = plane;

        // the primitive's box arrives ENABLED, and CutPlane's Reset/OnValidate that would park it do
        // not fire after a scripted AddComponent. Left on it swallows the interaction raycast aimed at
        // the body and the cut can never be entered -- so disable it here, as the window is an
        // authoring handle whose size/centre still read while disabled.
        if (go.TryGetComponent(out BoxCollider box))
        {
            box.enabled = false;
        }

        Undo.RegisterCreatedObjectUndo(go, "Create Cut Minigame");

        Debug.Log($"A CutPlane cube was created for {guide.name}. Place it where the cut should go.", go);
        return plane;
    }

    /// <summary>The camera's orbit, added to the camera if it hasn't got one yet.</summary>
    /// <remarks>
    /// Resolved through the manager's own <c>SceneCamera</c> so the tool and the runtime can't
    /// disagree about which camera the cut takes over. Returns null only when the scene has no
    /// camera at all, which the caller reports.
    /// </remarks>
    private static CameraFollow EnsureCameraFollow(CuttingManager manager)
    {
        Camera cam = manager.SceneCamera;
        if (cam == null)
        {
            Debug.LogWarning("No camera in the scene, so the cut has no orbit to drive. Add one, then press Auto-wire on the manager.", manager);
            return null;
        }

        if (cam.TryGetComponent(out CameraFollow existing))
        {
            return existing;
        }

        CameraFollow follow = Undo.AddComponent<CameraFollow>(cam.gameObject);

        // parked: the minigame enables it on entry, and CameraFollow.OnEnable is what seeds the
        // orbit angle from startAngle. Left running, it would orbit the camera off its free-look
        // pose the moment play starts, before any cut has been entered.
        follow.enabled = false;

        Debug.Log($"Added a CameraFollow to {cam.name} for this cut.", cam);
        return follow;
    }

    /// <summary>The scalpel this cut drives and its surface driver, created under the cut when it has none, and wired into the manager either way.</summary>
    /// <remarks>
    /// One scalpel per CUT: the driver snaps the transform it sits on onto the
    /// body and draws that cut's trail into its own LineRenderer, so a shared one could only ever hold
    /// one cut's trace and would have to be re-pointed at whichever cut ran last. Cuts on different
    /// bodies then cannot keep their own lines at all.
    /// <para>Built as a child of the cut, so it travels with it -- including through
    /// <see cref="CutCopier"/>, which copies the cut's hierarchy wholesale.</para>
    /// <para>Only the running cut's driver is live: it parks itself when play starts, and
    /// <c>CuttingManager.SetScalpelTrace</c> switches it on at entry.</para>
    /// <para>An already-assigned <c>scalpelFollow</c> is left alone -- a scene can carry a hand-placed
    /// scalpel, and that one keeps its transform and only gains the missing component.</para>
    /// <para>The mesh is left to the author: what the scalpel looks like is not something this tool can
    /// guess, and an empty object still drives the cut.</para>
    /// </remarks>
    private static ScalpelSurfaceDriver EnsureScalpelDriver(CuttingManager manager)
    {
        CameraFollow follow = manager.scalpelFollow;

        if (follow == null)
        {
            // deliberately NOT adopting another cut's scalpel: that is the shared-scalpel arrangement
            // this per-cut layout exists to replace.
            GameObject go = new GameObject("Scalpel");
            go.transform.SetParent(manager.transform, false);

            // RequireComponent on the driver brings the CameraFollow with it.
            ScalpelSurfaceDriver created = go.AddComponent<ScalpelSurfaceDriver>();
            manager.scalpelFollow = go.GetComponent<CameraFollow>();

            AssignTraceMaterial(created);

            Undo.RegisterCreatedObjectUndo(go, "Create Cut Minigame");

            Debug.Log($"A Scalpel was created for {manager.name}. Give it a mesh, then place it.", go);
            return created;
        }

        if (follow.TryGetComponent(out ScalpelSurfaceDriver driver))
        {
            AssignTraceMaterial(driver);
            return driver;
        }

        driver = Undo.AddComponent<ScalpelSurfaceDriver>(follow.gameObject);
        AssignTraceMaterial(driver);
        Debug.Log($"Added a ScalpelSurfaceDriver to {follow.name} for this cut.", follow);
        return driver;
    }

    /// <summary>Builds one closed-loop LineRenderer for a guide, in the line's own space, sharing the guide material.</summary>
    /// <remarks>Local, not world: a LineRenderer's points are serialized, and world-space points are a
    /// property of where the body stands -- so the same ring writes different numbers in the prefab stage
    /// than on an instance in a scene, and every alternation rewrites the lot. <see cref="LoopGuideBuilder"/>
    /// takes the loop into this space before it writes, and flips any line still left on world space.</remarks>
    private static LineRenderer CreateLine(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 0;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = GuideMaterial();
        return lr;
    }

    /// <summary>Gives the scalpel trace the shared material, when it hasn't got one already. The trace is identical across cuts, so nobody should have to pick it.</summary>
    private static void AssignTraceMaterial(ScalpelSurfaceDriver driver)
    {
        if (driver == null || driver.traceMaterial != null) return;
        driver.traceMaterial = ScalpelTraceMaterial();
        Debug.Log($"Wired the shared scalpel trace material into {driver.name}.", driver);
    }

    /// <summary>The shared material for the scalpel trace line, created as an asset on first use so it survives a scene reload. Distinct colour from the guide so the drawn cut reads apart from the target loop.</summary>
    private static Material ScalpelTraceMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(ScalpelTraceMaterialPath);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader) { name = "ScalpelTraceLine" };
        mat.color = Color.red;
        AssetDatabase.CreateAsset(mat, ScalpelTraceMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// <summary>The shared unlit material for guide lines, created as an asset on first use so it survives a scene reload.</summary>
    private static Material GuideMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(GuideMaterialPath);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader) { name = "CutGuideLine" };
        mat.color = Color.cyan;
        AssetDatabase.CreateAsset(mat, GuideMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

}
