using System.Collections.Generic;
using EzySlice;
using UnityEngine;

/// <summary>One cutting plane on a body: where the cut goes, and how wide a window it cuts through.</summary>
/// <remarks>
/// The plane is this object's own transform -- its position and its up axis. Everything that was
/// once a single slot on <see cref="CuttableObject"/> lives here instead, which is what lets one
/// body carry an arm cut, a leg cut and a head cut at once rather than whichever was authored last.
/// <para>
/// The window is the <see cref="BoxCollider"/>'s box, with no number beside it to disagree with:
/// you size the window by dragging the collider's handles in the scene view, and what the box
/// outlines is what gets cut. The collider is an authoring handle only -- see <see cref="WindowBox"/>.
/// </para>
/// <para>
/// Nothing assigns a plane to the body. <see cref="CuttableObject.SpliceWindowed"/> takes one as an
/// argument, so the body never holds "which cut is happening" state that could be stale or raced.
/// </para>
/// </remarks>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class CutPlane : MonoBehaviour
{
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

    /// <summary>The box that IS the cut window. Cached, and guaranteed present by <c>RequireComponent</c>.</summary>
    /// <remarks>
    /// An authoring handle, not physics: keep the collider DISABLED. Its <c>size</c> and <c>center</c>
    /// still read and its box still draws, while an enabled one sits between the player's aim and the
    /// body and swallows the interaction raycast (see <see cref="OnValidate"/>).
    /// <para>Not a serialized slot -- there is nothing to point it at but this object, and a slot
    /// would only add a way to wire it to the wrong plane's box.</para>
    /// </remarks>
    public BoxCollider WindowBox
    {
        get
        {
            if (windowBox == null)
            {
                TryGetComponent(out windowBox);
            }
            return windowBox;
        }
    }

    /// <summary>Cached window box; resolved off this object, never authored.</summary>
    private BoxCollider windowBox;

    /// <summary>Size of the cut window: the box's X and Z, in this transform's local units.</summary>
    /// <remarks>
    /// Local, not world: the plane's own scale multiplies it downstream exactly as it scales the box
    /// the collider draws, so what the box outlines in the scene view is what gets cut.
    /// <para>The box's Y is dropped. A cut is a plane, unbounded along its normal, so a height would
    /// be a number that changes nothing.</para>
    /// <para>Zero when the box has somehow gone (it is <c>RequireComponent</c>'d, so only a
    /// deliberate delete does it). Zero fails closed -- every contour comes back clipped and the cut
    /// refuses with a reason -- where falling back to "no window" would silently cut the body in
    /// half on an infinite plane.</para>
    /// </remarks>
    public Vector2 WindowSize
    {
        get
        {
            BoxCollider box = WindowBox;
            return box != null ? new Vector2(box.size.x, box.size.z) : Vector2.zero;
        }
    }

    /// <summary>Centre of the cut window: the box's centre X and Z, in this transform's local units.</summary>
    /// <remarks>The box's Y centre is dropped with its Y size: sliding the window along the normal would not move the cut, only mislead about where it is.</remarks>
    public Vector2 WindowCenter
    {
        get
        {
            BoxCollider box = WindowBox;
            return box != null ? new Vector2(box.center.x, box.center.z) : Vector2.zero;
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
    /// <summary>Switches the freshly added window box out of physics, since it is here to be dragged and measured, not collided with.</summary>
    private void Reset()
    {
        BoxCollider box = WindowBox;
        if (box != null)
        {
            box.enabled = false;
        }
    }

    /// <summary>Takes the window box back out of the physics scene, saying so, whenever something has put it in.</summary>
    /// <remarks>
    /// Switches it off rather than only warning, because the collider that <c>RequireComponent</c>
    /// adds to a plane authored before the box existed arrives ENABLED -- and an enabled one sits
    /// between the player's aim and the body, swallows the interaction raycast, and leaves a cut
    /// that simply cannot be entered. Logged, so it is a decision you can see and undo rather than
    /// a component quietly changing under you.
    /// </remarks>
    private void OnValidate()
    {
        BoxCollider box = WindowBox;
        if (box != null && box.enabled)
        {
            box.enabled = false;
            Debug.Log(
                $"{name}: disabled the cut window's BoxCollider. It is the window's authoring handle — " +
                "its size and centre still read and its box still draws — but enabled it would swallow " +
                "the interaction raycast aimed at the body and the cut could never be entered.",
                this);
        }
    }


    private void OnDrawGizmos()
    {
        CuttableObject body = Target;

        // the window first, and NOT behind the body's Draw Cut Loops switch: drawing a rectangle
        // costs nothing, and the thing that switch turns off is the loop re-extraction below. A
        // window you cannot see is how one ends up many times the size of the part it cuts.
        if (drawLoop)
        {
            GizmoUtils.DrawBoundsGizmo(transform, WindowSize, WindowCenter);
        }

        if (!drawLoop || body == null || !body.drawCutLoops)
        {
            return;
        }

        // re-extracted every editor frame so dragging the plane updates the loop live. This is the
        // expensive part of authoring a cut; the body's Draw Cut Loops switch turns it off for
        // every plane at once once the placement is settled.
        gizmoLoops.Clear();
        gizmoLoops.AddRange(CuttableObject.GetLoops(body.gameObject, transform, body.weld, WindowSize, WindowCenter));

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
    }
#endif
}
