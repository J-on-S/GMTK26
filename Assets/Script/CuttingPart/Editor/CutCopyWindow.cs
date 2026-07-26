using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Authoring front end for <see cref="CutCopier"/>: copy every cut of one body, or of a whole prefab, onto another body.</summary>
/// <remarks>
/// Deliberately thin. All of the copying and rebinding lives in <see cref="CutCopier"/>, which is
/// runtime code, so a body assembled at load time can get the same cuts without an editor around.
/// What is here and cannot be there: the selection, the undo entries and the report.
/// <para>A window rather than a menu item, because the operation needs two things picked and a
/// selection gives no reliable order to read "from" and "to" out of.</para>
/// </remarks>
public class CutCopyWindow : EditorWindow
{
    /// <summary>Where the cuts being copied come from.</summary>
    private enum SourceKind
    {
        /// <summary>A body in a loaded scene; its own cuts are copied.</summary>
        Body,
        /// <summary>A prefab (or any hierarchy) used as a library: every cut inside it is copied.</summary>
        Prefab,
    }

    private SourceKind sourceKind = SourceKind.Body;
    private CuttableObject from;
    private GameObject template;

    /// <summary>Bodies the cuts are copied onto. Always at least one row, so the window opens with a slot to fill.</summary>
    private readonly List<CuttableObject> targets = new() { null };

    private bool replaceExisting;
    private Vector2 scroll;

    [MenuItem("Tools/Cutting/Copy Cuts...", false, 20)]
    private static void Open()
    {
        var window = GetWindow<CutCopyWindow>(true, "Copy cuts");
        window.minSize = new Vector2(380f, 320f);
        window.PrefillFromSelection();
        window.Show();
    }

    /// <summary>Uses whatever is selected as the source, so the common case opens ready to go.</summary>
    /// <remarks>A selected prefab asset switches the window to prefab mode: nothing else could have been
    /// meant by picking one before opening this.</remarks>
    private void PrefillFromSelection()
    {
        GameObject picked = Selection.activeGameObject;
        if (picked == null) return;

        if (PrefabUtility.IsPartOfPrefabAsset(picked))
        {
            if (template == null) template = picked;
            sourceKind = SourceKind.Prefab;
            return;
        }

        CuttableObject body = picked.GetComponentInParent<CuttableObject>();
        if (body != null && from == null) from = body;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Copies cuts onto a body, keeping each cut's placement and rebinding it to that body. " +
            "Meant for bodies of the same shape.",
            MessageType.None);

        EditorGUILayout.Space();
        sourceKind = (SourceKind)EditorGUILayout.EnumPopup(
            new GUIContent("Source", "A body copies its own cuts. A prefab copies every cut inside it, whatever body they were authored against."),
            sourceKind);

