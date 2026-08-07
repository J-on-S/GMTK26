using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Moves the follower left/right along the limb, snaps it onto the mesh surface, scores it, and draws its gizmos. The orbit angle around the loop is driven externally (by <see cref="CuttingManager"/>); this script does not sync it.</summary>
/// <remarks>Invariant: runs before its own <c>CameraFollow</c>, so a snap uses the same frame's orbit angle.</remarks>
[ExecuteAlways]
[RequireComponent(typeof(CameraFollow))]
[DefaultExecutionOrder(-10)]
public class ScalpelSurfaceDriver : MonoBehaviour
{
    /// <summary>Supplies the cut loop, its centre, and surface projection.</summary>
    [HideInInspector] public LoopGuideBuilder builder;

    /// <summary>Which way mouse-x slides the object across the loop.</summary>
    public enum SlideAxis {
        /// <summary>Along the plane normal (up/down the limb axis).</summary>
        PlaneNormal,
        /// <summary>Sweep the sample along the limb over a sphere of the loop radius, instead of a straight axial slide.</summary>
        RadialPerpendicular,
    }

    [Tooltip("RadialPerpendicular only: largest sweep angle from the loop, in degrees. Kept under 90 so the sample can't cross the pole (top of the limb) and wrap onto the far side.")]
    public float radialMaxAngle = 85f;

    /// <summary>What drives left/right travel along the limb.</summary>
    public enum MoveInput {
        /// <summary>Horizontal mouse motion; speed scaled per-pixel by <c>Xspeed</c>.</summary>
        MouseDelta,
        /// <summary>Hold left click = left, right click = right; speed scaled per-second by <c>Xspeed</c>.</summary>
        MouseButtons,
        /// <summary>Left/right arrow keys; speed scaled per-second by <c>Xspeed</c>.</summary>
        ArrowKeys,
    }

    /// <summary>Serialized tuning bundle: input mode, slide axis, speeds, and smoothing.</summary>
    [HideInInspector] public ScalpelSurfacePreset preset;

    /// <summary>Left/right arrow-and-A/D axis driving along-limb travel.</summary>

    /// <summary>This object's own <c>CameraFollow</c>, supplying <c>BasePosition</c> and aim while this script owns the surface-snapped position. Its orbit angle is driven externally by <see cref="CuttingManager"/>.</summary>
    private CameraFollow owned;

    /// <summary>Along-limb travel, in world units for <c>PlaneNormal</c> mode or degrees for <c>RadialPerpendicular</c>.</summary>
    private float offset;

    /// <summary>Distance the object floats above the surface along the smoothed normal, in world units.</summary>
    public float ObjectHover = 0.01f;

    [Header("Trace")]
    [Tooltip("Draw the path the scalpel walks over the surface into traceRenderer.")]
    public bool drawTrace = false;

    public LineRenderer traceRenderer;

    [Tooltip("Material the trace line is drawn with. Assign it here rather than on the LineRenderer: the line's own settings are rewritten by this component, so a hand-made renderer is only worth adding for its material.")]
    public Material traceMaterial;

    [Tooltip("Line width, in world units.")]
    public float traceWidth = 0.005f;

    [Tooltip("How far the drawn line floats off the body, in world units, in edit mode and in play alike. Drawing only -- it never moves the scalpel, which floats at Object Hover. Too low and the line z-fights the mesh and looks tangled.")]
    public float traceHover = 0.01f;

    [Tooltip("Min world distance between stored points; skips near-duplicates so the list stays small. 0 stores one point every frame the scalpel is on the surface.")]
    public float traceMinStep = 0.001f;

    [Tooltip("Max world distance between stored points. A jump longer than this is filled in with points along the way, so a fast sweep draws a curve instead of a straight chord between two frames. 0 turns the fill-in off.")]
    public float traceMaxStep = 0.01f;

