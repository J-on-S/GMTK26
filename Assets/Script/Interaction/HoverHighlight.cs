using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws a material over an object while the player's aim is on it.</summary>
/// <remarks>
/// An overlay rather than an extra slot on the object's own renderer: Unity draws materials past the
/// submesh count over the LAST submesh only, and a severed body part's last submesh is its
/// cross-section cap -- so the "just append the outline material" trick would outline the cut face
/// and nothing else. A copy of the mesh with the highlight material in every slot outlines the whole
/// piece, and the object's own materials are never touched, so turning the highlight off cannot
/// leave it looking different from how it was authored.
/// <para>Added at runtime by <see cref="GrabbableObject"/>; nothing needs to place it by hand.</para>
/// </remarks>
[DisallowMultipleComponent]
public class HoverHighlight : MonoBehaviour
{
    /// <summary>Overlay objects are named with this prefix and skipped when the sources are collected, so a rebuild never overlays an overlay.</summary>
    private const string OverlayPrefix = "~";

    private const string OverlayName = OverlayPrefix + "HoverHighlight";

    /// <summary>One overlay renderer per source mesh renderer under this object.</summary>
    private readonly List<MeshRenderer> overlays = new();

    /// <summary>Material the overlays currently carry; a different one rebuilds them.</summary>
    private Material builtWith;

    private bool visible;

    /// <summary>Lights this object in <paramref name="material"/>. A null material clears.</summary>
    public void Show(Material material)
    {
        if (material == null)
        {
            Hide();
            return;
        }

        if (material != builtWith || overlays.Count == 0)
        {
            Rebuild(material);
        }

        SetVisible(true);
    }

    /// <summary>Turns the highlight off. Safe to call when it is already off.</summary>
    public void Hide()
    {
        SetVisible(false);
    }

    private void OnDisable()
    {
        Hide();
    }

    private void SetVisible(bool on)
    {
        if (visible == on)
        {
            return;
        }

        visible = on;
        for (int i = overlays.Count - 1; i >= 0; i--)
        {
            if (overlays[i] == null)
            {
                overlays.RemoveAt(i);
                continue;
            }
            overlays[i].enabled = on;
        }
    }

    /// <summary>Builds one overlay per mesh renderer under this object, all carrying <paramref name="material"/>.</summary>
    private void Rebuild(Material material)
    {
        ClearOverlays();
        builtWith = material;
        visible = false;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter.sharedMesh == null) continue;

            // '~' marks a runtime presentation object -- this class's own overlays and the cut
            // region highlighter's. Overlaying one would double-draw the highlight.
            if (filter.gameObject.name.StartsWith(OverlayPrefix)) continue;
            if (!filter.TryGetComponent(out MeshRenderer source)) continue;

            overlays.Add(BuildOverlay(filter, source, material));
        }
    }

    private static MeshRenderer BuildOverlay(MeshFilter filter, MeshRenderer source, Material material)
    {
        // HideAndDontSave: presentation rebuilt on demand. It must not be saved into the scene, nor
        // ride along as a stray child if the object is duplicated or a part is shelved.
        GameObject overlay = new GameObject(OverlayName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        // identity local transform: the overlay draws the same mesh in the same space as its source
        overlay.transform.SetParent(filter.transform, false);

        MeshFilter overlayFilter = overlay.AddComponent<MeshFilter>();
        overlayFilter.sharedMesh = filter.sharedMesh;

        MeshRenderer overlayRenderer = overlay.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterials = SlotsFor(filter.sharedMesh, material);
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.enabled = false;

        // the piece is lit by whatever light probes the source uses; matching them keeps a lit
        // highlight material from popping when the object is picked up and moved.
        overlayRenderer.lightProbeUsage = source.lightProbeUsage;
        overlayRenderer.reflectionProbeUsage = source.reflectionProbeUsage;

        return overlayRenderer;
    }

    /// <summary>The highlight material once per submesh: a renderer short of materials silently drops the extra submeshes, so part of the object would go unhighlighted.</summary>
    private static Material[] SlotsFor(Mesh mesh, Material material)
    {
        int count = Mathf.Max(1, mesh.subMeshCount);
        var slots = new Material[count];
        for (int i = 0; i < count; i++)
        {
            slots[i] = material;
        }
        return slots;
    }

    private void ClearOverlays()
    {
        for (int i = 0; i < overlays.Count; i++)
        {
            if (overlays[i] == null) continue;

            GameObject overlay = overlays[i].gameObject;
            if (Application.isPlaying) Destroy(overlay);
            else DestroyImmediate(overlay);
        }
        overlays.Clear();
    }

    private void OnDestroy()
    {
        ClearOverlays();
    }

    /// <summary>Gets the highlighter on an object, adding one if it hasn't got one.</summary>
    public static HoverHighlight For(GameObject target)
    {
        if (target == null)
        {
            return null;
        }
        if (!target.TryGetComponent(out HoverHighlight highlight))
        {
            highlight = target.AddComponent<HoverHighlight>();
        }
        return highlight;
    }
}
