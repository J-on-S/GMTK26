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
