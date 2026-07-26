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

        // No plane is created. Where a cut goes is the one decision this tool cannot make for you,
        // and a generated one at the body's centre would just be a wrong answer to move. Add a
        // CutPlane under this object and press Auto-wire, or drop it on the guide directly.

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

        // free-look, which the cut switches off on entry and back on when it exits. Unlike the
        // orbit this one is assigned, since the manager holds it in a slot.
        EnsureMoveCamera(manager);

        // this cut's own scalpel and surface driver, built as a child so it travels with the cut.
        EnsureScalpelDriver(manager);

        if (target != null)
        {
            // sit the cut under its target, so a CutPlane added later resolves its own body and
            // the whole cut travels with the part.
            root.transform.SetParent(target.transform, false);
        }

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

    /// <summary>The scene's free-look, created on its own GameObject when there isn't one, and wired into the manager either way.</summary>
    /// <remarks>
    /// One per scene rather than one per cut: <see cref="MoveCamera"/> resolves its own camera and
    /// every manager only ever toggles it, so a second instance would fight the first over the same
    /// aim highlight. Created here rather than in <c>AutoWire</c> because this is the one entry point
    /// that is a deliberate authoring action -- <c>AutoWire</c> also runs from <c>Reset</c>, where
    /// spawning objects would be a surprise.
    /// </remarks>
    private static MoveCamera EnsureMoveCamera(CuttingManager manager)
    {
        MoveCamera existing = Object.FindFirstObjectByType<MoveCamera>(FindObjectsInactive.Include);
        if (existing != null)
        {
            manager.moveCamera = existing;
            return existing;
        }

        GameObject go = new GameObject("MoveCamera");

        // the manager's own scene, not whichever happens to be active: with more than one scene
        // loaded a free-look in the wrong one is unloaded out from under the cut.
        if (manager.gameObject.scene.IsValid())
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, manager.gameObject.scene);
        }

        MoveCamera created = go.AddComponent<MoveCamera>();
        manager.moveCamera = created;

        Undo.RegisterCreatedObjectUndo(go, "Create Cut Minigame");

        Debug.Log("No free-look in the scene, so a MoveCamera was created for this cut.", go);
        return created;
    }

    /// <summary>The scalpel this cut drives and its surface driver, created under the cut when it has none, and wired into the manager either way.</summary>
    /// <remarks>
    /// One scalpel per CUT, unlike the free-look: the driver snaps the transform it sits on onto the
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

            Undo.RegisterCreatedObjectUndo(go, "Create Cut Minigame");

            Debug.Log($"A Scalpel was created for {manager.name}. Give it a mesh, then place it.", go);
            return created;
        }

        if (follow.TryGetComponent(out ScalpelSurfaceDriver driver))
        {
            return driver;
        }

        driver = Undo.AddComponent<ScalpelSurfaceDriver>(follow.gameObject);
        Debug.Log($"Added a ScalpelSurfaceDriver to {follow.name} for this cut.", follow);
        return driver;
    }

    /// <summary>Builds one world-space closed-loop LineRenderer for a guide, sharing the guide material.</summary>
    private static LineRenderer CreateLine(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 0;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = GuideMaterial();
        return lr;
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
