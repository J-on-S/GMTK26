using UnityEngine;

/// <summary>Turns the raw mesh a slice hands back into a finished, grabbable body part: dynamic colliders, a <see cref="DetachedBodyPart"/>, the cut's placement offsets, and the finisher's kick.</summary>
/// <remarks>
/// Split off <see cref="CuttingManager"/> so the manager owns cut flow and this owns what the cut
/// leaves behind. The manager passes in what only it knows (the body, the part type, the item name,
/// the finisher); everything about how the piece ends up -- how healthy, where it sits, what it
/// sounds like -- lives here.
/// <para>A sibling of the manager (<see cref="RequireComponent"/> on the manager adds it), so a cut
/// authored before this existed gains one with default tuning the first time it is inspected. The
/// two offset fields are the only ones an old scene may have set on the manager; re-set them here.</para>
/// <para>Held size lives here for the same reason the sounds do: the piece is built at runtime from
/// a slice, so there is no prefab an author could put it on.</para>
/// </remarks>
public class SeveredPiece : MonoBehaviour
{
    [Tooltip("Health the severed piece starts and caps at, in seconds of freshness before it is spoiled.")]
    public float health = 60f;

    [Tooltip("Grab/drop sounds handed to the severed piece. The piece is built at runtime, so its audio can only come from here.")]
    public AudioGrappablePreset audioPreset;

    [Tooltip("Where the severed piece is put once it comes away, as an offset from the body it was cut from, in the body's own space. Zero leaves it exactly where the slice left it.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Rotation given to the severed piece once it comes away, in degrees, on top of the pose the slice left it in.")]
    public Vector3 rotationOffset = Vector3.zero;

    [Tooltip("Per-axis size the severed piece is held at, as a multiple of the size it lies at in the room. One = held at its real size. Only while it is in the hand -- on the floor or in the fridge it keeps the size the slice gave it.")]
    public Vector3 holdScaleMultiplier = Vector3.one;

    /// <summary>Fits out the raw slice result: convex colliders, a <see cref="DetachedBodyPart"/> of the given type and name, this cut's placement offsets, and the finisher's kick.</summary>
    /// <param name="piece">The GameObject the slice produced, in the body's local pose.</param>
    /// <param name="body">The body it was cut from -- the space the position offset is read in.</param>
    /// <param name="bodyPartType">The BodyPart asset the piece takes its identity from.</param>
    /// <param name="itemName">What the piece is called, so the rest of the game can ask for it by name.</param>
    /// <param name="finisher">The cut's finisher, for the kick and its direction; null skips the kick.</param>
    public void Outfit(GameObject piece, CuttableObject body, BodyPart bodyPartType, string itemName, CutFinisher finisher)
    {
        if (piece == null) return;

        MakeCollidersDynamic(piece);

        DetachedBodyPart part = DetachedBodyPart.MakeDetachedBodyPart(health, health, bodyPartType, piece, audioPreset, itemName);

        // held size has to be handed over here as well: the piece has no prefab an author could set it on,
        // and a limb cut at body scale is often too big to read in the hand.
        if (part != null) part.holdScaleMultiplier = holdScaleMultiplier;

        Place(part, body);

        Kick(piece, finisher);
    }

    /// <summary>Moves the freshly severed piece to where this cut wants it, by the offsets above.</summary>
    /// <remarks>
    /// The slice hands the piece back sitting exactly inside the body it came from, which is right for
    /// the frame it comes away and wrong for a part that should fall clear, sit on a tray, or face the
    /// camera. The offset is read in the body's own space, so it follows a client who is turned around.
    /// <para>The home pose is re-taken afterwards: <see cref="GrabbableObject"/> snapshots it in Awake,
    /// which ran when the component was added -- before this moved it -- so without this a respawn would
    /// send the part back inside the body.</para>
    /// </remarks>
    private void Place(DetachedBodyPart part, CuttableObject body)
    {
        if (part == null) return;

        Transform piece = part.transform;

        if (rotationOffset != Vector3.zero)
        {
            piece.rotation *= Quaternion.Euler(rotationOffset);
        }

        if (positionOffset != Vector3.zero)
        {
            Transform bodyTransform = body != null ? body.transform : null;
            piece.position += bodyTransform != null
                ? bodyTransform.TransformVector(positionOffset)
                : positionOffset;
        }

        part.SetStartPose(piece.position, piece.rotation);
    }

    private void Kick(GameObject piece, CutFinisher finisher)
    {
        // nothing to throw a piece with in edit mode: physics is not stepping, and the authoring copy
        // wants to stay where it was put.
        if (!Application.isPlaying) return;

        if (finisher == null) return;

        float force = finisher.Kick;
        if (force <= 0f) return;

        if (!piece.TryGetComponent(out Rigidbody body)) return;

        body.AddForce(-finisher.ApproachAxis * force, ForceMode.Impulse);
    }

    /// <summary>Turns the severed piece's mesh colliders convex, which a dynamic <c>Rigidbody</c> requires.</summary>
    /// <remarks>
    /// Invariant: a piece left concave logs <c>"Concave Mesh Colliders are not supported when used
    /// with dynamic Rigidbody GameObjects"</c> and falls through the world.
    /// <para>Invariant: only the severed piece is touched — the body keeps its exact concave shape
    /// for the aim raycasts.</para>
    /// </remarks>
    public static void MakeCollidersDynamic(GameObject piece)
    {
        MeshCollider[] colliders = piece.GetComponentsInChildren<MeshCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].convex = true;
        }
    }

    /// <summary>Rebuilds every mesh collider on a piece from its mesh as it stands.</summary>
    /// <remarks>For the authoring copy, where the mesh is re-centred and the piece moved after the
    /// colliders were made: the cook is cached per collider, so it keeps the shape it was first given.</remarks>
    public static void RecookColliders(GameObject piece)
    {
        MeshCollider[] colliders = piece.GetComponentsInChildren<MeshCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            CuttableObject.Recook(colliders[i], colliders[i].sharedMesh);
        }
    }

    /// <summary>One material per submesh, padded from the body's own list.</summary>
    /// <remarks>The severed piece carries the body's skin submeshes plus a cross-section cap, so it has
    /// one submesh more than the body; a renderer short of materials drops the extra silently.</remarks>
    public static Material[] MaterialsForPiece(Mesh piece, Material[] bodyMaterials)
    {
        int count = Mathf.Max(1, piece.subMeshCount);
        var slots = new Material[count];

        for (int i = 0; i < count; i++)
        {
            if (bodyMaterials == null || bodyMaterials.Length == 0)
            {
                slots[i] = null;
                continue;
            }

            slots[i] = i < bodyMaterials.Length ? bodyMaterials[i] : bodyMaterials[bodyMaterials.Length - 1];
        }

        return slots;
    }
}
