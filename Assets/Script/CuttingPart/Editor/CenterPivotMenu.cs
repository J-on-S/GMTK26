using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Puts a GameObject's origin in the middle of the mesh it shows, without anything moving on screen.</summary>
/// <remarks>
/// The editor counterpart of <see cref="CuttableObject.CenterPivot"/>. That one is for a piece cut at
/// runtime, whose mesh is a fresh instance nobody else holds, so it shifts the vertices in place. An
/// authored object shows an IMPORTED mesh: shifting those vertices edits an asset shared by every
/// object using it, and the edit is thrown away at the next reimport of the model. So the vertices are
/// shifted in a saved COPY of the mesh, and that copy is what the object gets.
/// <para>Invariants, both of which are the point of the tool:</para>
/// <list type="bullet">
/// <item><description>the mesh does not move -- the vertices go back by the same offset the transform
/// goes forward;</description></item>
/// <item><description>the children do not move -- their world poses are taken before the transform
/// moves and written back after, so cuts, planes and scalpels stay on the anatomy they were authored
/// against rather than being dragged along by the parent.</description></item>
/// </list>
/// </remarks>
public static class CenterPivotMenu
{
    /// <summary>Folder the centred copies are written to, created on first use.</summary>
    private const string CenteredFolder = "Assets/1_3dModels/CenteredMeshes";

    [MenuItem("Tools/Cutting/Center Pivot On Mesh", false, 22)]
    private static void CenterPivotOnSelection()
    {
        GameObject[] picked = Selection.gameObjects;
        int done = 0;

        for (int i = 0; i < picked.Length; i++)
        {
            if (CenterPivot(picked[i])) done++;
        }

        if (done == 0)
        {
            Debug.Log("Center Pivot: nothing to do -- the selection has no mesh whose pivot is off centre.");
        }
    }

    [MenuItem("Tools/Cutting/Center Pivot On Mesh", true)]
    private static bool CanCenterPivot()
    {
        GameObject[] picked = Selection.gameObjects;
        for (int i = 0; i < picked.Length; i++)
        {
            if (picked[i] != null && picked[i].TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Centres one object's pivot on its mesh. Returns false when there was nothing to move.</summary>
    public static bool CenterPivot(GameObject target)
    {
        if (target == null) return false;

        if (target.TryGetComponent(out SkinnedMeshRenderer _))
        {
            Debug.LogWarning($"{target.name} is skinned: its pivot is the rig's, not the mesh's, and moving it here would desync the bones. Re-export it centred instead.", target);
            return false;
        }

        if (!target.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
        {
            return false;
        }

        Mesh source = filter.sharedMesh;
        Vector3 center = source.bounds.center;

        // a tolerance rather than == zero: an imported mesh authored centred still comes back with a
        // bounds centre a float epsilon off, and rewriting a mesh asset to move a pivot by 1e-8 is noise.
        if (center.magnitude < 1e-5f)
        {
            Debug.Log($"{target.name}: pivot is already on the mesh centre.", target);
            return false;
        }

        Mesh centered = SaveCenteredCopy(source, center);
        if (centered == null) return false;

        Undo.SetCurrentGroupName("Center Pivot On Mesh");
        int group = Undo.GetCurrentGroup();

        // taken BEFORE the transform moves: these are the poses that have to survive it. Cuts hang off
        // the body, and a cut dragged along by its parent is exactly the mismatch this avoids.
        Transform t = target.transform;
        var children = new List<Transform>(t.childCount);
        var poses = new List<(Vector3 position, Quaternion rotation)>(t.childCount);
        for (int i = 0; i < t.childCount; i++)
        {
            Transform child = t.GetChild(i);
            children.Add(child);
            poses.Add((child.position, child.rotation));
            Undo.RecordObject(child, "Center Pivot On Mesh");
        }

        Undo.RecordObject(filter, "Center Pivot On Mesh");
        filter.sharedMesh = centered;

        // TransformVector, not TransformPoint: a displacement, carrying the object's own rotation and
        // scale so the mesh lands back exactly where it was.
        Undo.RecordObject(t, "Center Pivot On Mesh");
        t.position += t.TransformVector(center);

        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetPositionAndRotation(poses[i].position, poses[i].rotation);
            EditorUtility.SetDirty(children[i]);
        }

        // the collider caches the mesh it cooked, so it keeps the old shape -- at the old place -- until
        // it is made to read the new vertices.
        if (target.TryGetComponent(out MeshCollider collider))
        {
            Undo.RecordObject(collider, "Center Pivot On Mesh");
            CuttableObject.Recook(collider, centered);
            EditorUtility.SetDirty(collider);
        }

        EditorUtility.SetDirty(filter);
        EditorUtility.SetDirty(t);
        Undo.CollapseUndoOperations(group);

        Debug.Log($"{target.name}: pivot centred on its mesh (moved {center:F4} in mesh space), now using {AssetDatabase.GetAssetPath(centered)}.", target);
        return true;
    }

    /// <summary>Writes a copy of a mesh with its vertices shifted back by the offset, as an asset of its own.</summary>
    /// <remarks>
    /// A copy, because the source is normally an imported model's mesh: editing it would change every
    /// object using that model and would be thrown away at the next reimport. The copy is a plain mesh
    /// asset, which reimporting the model cannot touch.
    /// <para>Re-running on the same mesh overwrites the same asset rather than piling up copies -- so a
    /// second run is a correction, not a new file.</para>
    /// </remarks>
    private static Mesh SaveCenteredCopy(Mesh source, Vector3 center)
    {
        var centered = Object.Instantiate(source);
        centered.name = $"{source.name}_centered";

        Vector3[] vertices = centered.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= center;
        }
        centered.vertices = vertices;
        centered.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder(CenteredFolder))
        {
            string parent = System.IO.Path.GetDirectoryName(CenteredFolder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                Debug.LogError($"Center Pivot: {parent} does not exist, so the centred mesh has nowhere to go.");
                Object.DestroyImmediate(centered);
                return null;
            }
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(CenteredFolder));
        }

        string path = $"{CenteredFolder}/{centered.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

        if (existing != null)
        {
            // the same asset, refilled: every MeshFilter already pointing at it follows, where a second
            // file would leave half the scene on the old copy.
            existing.Clear();
            EditorUtility.CopySerialized(centered, existing);
            existing.name = centered.name;
            Object.DestroyImmediate(centered);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(centered, path);
        AssetDatabase.SaveAssets();
        return centered;
    }
}