    [Tooltip("Most fill-in points one frame may add. Only reached when the scalpel jumps a long way at once (an entry, a teleport); it keeps that frame from queueing thousands of points.")]
    public int traceMaxFillPerFrame = 64;

    private readonly List<Vector3> tracePoints = new List<Vector3>();

    /// <summary>The body the trace is drawn on. Its transform is the frame the line's points live in, so a cut drawn on it moves, turns and slices with the body instead of hanging in world space where the cut was made.</summary>
    /// <remarks><c>null</c> until a cut wires the guide's <see cref="LoopGuideBuilder.meshFollow"/>; while it is, the line falls back to world space on the scalpel so authoring without a body still previews.</remarks>
    private Transform TraceSpace => builder != null && builder.meshFollow != null ? builder.meshFollow.transform : null;

    /// <summary>The cut body's own scale as a single factor, so the trace width and hover lift stay proportional to a body scaled up or down. Mirrors <c>CuttingManager.BodyScale</c>: a LineRenderer's width is world-space and ignores the host's transform scale, so it has to be scaled by hand. The average of the three lossy-scale axes, to survive a non-uniform scale; 1 when no body is wired yet.</summary>
    private float BodyScale
    {
        get
        {
            Transform t = TraceSpace;
            if (t == null) return 1f;
            Vector3 s = t.lossyScale;
            return (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
        }
    }

    /// <summary>The object the line is hosted on when it is hung on the body -- a managed child of <see cref="TraceSpace"/>, so its local frame equals the body's.</summary>
    private Transform traceHost;

    /// <summary>Most recent surface hit with hover applied; held through frames the ray misses.</summary>
    private Vector3 lastSurfacePos;

    /// <summary>The same hit lifted by the TRACE's own hover: where the drawn line goes, as opposed to where the scalpel floats.</summary>
    /// <remarks>
    /// Kept apart from <see cref="lastSurfacePos"/> on purpose. Trailing the scalpel's own position
    /// instead made the drawn line inherit two things that are not the line's: <see cref="ObjectHover"/>,
    /// so <see cref="traceHover"/> did nothing in play and the line sat at a height the edit-mode preview
    /// never showed, and the <c>followSmooth</c> ease, which lags the real surface point and cuts corners
    /// through the mesh on a tight sweep.
    /// </remarks>
    private Vector3 lastTracePos;

    /// <summary>Whether <c>lastSurfacePos</c> holds a real hit yet.</summary>
    private bool hasSurface;

    /// <summary>Low-passed surface normal for the hover lift, so it doesn't step between triangles.</summary>
    private Vector3 smoothedNormal;

    /// <summary>Whether <c>smoothedNormal</c> has been seeded.</summary>
    private bool hasNormal;

    /// <summary>Latest raw surface hit before hover, shared with the precision gizmo.</summary>
    private Vector3 onMeshPos;

    void Start()
    {
        owned = GetComponent<CameraFollow>();

        ApplyTraceWidth(); // no-op without a renderer; never creates one

        if (!Application.isPlaying) return;

        // after the guard, not before: [ExecuteAlways] runs Start in the editor too, and this call can
        // AddComponent a LineRenderer. Doing that in edit mode adds a component to whatever the scalpel
        // is -- on a prefab instance that is an "Added Component" override nobody asked for, with no
        // Undo entry, saved into the scene the next time it is written.
        EnsureTraceRenderer();

      
        Cursor.lockState = CursorLockMode.Locked;

        HideScalpelRenderers();


        ResetTrace();

        
        enabled = false;
    }

    void HideScalpelRenderers()
    {
        Transform root = owned != null ? owned.transform : transform;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is LineRenderer) continue;
            renderers[i].enabled = false;
        }
    }

    LineRenderer EnsureTraceRenderer()
    {
        if (owned == null) owned = GetComponent<CameraFollow>();
        if (owned == null)
        {
            Debug.LogError($"{name}: no CameraFollow to hang the scalpel trace on, so the cut line cannot be drawn.", this);
            return null;
        }

        // the line hangs on a child of the body, so its local frame is the body's and its points travel
        // with the body. Until a cut wires the body, it lives on the scalpel in world space instead.
        // Play-only: edit-mode authoring keeps the world-space preview on the scalpel so selecting a
        // driver never spawns a child on the body it points at and saves it into the scene.
        Transform space = Application.isPlaying ? TraceSpace : null;
        Transform wantHost = space != null ? TraceHost(space) : owned.transform;

        // already on the right host: keep it, but re-apply the settings the frame may have changed.
        if (traceRenderer != null && traceRenderer.transform == wantHost)
        {
            ConfigureTraceRenderer();
            return traceRenderer;
        }

        // read before the slot below is repointed: this is the hand-authored line the edit-mode preview
        // draws into, and its look is the one the author signed off on. A renderer created for play
        // copies it, so the preview is a preview.
        LineRenderer authored = traceRenderer;

        // host moved (a body was wired, or a different one): the old line's points are in the old frame,
        // so drop them and start the trail again on the new host.
        if (traceRenderer != null && traceRenderer.transform != wantHost)
        {
            tracePoints.Clear();
            traceRenderer.positionCount = 0;
        }

        // and the same for whatever line is parked on the fallback host, whether or not this driver's
        // slot points at it. A LineRenderer's points are serialized, so the ring the edit-mode preview
        // drew onto the scalpel is still there when play starts: on a cut whose traceRenderer was never
        // assigned the branch above cannot reach it, and the authored ring hangs over the body,
        // undrivable and never cleared, for the whole run. It is also the only thing left to copy a
        // look from, so an unassigned cut still gets the authored one rather than the bare defaults.
        if (wantHost != owned.transform
            && owned.TryGetComponent(out LineRenderer parked)
            && parked != traceRenderer)
        {
            parked.positionCount = 0;
            if (authored == null) authored = parked;
        }

        if (!wantHost.TryGetComponent(out traceRenderer))
        {
            traceRenderer = wantHost.gameObject.AddComponent<LineRenderer>();
            traceRenderer.positionCount = 0;

            CopyPresentation(authored, traceRenderer);
            ApplyTraceWidth();
        }

        ConfigureTraceRenderer();
        return traceRenderer;
    }

    /// <summary>Gives a freshly created trace line the look of the one the author tuned on the scalpel.</summary>
    /// <remarks>
    /// The two lines are different components on different objects: edit mode previews into a
    /// LineRenderer authored by hand, play draws into one this component creates on the body. Anything
    /// that lives only on the authored component does not travel unless it is copied, and a fresh
    /// LineRenderer arrives with <c>textureMode</c> at <c>Stretch</c> — which maps the whole trace
    /// texture once across the line instead of repeating it along it. With a stitch texture that is the
    /// difference between a row of stitches in the scene view and one smear in play, and it is exactly
    /// the setting the note at the bottom of <c>LoopGuideBuilder</c> asks authors to set to Tile.
    /// <para>Deliberately not copied: points (the trail owns its own), width (<c>traceWidth</c> through
    /// <see cref="ApplyTraceWidth"/>), space (follows the host, see <see cref="ConfigureTraceRenderer"/>)
    /// and material (this component's <c>traceMaterial</c> slot). Those four have an owner already, and
    /// copying them would let the authored line quietly overrule it.</para>
    /// </remarks>
    /// <param name="from">The authored line, or <c>null</c> when this cut has none to follow.</param>
    private static void CopyPresentation(LineRenderer from, LineRenderer to)
    {
        if (to == null) return;

        if (from == null)
        {
            // nothing authored to follow. Tile rather than the LineRenderer default of Stretch: every
            // trace material in this project is a repeating stitch, so Stretch is never the right guess.
            to.alignment = LineAlignment.View;
            to.textureMode = LineTextureMode.Tile;
            to.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            to.receiveShadows = false;
            return;
        }

        to.alignment = from.alignment;
        to.textureMode = from.textureMode;
        to.textureScale = from.textureScale;
        to.colorGradient = from.colorGradient;
        to.numCornerVertices = from.numCornerVertices;
        to.numCapVertices = from.numCapVertices;
        to.shadowBias = from.shadowBias;
        to.generateLightingData = from.generateLightingData;
        to.shadowCastingMode = from.shadowCastingMode;
        to.receiveShadows = from.receiveShadows;
        to.sortingLayerID = from.sortingLayerID;
        to.sortingOrder = from.sortingOrder;
    }

    /// <summary>The managed child of the body that hosts this driver's line, sitting at the body's origin so its local space is the body's own.</summary>
    /// <remarks>One per driver, not one per body: several cuts share a body, and a child keyed by name
    /// would have the second cut's run reuse the first's line and <c>ResetTrace</c> wipe the cut it left.
    /// Cached for the driver's life and rebuilt only if the body it hung under was destroyed.</remarks>
    private Transform TraceHost(Transform space)
    {
        if (traceHost != null && traceHost.parent == space) return traceHost;

        GameObject go = new GameObject($"ScalpelTrace ({name})");
        go.transform.SetParent(space, false);
        go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        traceHost = go.transform;
        return traceHost;
    }

    /// <summary>Holds the one renderer setting the trace cannot work without, and the material when this component owns it.</summary>
    /// <remarks>
    /// Space follows the host. On the body's <c>ScalpelTrace</c> child <c>useWorldSpace</c> is off, so the
    /// points -- stored in that child's local frame, which is the body's -- travel with the body when it
    /// moves, turns or is sliced. On the fallback host (the scalpel, before a body is wired) it is on,
    /// because the scalpel is moved and orbited every frame and local points would wind around it.
    /// Deliberately narrow: alignment, texture mode, shadows and the rest are presentation, set once when
    /// this component creates the renderer and left to the author from then on.
    /// <para>Play only, though. The edit-mode preview also lives on the scalpel, and a LineRenderer's
    /// points are serialized -- so world-space points there wrote the whole ring into the prefab in
    /// coordinates that depend on where the body was standing, and rewrote every one of them whenever it
    /// was next opened somewhere else. The preview has no winding to fear: it rebuilds the entire ring
    /// each frame rather than appending to a trail, so it is stable in the scalpel's own space.</para>
    /// </remarks>
    private void ConfigureTraceRenderer()
    {
        if (traceRenderer == null) return;

        // world space only while a RUNNING trail is parked on the scalpel; on the body's own child the
        // points are body-local so the line rides the body, and the edit-mode preview is always local so
        // nothing it draws is serialized in world coordinates.
        traceRenderer.useWorldSpace = Application.isPlaying
            && (owned == null || traceRenderer.transform == owned.transform);

        // only when the slot above is filled: an empty slot means the material is the renderer's own.
        if (traceMaterial != null && traceRenderer.sharedMaterial != traceMaterial)
        {
            traceRenderer.sharedMaterial = traceMaterial;
        }
    }

    /// <summary>Converts a world-space surface hit into whatever space the line is currently drawn in.</summary>
    private Vector3 ToRendererSpace(Vector3 worldPoint)
    {
        if (traceRenderer == null || traceRenderer.useWorldSpace) return worldPoint;
        return traceRenderer.transform.InverseTransformPoint(worldPoint);
    }

    /// <summary>Puts the trace line back on screen, and in edit mode fills it with the whole loop the scalpel would walk.</summary>
    /// <remarks>
    /// A run only ever draws the stretch the player got through, and edit mode runs no cut at all, so
    /// the honest authoring view is the finished line: the entire target loop, at the trace's own width
    /// and material. That is what the cut is supposed to leave behind.
    /// <para>Never creates the renderer -- that is play-mode work, so edit mode cannot dirty the scene
    /// just by having this component selected.</para>
    /// </remarks>
    public void ShowTrace()
    {
        LineRenderer line = traceRenderer;
        if (line == null) return;

        line.enabled = true;
        ConfigureTraceRenderer();

        // in play the points are the player's own cut, appended a frame at a time; leave them alone.
        if (Application.isPlaying) return;

        if (builder == null) return;

        // the loop as it sits on the surface, then lifted by this line's own hover: drawn flush with the
        // mesh it z-fights and dips through it, which reads as a tangle rather than a ring.
        if (!builder.TryGetFlatLoop(out Vector3 center, out List<Vector3> points))
        {
            return;
        }

        if (points == null || points.Count < 2) return;

        // nothing about the drawn preview has changed since the last one: leave the renderer alone.
        // A LineRenderer's points are serialized, so rewriting them every editor frame keeps the scene
        // permanently dirty and, on a prefab instance, keeps a positions override alive that nobody
        // authored -- the whole array turning up in every diff of the scene or prefab.
        float hover = traceHover * BodyScale;
        float width = traceWidth * BodyScale;
        if (ReferenceEquals(points, previewSource)
            && previewHost == line.transform
            && Mathf.Approximately(previewHover, hover)
            && Mathf.Approximately(previewWidth, width)
            && line.positionCount == previewCount)
        {
            return;
        }

        previewSource = points;
        previewHost = line.transform;
        previewHover = hover;
        previewWidth = width;

        // copied, never written in place: with a hover of zero BuildHoverLift hands the list straight
        // back, and the space conversion below would then rewrite the builder's own cached loop.
        var drawn = new List<Vector3>(builder.BuildHoverLift(center, points, hover));
        if (drawn.Count < 2) return;

        // the loop comes out of the builder in world space; hand it to the line in whatever space the
        // line draws in, so a body-hosted preview sits on the body rather than winding off it.
        for (int i = 0; i < drawn.Count; i++) drawn[i] = ToRendererSpace(drawn[i]);

        previewCount = drawn.Count;

        // the reference check above is instance state, so a domain reload or a prefab reopen empties it
        // and this frame would rewrite the array with the values already in it -- exactly the override
        // that comment is about. Comparing what the line actually stores survives the reload.
        if (!LoopGuideBuilder.HoldsPoints(line, drawn))
        {
            // closed: the full line is the whole ring, not a ring with a gap where the run would have started
            if (!line.loop) line.loop = true;
            line.positionCount = drawn.Count;
            line.SetPositions(drawn.ToArray());
        }

        ApplyTraceWidth();
    }

    // what the edit-mode preview was last drawn from, so an unchanged frame writes nothing
    private List<Vector3> previewSource;
    private Transform previewHost;
    private float previewHover = float.NaN;
    private float previewWidth = float.NaN;
    private int previewCount = -1;

