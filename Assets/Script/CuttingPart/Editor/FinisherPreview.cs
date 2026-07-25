using UnityEditor;
using UnityEngine;

/// <summary>Runs a <see cref="CutFinisher"/> in edit mode so its framing and swing can be judged without pressing play.</summary>
/// <remarks>
/// Invariant: nothing is sliced — the severed piece is drawn as an overlay, and the body's mesh is
/// never reassigned.
/// <para>Invariant: the scene camera is moved and restored on stop, and only one preview holds it
/// at a time.</para>
/// <para>Invariant: the beat runs on the finisher's own durations in real seconds, so a duration
/// edited while it runs is visible on the next tick.</para>
/// </remarks>
public static class FinisherPreview
{
    /// <summary>Where in the beat the preview is.</summary>
    public enum Beat
    {
        /// <summary>Camera flying to the shot.</summary>
        EaseIn,
        /// <summary>Tool bobbing, waiting for the click.</summary>
        Wait,
        /// <summary>The swing.</summary>
        Slash,
        /// <summary>Holding on the aftermath.</summary>
        Hold,
    }

    /// <summary>Identity handed to <see cref="EditorCameraClaim"/>, kept static so a finisher deleted mid-preview still releases the camera.</summary>
    private static readonly object ClaimOwner = new object();

    /// <summary>The finisher being previewed, or <c>null</c> when nothing is running.</summary>
    public static CutFinisher Active { get; private set; }

    /// <summary>Whether a preview is up, paused or not.</summary>
    public static bool IsRunning => Active != null;

    /// <summary>Whether the beat is advancing rather than sitting where it was scrubbed to.</summary>
    public static bool Playing { get; private set; }

    /// <summary>Replays from the top when the beat ends instead of stopping on the hold.</summary>
    public static bool Loop = true;

    /// <summary>Playback rate, where <c>1</c> is real time.</summary>
    public static float TimeScale = 1f;

    /// <summary>Seconds to sit on the wait when <c>AutoSlashAfter</c> is <c>0</c>, which would otherwise wait indefinitely.</summary>
    public static float PreviewWaitSeconds = 1.5f;

    /// <summary>Seconds into the beat.</summary>
    public static float Elapsed { get; private set; }

    /// <summary>Which part of the beat <see cref="Elapsed"/> lands in.</summary>
    public static Beat Phase { get; private set; }

    private static double lastTickTime;

    /// <summary>Camera pose the ease-in starts from: wherever the camera was when the preview took it.</summary>
    private static Vector3 fromPos;
    private static Quaternion fromRot;
    private static float fromFOV;

    /// <summary>The body whose highlight is lit, tracked separately so stopping can clear it after the finisher is gone.</summary>
    private static CuttableObject litBody;

    // ---- the beat's real durations, read live so editing one moves the timeline now ----

    /// <summary>Seconds the camera takes to reach the shot.</summary>
    public static float EaseInDuration => Active != null ? Mathf.Max(0f, Active.EaseIn) : 0f;

    /// <summary>Seconds spent waiting for the click: the finisher's own timeout, or <see cref="PreviewWaitSeconds"/> when it has none.</summary>
    public static float WaitDuration
    {
        get
        {
            if (Active == null) return 0f;
            float timeout = Active.AutoSlashAfter;
            return timeout > 0f ? timeout : Mathf.Max(0f, PreviewWaitSeconds);
        }
    }

    /// <summary>Seconds the swing takes.</summary>
    public static float SlashDuration => Active != null ? Mathf.Max(0f, Active.SlashTime) : 0f;

    /// <summary>Seconds held on the aftermath.</summary>
    public static float HoldDuration => Active != null ? Mathf.Max(0f, Active.HoldAfter) : 0f;

    public static float TotalDuration => EaseInDuration + WaitDuration + SlashDuration + HoldDuration;

