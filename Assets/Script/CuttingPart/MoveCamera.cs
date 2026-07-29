using UnityEngine;

/// <summary>Tints the piece the player is aiming at, in the colour that says whether its cut can be started.</summary>
/// <remarks>
/// Aiming resolves in three steps, because a cut does not live on the object it cuts. The ray finds
/// the body (a <see cref="CuttableObject"/>), <see cref="CutRegistry"/> maps the body back to its
/// cuts, and the hit point picks which of those would take the piece being pointed at. Regions
/// nest -- the hand sits inside both the wrist cut's piece and the shoulder cut's -- so the
/// registry hands back the innermost.
/// </remarks>
public class MoveCamera : MonoBehaviour
{
    [Header("Aim highlight")]
    [Tooltip("How far the tint reaches, in world units. Independent of Interactor's own reach, so a piece can be read from across the room and walked up to.")]
    public float highlightRange = 100f;

    [Tooltip("How many times a second the aim is re-resolved while still pointing at the same body. Resolving runs the real slicer over the whole body mesh, so this is the single biggest cost here. Aiming at a different body always resolves at once.")]
    public float resolvesPerSecond = 12f;

    [Tooltip("The cut can be started right now.")]
    public Color canCutColor = new(0f, 1f, 0f, 0.35f);

    [Tooltip("This piece has already been taken off.")]
    public Color completedColor = new(0f, 0.5f, 1f, 0.35f);

    [Tooltip("The cut is otherwise fine, but the player is holding the wrong tool for it.")]
    public Color wrongToolColor = new(1f, 0.92f, 0f, 0.35f);

    private Camera cam;

    /// <summary>What the player is carrying, for the tool check. Optional: with none, the player counts as empty-handed.</summary>
    private Interactor interactor;

    /// <summary>Highlighter currently lit, so it can be cleared when the aim moves off it.</summary>
    private CutRegionHighlighter litHighlighter;

    /// <summary>What is on screen now, so an unchanged tint is not rewritten every frame.</summary>
    private Mesh litMesh;
    private Color litColor;

    /// <summary>Body the last resolve was for, so aiming at a different one resolves without waiting.</summary>
    private CuttableObject lastBody;

    /// <summary>Time the next throttled resolve is due.</summary>
    private float nextResolve;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (cam == null)
        {
            return;
        }

        if (interactor == null)
        {
            // retried rather than resolved once, since the player can be spawned after this
            interactor = FindFirstObjectByType<Interactor>();
        }

        Aim();
    }

    /// <summary>Lights the piece under the crosshair, or clears the tint when none is aimed at.</summary>
    void Aim()
    {
        // screen centre: the cursor is locked, so a mouse position carries no information.
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, highlightRange)
            || !hit.collider.TryGetComponent(out CuttableObject body))
        {
            lastBody = null;
            Highlight(null, null, default);
            return;
        }

        // Resolving is expensive -- it runs the slicer over the whole body -- and the ray moves a
        // little every frame even when the answer cannot change. Sweeping onto a different body
        // resolves immediately; holding on one resolves at the throttled rate.
        if (body == lastBody && Time.time < nextResolve)
        {
            return;
        }

        lastBody = body;
        nextResolve = Time.time + (resolvesPerSecond > 0f ? 1f / resolvesPerSecond : 0f);

        // which cut would take the piece under the crosshair. Null means the upper hull -- the
        // part that stays attached -- and that is never tinted.
        CuttingManager aimed = CutRegistry.CutAt(body, hit.point);
        if (aimed == null)
        {
            Highlight(null, null, default);
            return;
        }

        string heldItem = interactor != null && interactor.heldObject != null ? interactor.heldObject.item.Name : null;
        bool hasTool = aimed.HasRequiredTool(heldItem);

        // completed wins: a finished cut reads as done whatever the player happens to be holding.
        Color color =
            aimed.getState() == CuttingManager.CuttingState.COMPLETED ? completedColor :
            !hasTool ? wrongToolColor :
            canCutColor;

        // the actual severed mesh, so the tint is the piece that would come away and nothing more
        Highlight(body, aimed.SeveredPreviewMesh, color);
    }

    /// <summary>Lights one body's severed piece, clearing whichever was lit before.</summary>
    /// <param name="body">A <c>null</c> body, or a <c>null</c> <paramref name="severedMesh"/>, clears the tint.</param>
    void Highlight(CuttableObject body, Mesh severedMesh, Color color)
    {
        CutRegionHighlighter target = body != null && severedMesh != null
            ? CutRegionHighlighter.For(body)
            : null;

        // already showing exactly this: rewriting it would churn a property block for no change
        if (target == litHighlighter && severedMesh == litMesh && color == litColor)
        {
            return;
        }

        // clear the old one first, so sweeping between two bodies never leaves both lit
        if (litHighlighter != null && litHighlighter != target)
        {
            litHighlighter.Hide();
        }

        litHighlighter = target;
        litMesh = severedMesh;
        litColor = color;

        if (target != null)
        {
            target.Show(severedMesh, color);
        }
    }

    void OnDisable()
    {
        // a tint left on would sit frozen on screen for the whole minigame
        Highlight(null, null, default);
    }
}