#if UNITY_EDITOR
    // guards against stacking one deferred apply per OnValidate call; not serialized, purely edit-time.
    [System.NonSerialized] private bool _widthApplyQueued;
#endif

    /// <remarks>Deferred for the same reason as <c>CuttingManager.OnValidate</c>: the width lands on the
    /// LineRenderer, another object, and a validate that fires during a prefab apply must not fight it.</remarks>
    void OnValidate()
    {
#if UNITY_EDITOR
        if (_widthApplyQueued) return;
        _widthApplyQueued = true;
        UnityEditor.EditorApplication.delayCall += RunDeferredWidthApply;
#endif
    }

#if UNITY_EDITOR
    private void RunDeferredWidthApply()
    {
        _widthApplyQueued = false;

        if (this == null) return; // destroyed between the validate and this callback
        ApplyTraceWidth();
    }
#endif

    void Update()
    {
        // Input runs only in play mode.
        if (!Application.isPlaying) {
            ShowTrace();
            return;
        }

        // Accumulate left/right travel: MouseDelta is per-pixel, the held modes per-second.
        updateOffset(preset.moveInput);

        // In radial mode offset is degrees; clamp so it can't cross the pole and wrap round the back.
        if (preset.slideAxis == SlideAxis.RadialPerpendicular) {
            offset = Mathf.Clamp(offset, -radialMaxAngle, radialMaxAngle);
        }
    }

    /// <summary>Closest point on the target loop to <c>onMeshPos</c>, drawn by the precision gizmo.</summary>
    private Vector3 expected;

    /// <summary>Logs how far the snapped object sits from the target loop and records the nearest loop point for the gizmo.</summary>
    void calculatePrecision()
    {
        if (!builder.TryGetFlatLoop(out _, out List<Vector3> points)) return;

        expected = LoopScorer.ClosestPointOnPolyline(points, onMeshPos, out float t, out float dst);
        //Debug.Log(dst.ToString("0.000"));
    }

    void OnDrawGizmos()
    {
        if (owned == null) return;
        return;
        Color c = Gizmos.color;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(expected, 0.01f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(owned.BasePosition, 0.01f);
        Gizmos.color = c;
    }

    /// <summary>Snaps the object onto the mesh surface each frame, after its orbit position is known.</summary>
    void LateUpdate()
    {
        if (!Application.isPlaying || builder == null || owned == null) return;

        // Both modes travel along the limb; they differ in how the surface point is found.
        Vector3 rawPos;   // free-space sample the ray starts from
        Vector3 projDir;  // outward direction to project along (-projDir shoots into the surface)

        if (preset.slideAxis == SlideAxis.PlaneNormal) {
            // Slide along the limb axis, then project radially inward.
            rawPos = owned.BasePosition + builder.PlaneNormal * offset;
            projDir = (rawPos - builder.LoopCenter).normalized;
        } else {
            // In-plane radial: drop any lift along the plane normal so the sweep starts on the loop.
            Vector3 fromCenter = owned.BasePosition - builder.LoopCenter;
            Vector3 radialIn = fromCenter - builder.PlaneNormal * Vector3.Dot(fromCenter, builder.PlaneNormal);

            // Rotate the in-plane radial about the tangent by offset degrees.
            Vector3 axis = Vector3.Cross(builder.PlaneNormal, radialIn).normalized;
            projDir = (Quaternion.AngleAxis(-offset, axis) * radialIn).normalized;
            rawPos = builder.LoopCenter + projDir * radialIn.magnitude;
        }

        // Ray search length from rawPos's own distance to the centre, so a large offset still
        // reaches the surface.
        float reach = (rawPos - builder.LoopCenter).magnitude;

        if (builder.TryProjectOntoSurface(rawPos, projDir, 1.3f * reach, out onMeshPos, out Vector3 onMeshNormal)) {
            // Low-pass the flat per-triangle collider normal so the hover lift doesn't step.
            smoothedNormal = hasNormal
                ? Vector3.Slerp(smoothedNormal, onMeshNormal, 1f - Mathf.Exp(-preset.normalSmooth * Time.deltaTime))
                : onMeshNormal;
            hasNormal = true;

            lastSurfacePos = onMeshPos + smoothedNormal * (ObjectHover * BodyScale);
            lastTracePos = onMeshPos + smoothedNormal * (traceHover * BodyScale);
            hasSurface = true;
        }
        // Miss: keep lastSurfacePos instead of snapping out to free space.

        if (!hasSurface) return;

        // Ease onto the target; the exp factor keeps it framerate-independent.
        owned.transform.position = preset.followSmooth > 0f
            ? Vector3.Lerp(owned.transform.position, lastSurfacePos, 1f - Mathf.Exp(-preset.followSmooth * Time.deltaTime))
            : lastSurfacePos;

        // Trail the surface point itself at the trace's own hover, not the scalpel: the line is what the
        // cut left on the body, and it has to sit where the edit-mode preview said it would.
        if (drawTrace && EnsureTraceRenderer() != null) AddTracePoint(lastTracePos);
        calculatePrecision();
    }


    void AddTracePoint(Vector3 p)
    {
        int n = tracePoints.Count;
        if (n == 0)
        {
            AppendTracePoint(p);
            return;
        }

        Vector3 last = tracePoints[n - 1];
        float distance = Vector3.Distance(last, p);

        if (distance < traceMinStep) return;

        if (traceMaxStep > 0f && distance > traceMaxStep)
        {
            int fill = Mathf.Min(Mathf.CeilToInt(distance / traceMaxStep) - 1, Mathf.Max(traceMaxFillPerFrame, 0));
            for (int i = 1; i <= fill; i++)
            {
                AppendTracePoint(Vector3.Lerp(last, p, i / (float)(fill + 1)));
            }
        }

        AppendTracePoint(p);
    }

    /// <summary>Stores one point and shows it, without any spacing rule.</summary>
    /// <remarks><c>tracePoints</c> keeps the world position so the spacing rules stay in world units; the
    /// renderer is fed the same point in its own space, which is the body's when the line rides it.</remarks>
    void AppendTracePoint(Vector3 p)
    {
        tracePoints.Add(p);
        traceRenderer.positionCount = tracePoints.Count;
        traceRenderer.SetPosition(tracePoints.Count - 1, ToRendererSpace(p));
    }

    void ApplyTraceWidth()
    {
        if (traceRenderer == null) return;

        float width = traceWidth * BodyScale;

        // nothing to write: this is reached from the edit-mode preview, where an unconditional write
        // would register an undo step and dirty the line every frame the inspector repaints.
        if (Mathf.Approximately(traceRenderer.widthMultiplier, 1f)
            && traceRenderer.widthCurve.length == 1
            && Mathf.Approximately(traceRenderer.widthCurve[0].value, width))
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(traceRenderer, "Set scalpel trace width");
        }
#endif

        traceRenderer.widthCurve = AnimationCurve.Constant(0, 1, width);

        // the inspector's "Width" field is this multiplier, and the final width is curve x multiplier:
        // left at anything but 1 it silently scales every value typed above.
        traceRenderer.widthMultiplier = 1f;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(traceRenderer);
        }
