
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Free-look between cuts: turns the camera, works out which cut the player is aiming at, tints it, and starts it on click.</summary>
/// <remarks>
/// Aiming resolves in three steps, because a cut does not live on the object it cuts. The ray finds
/// the body (a <see cref="CuttableObject"/>), <see cref="CutRegistry"/> maps the body back to its
/// cuts, and the hit point picks which of those would take the piece being pointed at. Regions
/// nest -- the hand sits inside both the wrist cut's piece and the shoulder cut's -- so the
/// registry hands back the innermost.
/// </remarks>
public class MoveCamera  :  MonoBehaviour {

    public float speedH = 2.0f;
    public float speedV = 2.0f;

    [Header("Aim highlight")]
    [Tooltip("The cut can be started right now.")]
    public Color canCutColor = new(0f, 1f, 0f, 0.35f);

    [Tooltip("This piece has already been taken off.")]
    public Color completedColor = new(0f, 0.5f, 1f, 0.35f);

    [Tooltip("The cut is otherwise fine, but the player is holding the wrong tool for it.")]
    public Color wrongToolColor = new(1f, 0.92f, 0f, 0.35f);

    private float yaw = 0.0f;
    private float pitch = 0.0f;

    private Camera c;

    /// <summary>Highlighter currently lit, so it can be cleared when the aim moves off it.</summary>
    private CutRegionHighlighter litHighlighter;

    /// <summary>What the player is carrying; supplies the tool check. Found once.</summary>
    private PlayerInventoryandInteraction inventory;

    void Start() {
        c = Camera.main;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        yaw = c.transform.eulerAngles.y;
        pitch = c.transform.eulerAngles.x;

        inventory = FindFirstObjectByType<PlayerInventoryandInteraction>();
    }

    void Update() {

        Vector2 move = CuttingManager.mouseDelta.ReadValue<Vector2>();
        yaw += speedH * move.x;
        pitch -= speedV * move.y;

        c.transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);

        CheckStartMinigame();
    }

    void CheckStartMinigame()
    {
        // aim is the screen centre: the cursor is locked, so a mouse position carries no information.
        Ray ray = c.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit)
            || !hit.collider.TryGetComponent(out CuttableObject body))
        {
            Highlight(null, null, default);
            return;
        }

        // which cut would take the piece under the crosshair. Null means the upper hull -- the
        // part that stays attached -- and that is never tinted.
        CuttingManager aimed = CutRegistry.CutAt(body, hit.point);
        if (aimed == null)
        {
            Highlight(null, null, default);
            return;
        }

        bool hasTool = aimed.HasRequiredTool(inventory);
        bool ready = aimed.canEnterMinigame() && hasTool;

        // completed wins: a finished cut reads as done whatever the player happens to be holding.
        Color color =
            aimed.getState() == CuttingManager.CuttingState.COMPLETED ? completedColor :
            !hasTool ? wrongToolColor :
            canCutColor;

        // the actual severed mesh, so the tint is the piece that would come away and nothing more
        Highlight(body, aimed.SeveredPreviewMesh, color);

        // edge, not held: isPressed would re-enter the instant the player quit with the button down.
        if (ready && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // drop the tint before handing over: this script stops updating during the cut.
            Highlight(null, null, default);
            aimed.EnterMinigame();
        }
    }

    /// <summary>Lights one body's severed piece, clearing whichever was lit before. Pass a null body or mesh to clear.</summary>
    void Highlight(CuttableObject body, Mesh severedMesh, Color color)
    {
        CutRegionHighlighter target = body != null && severedMesh != null
            ? CutRegionHighlighter.For(body)
            : null;

        // clear the old one first, so sweeping between two bodies never leaves both lit
        if (litHighlighter != null && litHighlighter != target)
        {
            litHighlighter.Hide();
        }

        litHighlighter = target;
        if (target != null)
        {
            target.Show(severedMesh, color);
        }
    }

    void OnDisable()
    {
        // the cut takes the camera from here; a tint left on would sit frozen on screen for the
        // whole minigame.
        Highlight(null, null, default);
    }
}