        if (sourceKind == SourceKind.Body)
        {
            from = (CuttableObject)EditorGUILayout.ObjectField(
                new GUIContent("Copy cuts from", "Body whose cuts are the template. Left untouched."),
                from, typeof(CuttableObject), true);
        }
        else
        {
            template = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Prefab", "Prefab holding the authored cuts. Every cut inside it is copied. Left untouched."),
                template, typeof(GameObject), true);
        }

        DrawTargets();

        replaceExisting = EditorGUILayout.Toggle(
            new GUIContent("Replace existing cuts", "Deletes each target's own cuts first. Off, the copies are added to them."),
            replaceExisting);

        EditorGUILayout.Space();

        if (!Ready(out string problem))
        {
            EditorGUILayout.HelpBox(problem, MessageType.Info);
            return;
        }

        int sourceCount = SourceCuts().Count;
        List<CuttableObject> picked = PickedTargets();

        EditorGUILayout.LabelField($"{SourceName()}: {sourceCount} cut{(sourceCount == 1 ? "" : "s")}");
        for (int i = 0; i < picked.Count; i++)
        {
            int existing = CutCopier.CutsOn(picked[i]).Count;
            EditorGUILayout.LabelField($"{picked[i].name}: {existing} cut{(existing == 1 ? "" : "s")}" +
                                       (replaceExisting && existing > 0 ? "  (will be deleted)" : ""));
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(sourceCount == 0))
        {
            int total = sourceCount * picked.Count;
            string label = picked.Count == 1
                ? (total == 1 ? "Copy 1 cut" : $"Copy {total} cuts")
                : $"Copy {sourceCount} cuts to {picked.Count} bodies";

            if (GUILayout.Button(label, GUILayout.Height(24f)))
            {
                Copy();
            }
        }
    }

    /// <summary>The list of bodies to copy onto, with the rows to edit it.</summary>
    /// <remarks>A plain list rather than a <c>ReorderableList</c>: order carries no meaning here, and the
    /// two things an author actually does -- add a row, hand it the selection -- are one button each.</remarks>
    private void DrawTargets()
    {
        EditorGUILayout.LabelField(new GUIContent("Copy cuts to", "Bodies the copies are parented under and rebound to."), EditorStyles.boldLabel);

        using (var view = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.MaxHeight(140f)))
        {
            scroll = view.scrollPosition;

            for (int i = 0; i < targets.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    targets[i] = (CuttableObject)EditorGUILayout.ObjectField(targets[i], typeof(CuttableObject), true);

                    // one row always stays, so the window never presents nowhere to drop a body
                    using (new EditorGUI.DisabledScope(targets.Count == 1))
                    {
                        if (GUILayout.Button("-", GUILayout.Width(22f)))
                        {
                            targets.RemoveAt(i);
                            return;
                        }
                    }
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add row"))
            {
                targets.Add(null);
            }

            if (GUILayout.Button(new GUIContent("Use selection", "Replaces the list with every CuttableObject in the current selection.")))
            {
                TakeSelectionAsTargets();
            }
        }
    }

    /// <summary>Fills the target list from the hierarchy selection, ignoring anything that is not a body.</summary>
    private void TakeSelectionAsTargets()
    {
        var picked = new List<CuttableObject>();

        foreach (GameObject go in Selection.gameObjects)
        {
            CuttableObject body = go.GetComponentInParent<CuttableObject>();
            if (body != null && !picked.Contains(body)) picked.Add(body);
        }

        if (picked.Count == 0)
        {
            ShowNotification(new GUIContent("No CuttableObject selected"));
            return;
        }

        targets.Clear();
        targets.AddRange(picked);
    }

    /// <summary>The targets actually filled in, with blanks and duplicates dropped.</summary>
    private List<CuttableObject> PickedTargets()
    {
        var picked = new List<CuttableObject>();
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null && !picked.Contains(targets[i])) picked.Add(targets[i]);
        }
        return picked;
    }

    /// <summary>The cuts the current source offers, by whichever rule its kind uses.</summary>
    private List<CuttingManager> SourceCuts()
    {
        return sourceKind == SourceKind.Body
            ? CutCopier.CutsOn(from)
            : CutCopier.CutsIn(template);
    }

    private string SourceName()
    {
        if (sourceKind == SourceKind.Body) return from != null ? from.name : "(none)";
        return template != null ? template.name : "(none)";
    }

    private bool Ready(out string problem)
    {
        List<CuttableObject> picked = PickedTargets();
        if (picked.Count == 0)
        {
            problem = "Pick at least one body to copy the cuts to.";
            return false;
        }

        if (sourceKind == SourceKind.Body)
        {
            if (from == null)
            {
                problem = "Pick the body to copy from.";
                return false;
            }
            if (picked.Count == 1 && picked[0] == from)
            {
                problem = "Source and target are the same body.";
                return false;
            }
        }
        else if (template == null)
        {
            problem = "Pick the prefab holding the cuts.";
            return false;
        }

        // a target that is the source, or inside the template, is skipped with a warning rather than
        // blocking the run: with a list, one bad row must not stop the good ones.
        problem = null;
        return true;
    }

    /// <summary>Runs the copy as one undo step and reports what came out of it.</summary>
    /// <remarks>The delete is done here rather than through <c>CutCopier.RemoveCuts</c>, which destroys
    /// outright: an authoring action that deletes work has to be undoable, and only the editor can say so.</remarks>
    private void Copy()
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Copy cuts");

        List<CuttableObject> picked = PickedTargets();

        if (replaceExisting)
        {
            for (int t = 0; t < picked.Count; t++)
            {
                List<CuttingManager> existing = CutCopier.CutsOn(picked[t]);
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i] != null) Undo.DestroyObjectImmediate(existing[i].gameObject);
                }
            }
        }

        List<CuttingManager> copies = sourceKind == SourceKind.Body
            ? CutCopier.CopyCuts(from, picked)
            : CutCopier.CopyCutsFrom(template, picked);

        var stillMissing = new List<string>();
        for (int i = 0; i < copies.Count; i++)
        {
            Undo.RegisterCreatedObjectUndo(copies[i].gameObject, "Copy cuts");
            EditorUtility.SetDirty(copies[i]);

            List<string> missing = copies[i].MissingWiring();

            // MissingWiring only answers whether the references are filled in. It cannot say whether the
            // plane, in the place it was copied to, actually crosses THIS body's triangles -- and a cut
            // that severs nothing is invisible at runtime: no severed preview means no region, so aiming
            // highlights nothing and clicking opens nothing, with no error anywhere. One slice per copy
            // is worth paying for at authoring time to be told that here instead of in play.
            if (copies[i].SeveredPreviewMesh == null)
            {
                missing.Add("its plane severs nothing on this body (move the CutPlane or widen its window box)");
            }

            if (missing.Count > 0)
            {
                // named with its body: with several targets the same cut name comes back once per body,
                // and "Cut (Character): ..." three times over says nothing about which one to open.
                CuttableObject body = copies[i].GameObjectBeingCut;
                string who = body != null ? $"{body.name} / {copies[i].name}" : copies[i].name;
                stillMissing.Add($"{who}: {string.Join(", ", missing)}");
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (copies.Count > 0)
        {
            Selection.objects = ToGameObjects(copies);
            EditorGUIUtility.PingObject(copies[0]);
        }

        Report(copies.Count, picked, stillMissing);
    }

    private void Report(int copied, List<CuttableObject> picked, List<string> stillMissing)
    {
        Object context = picked.Count > 0 ? picked[0] : this;
        string where = picked.Count == 1 ? picked[0].name : $"{picked.Count} bodies";

        if (stillMissing.Count > 0)
        {
            Debug.LogWarning(
                $"Copied {copied} cut(s) to {where}, but some are not runnable yet:\n• " +
                string.Join("\n• ", stillMissing) + "\nFix them on each cut and press Auto-wire.", context);
        }
        else
        {
            Debug.Log($"Copied {copied} cut(s) to {where}. Check each guide line against the new mesh before playing.", context);
        }

        // the loop is re-extracted from the target's own triangles, so same placement is not the same
        // cut on a mesh that differs near the plane. Worth saying once, where it will be read.
        if (copied > 0)
        {
            ShowNotification(new GUIContent($"{copied} cut(s) copied"));
        }
    }

    private static Object[] ToGameObjects(List<CuttingManager> cuts)
    {
        var objects = new Object[cuts.Count];
        for (int i = 0; i < cuts.Count; i++)
        {
            objects[i] = cuts[i].gameObject;
        }
        return objects;
    }
}