#endif
    }

    /// <summary>Drops the trail and the along-limb offset, so a fresh run starts from a clean surface and a centred scalpel.</summary>
    [ContextMenu("reset trace points")]
    public void ResetTrace()
    {
        tracePoints.Clear();
        if (traceRenderer != null)
        {
            // open again: the edit-mode preview closes the ring, and a run left closed would draw a
            // chord across the body from the scalpel back to where the cut started.
            traceRenderer.loop = false;
            traceRenderer.positionCount = 0;
        }

        offset = 0f;
        hasSurface = false;
        hasNormal = false;
    }

    /// <summary>Accumulates along-limb travel into <c>offset</c> from the active input.</summary>
    private void updateOffset(MoveInput moveInput)
    {
        bool hasMouse = Mouse.current != null;
        switch (moveInput) {
            case MoveInput.MouseDelta:
                if (GameInputActions.MouseDelta != null)
                    offset -= GameInputActions.MouseDelta.ReadValue<Vector2>().x * preset.Xspeed;
                break;
            case MoveInput.MouseButtons: {
                if (!hasMouse) break;
                float dir = (Mouse.current.rightButton.isPressed ? 1f : 0f)
                          - (Mouse.current.leftButton.isPressed ? 1f : 0f);
                offset -= dir * preset.Xspeed * Time.deltaTime;
                break;
            }
            case MoveInput.ArrowKeys:
                offset -= GameInputActions.Arrows.ReadValue<Vector2>().x * preset.Xspeed * Time.deltaTime;
                break;
        }
    }
}