    /// <summary>When the blade reaches the cut, in seconds from the top of the beat.</summary>
    public static float ImpactTime => EaseInDuration + WaitDuration + SlashDuration * CutFinisher.ImpactT;

    /// <summary>Whether the wait length is the finisher's own timeout rather than the preview's stand-in.</summary>
    public static bool WaitIsAuthored => Active != null && Active.AutoSlashAfter > 0f;

    /// <summary>Starts previewing a finisher, stopping whatever was previewing before.</summary>
    public static void Start(CutFinisher finisher)
    {
        if (finisher == null || Application.isPlaying)
        {
            return;
        }

        Stop();

        CuttingManager manager = finisher.Manager;
        if (manager == null)
        {
            Debug.LogError($"{finisher.name}: no CuttingManager to preview against.", finisher);
            return;
        }

        Active = finisher;
        Playing = true;
        Elapsed = 0f;
        Phase = Beat.EaseIn;
        lastTickTime = EditorApplication.timeSinceStartup;

        EditorCameraClaim.Claim(ClaimOwner, Stop, finisher.name);

        // orbit off: this preview poses the camera itself, and an enabled orbit would overwrite it
        // every tick
        manager.EnterFinisherPreview();

        // captured after the claim, so the ease starts from the pose the cut hands over
        Camera cam = manager.SceneCamera;
        if (cam != null)
        {
            fromPos = cam.transform.position;
            fromRot = cam.transform.rotation;
            fromFOV = cam.fieldOfView;
        }

        // temporary, so a preview cannot leave a tool behind in the scene
        finisher.EnsureTool(true);

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        Apply();
    }

    /// <summary>Stops the preview and puts back everything it touched, doing nothing when none is running.</summary>
    public static void Stop()
    {
        EditorApplication.update -= Tick;

        CutFinisher finisher = Active;
        Active = null;
        Playing = false;

        // before the null check, so a finisher deleted mid-preview still frees the claim
        EditorCameraClaim.ReleaseIfHeldBy(ClaimOwner);

        if (litBody != null)
        {
            CutRegionHighlighter highlighter = CutRegionHighlighter.For(litBody);
            if (highlighter != null) highlighter.Hide();
        }
        litBody = null;

        if (finisher == null)
        {
            return;
        }

        finisher.ReleaseTool();

        if (finisher.Manager != null)
        {
            finisher.Manager.ExitFinisherPreview();
        }

        // the scene's dirty flag is left alone: clearing it would wipe every pending edit, not just
        // this preview's
        SceneView.RepaintAll();
    }

    /// <summary>Pauses or resumes the beat without giving the camera back.</summary>
    public static void SetPlaying(bool playing)
    {
        Playing = playing;
        lastTickTime = EditorApplication.timeSinceStartup;
    }

    /// <summary>Jumps to a point in the beat and pauses there.</summary>
    public static void ScrubTo(float seconds)
    {
        if (!IsRunning)
        {
            return;
        }

        Playing = false;
        Elapsed = Mathf.Clamp(seconds, 0f, TotalDuration);
        Apply();
    }

    /// <summary>Jumps straight to the frame the blade reaches the cut.</summary>
    public static void ScrubToImpact()
    {
        ScrubTo(ImpactTime);
    }

    private static void Tick()
    {
        // the finisher can be deleted, or the scene unloaded, while a preview is up
        if (Active == null)
        {
            Stop();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastTickTime);
        lastTickTime = now;

        if (Playing)
        {
            Elapsed += delta * Mathf.Max(0f, TimeScale);

            float total = TotalDuration;
            if (Elapsed >= total)
            {
                if (Loop)
                {
                    // wrapped, so a long frame does not swallow the top of the beat
                    Elapsed = total > 0f ? Mathf.Repeat(Elapsed, total) : 0f;
                }
                else
                {
                    Elapsed = total;
                    Playing = false;
                }
            }
        }

        Apply();
    }

