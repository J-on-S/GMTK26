using System.Collections.Generic;
using EzySlice;
using UnityEngine;

/// <summary>One cutting plane on a body: where the cut goes, and how wide a window it cuts through.</summary>
/// <remarks>
/// The plane is this object's own transform -- its position and its up axis. Everything that was
/// once a single slot on <see cref="CuttableObject"/> lives here instead, which is what lets one
/// body carry an arm cut, a leg cut and a head cut at once rather than whichever was authored last.
/// <para>
/// Nothing assigns a plane to the body. <see cref="CuttableObject.SpliceWindowed"/> takes one as an
/// argument, so the body never holds "which cut is happening" state that could be stale or raced.
/// </para>
/// </remarks>
[ExecuteAlways]
public class CutPlane : MonoBehaviour
{
    [Tooltip("Rectangle on the plane, in this transform's local units (X = right, Y = forward). It rotates with the plane; contours it clips are discarded from the cut, so keep it large enough to cover the loop you want cut and small enough to miss the limbs you don't.")]
    public Vector2 boundsSize = Vector2.one;

    [Tooltip("Draw this plane's cut loop and window in the scene view. The body's own Draw Cut Loops has to be on as well.")]
    public bool drawLoop = true;

    [Tooltip("Body this plane cuts. Left empty, the nearest CuttableObject up the hierarchy is used.")]
    public CuttableObject target;

    [Tooltip("Outward offset of the orange preview loop from its centre, for the gizmo only.")]
    public float previewScale = 0.05f;

    /// <summary>Loops last extracted for the gizmo, in the body's mesh-local space.</summary>
    private readonly List<CuttableObject.SavedLoop> gizmoLoops = new();

    /// <summary>The body this plane cuts: the explicit reference, else the nearest one up the hierarchy.</summary>
    public CuttableObject Target
    {
        get
        {
            if (target == null)
            {
                target = GetComponentInParent<CuttableObject>();
            }
            return target;
        }
    }

    /// <summary>Cut-plane normal in world space -- this transform's up.</summary>
    public Vector3 Normal => transform.up;

    /// <summary>A point on the cut plane, in world space.</summary>
    public Vector3 Origin => transform.position;

    /// <summary>Signed distance from a world point to this plane. Negative = the side the severed piece is on.</summary>
    public float SignedDistance(Vector3 worldPoint)
    {
        return Vector3.Dot(worldPoint - Origin, Normal);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        CuttableObject body = Target;
        if (!drawLoop || body == null || !body.drawCutLoops)
        {
            return;
        }

        // re-extracted every editor frame so dragging the plane updates the loop live. This is the
        // expensive part of authoring a cut; the body's Draw Cut Loops switch turns it off for
        // every plane at once once the placement is settled.
        gizmoLoops.Clear();
        gizmoLoops.AddRange(CuttableObject.GetLoops(body.gameObject, transform, body.weld, boundsSize));

        for (int i = 0; i < gizmoLoops.Count; i++)
        {
            var preview = new CuttableObject.SavedLoop
            {
                closed = gizmoLoops[i].closed,
                points = CutContour.ScaleLoop(gizmoLoops[i].points, previewScale),
            };
            GizmoUtils.DrawLoop(body.transform, preview, Color.orange, false);
        }

        CuttableObject.DrawLoops(body.transform, gizmoLoops, Color.green, true);

        GizmoUtils.DrawBoundsGizmo(transform, boundsSize);
    }
#endif
}