    /// <summary>Writes one frame of the preview: camera, tool, and the highlight of the piece about to come away.</summary>
    private static void Apply()
    {
        CutFinisher finisher = Active;
        if (finisher == null)
        {
            return;
        }

        CuttingManager manager = finisher.Manager;

        float easeInDur = EaseInDuration;
        float waitDur = WaitDuration;
        float slashDur = SlashDuration;

        float e = Mathf.Clamp(Elapsed, 0f, TotalDuration);

        float easeT;   // 0..1 through the camera's move to the shot
        float toolT;   // what TryGetToolPose wants: below 0 waits, 0..1 swings

        if (e < easeInDur)
        {
            Phase = Beat.EaseIn;
            easeT = easeInDur > 0f ? e / easeInDur : 1f;
            toolT = -1f;
        }
        else if (e < easeInDur + waitDur)
        {
            Phase = Beat.Wait;
            easeT = 1f;
            toolT = -1f;
        }
        else if (e < easeInDur + waitDur + slashDur)
        {
            Phase = Beat.Slash;
            easeT = 1f;
            toolT = slashDur > 0f ? (e - easeInDur - waitDur) / slashDur : 1f;
        }
        else
        {
            Phase = Beat.Hold;
            easeT = 1f;
            toolT = 1f;
        }

        ApplyCamera(finisher, manager, easeT);

        // Elapsed doubles as the bob clock, so scrubbing shows the bob where that instant puts it
        Transform tool = finisher.EnsureTool(true);
        if (tool != null && finisher.TryGetToolPose(toolT, e, out Vector3 toolPos, out Quaternion toolRot))
        {
            tool.SetPositionAndRotation(toolPos, toolRot);
        }

        ApplyHighlight(finisher, manager, toolT);

        SceneView.RepaintAll();

        // the Game view does not redraw on its own in edit mode, and it is the only view with the
        // real camera's framing
        EditorApplication.QueuePlayerLoopUpdate();
    }

    /// <summary>Eases the camera to the shot.</summary>
    private static void ApplyCamera(CutFinisher finisher, CuttingManager manager, float easeT)
    {
        Camera cam = manager != null ? manager.SceneCamera : null;
        if (cam == null)
        {
            return;
        }

        // re-read every tick, so dragging the handles or editing the FOV reframes live
        if (!finisher.TryGetCameraPose(out Vector3 shotPos, out Quaternion shotRot, out float shotFOV))
        {
            return;
        }

        AnimationCurve curve = finisher.EaseInCurve;
        float shaped = curve != null && curve.length > 0 ? curve.Evaluate(easeT) : easeT;

        cam.transform.SetPositionAndRotation(
            Vector3.Lerp(fromPos, shotPos, shaped),
            Quaternion.Slerp(fromRot, shotRot, shaped));
        cam.fieldOfView = Mathf.Lerp(fromFOV, shotFOV, shaped);
    }

    /// <summary>Lights the piece the swing is about to take, then slides it away once the blade is through.</summary>
    private static void ApplyHighlight(CutFinisher finisher, CuttingManager manager, float toolT)
    {
        CuttableObject body = manager != null ? manager.GameObjectBeingCut : null;
        if (body == null)
        {
            return;
        }

        CutRegionHighlighter highlighter = CutRegionHighlighter.For(body);
        if (highlighter == null)
        {
            return;
        }

        litBody = body;

        // nothing lit while the tool is still waiting
        if (toolT < 0f)
        {
            highlighter.Hide();
            return;
        }

        // cached against the plane and body poses, so this costs a matrix compare, not a re-slice
        Mesh severed = manager.SeveredPreviewMesh;
        if (severed == null)
        {
            highlighter.Hide();
            return;
        }

        highlighter.Show(severed, toolT >= CutFinisher.ImpactT ? Color.red : Color.green);

        // the mesh is in the body's local space, so the world-space push has to be brought into it
        highlighter.SetOffset(body.transform.InverseTransformVector(finisher.SeveredOffsetAt(toolT)));
    }
}
