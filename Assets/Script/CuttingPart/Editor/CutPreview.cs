using UnityEditor;
using UnityEngine;

/// <summary>Sweeps a cut's camera around its loop in edit mode so the framing can be judged without pressing play.</summary>
/// <remarks>
/// Invariant: the scene camera, both orbit angles and the scalpel's rotation speed are restored on
/// stop.
/// <para>Invariant: the sweep runs at a fixed speed rather than the player's variable travel, so two
/// passes over the same cut frame it identically.</para>
/// <para>Invariant: entry and exit run the same handover the real cut does, so the preview cannot
/// drift out of step with play mode.</para>
/// </remarks>
public static class CutPreview
{
    /// <summary>Identity handed to <see cref="EditorCameraClaim"/>, kept static so a manager deleted mid-preview still releases the camera.</summary>
    private static readonly object ClaimOwner = new object();

    /// <summary>The cut being previewed, or <c>null</c> when nothing is running.</summary>
    public static CuttingManager Active { get; private set; }

    /// <summary>Whether a preview is up, paused or not.</summary>
    public static bool IsRunning => Active != null;

    /// <summary>Whether the angle is advancing rather than sitting where it was scrubbed to.</summary>
    public static bool Playing { get; private set; }

    /// <summary>Sweep speed, in degrees per second.</summary>
    public static float Speed = 45f;

    /// <summary>Where the preview sits around the ring, in degrees.</summary>
    public static float Angle { get; private set; }

    /// <summary>Editor clock handed to the follow, so roll and pivot wander animate without a running <c>Time.time</c>.</summary>
    private static double clock;
    private static double lastTickTime;

    /// <summary>What the preview disturbed, so stopping can put it back.</summary>
    private static float savedCameraAngle;
    private static float savedScalpelAngle;
    private static float savedScalpelRotationSpeed;

    /// <summary>Starts previewing a cut, stopping whichever was previewing before.</summary>
    public static void Start(CuttingManager manager)
    {
        if (manager == null || Application.isPlaying)
        {
            return;
        }

        Stop();

        // takes the camera off whatever else was previewing it
        EditorCameraClaim.Claim(ClaimOwner, Stop, manager.name);

        Active = manager;
        Angle = manager.StartAngle;
        Playing = true;
        clock = 0d;
        lastTickTime = EditorApplication.timeSinceStartup;

        if (manager.cameraFollow != null)
        {
            savedCameraAngle = manager.cameraFollow.Angle;
        }
        if (manager.scalpelFollow != null)
        {
            savedScalpelAngle = manager.scalpelFollow.Angle;
            savedScalpelRotationSpeed = manager.scalpelFollow.rotationSpeed;
        }

        manager.EnterPreview();

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        Apply();
    }

    /// <summary>Stops the preview and puts back everything it touched. Safe to call when nothing is running.</summary>
    public static void Stop()
    {
        EditorApplication.update -= Tick;

        CuttingManager manager = Active;
        Active = null;
        Playing = false;

        // before the null check, so a manager deleted mid-preview still frees the claim
        EditorCameraClaim.ReleaseIfHeldBy(ClaimOwner);

        if (manager == null)
        {
            return;
        }

        manager.ExitPreview();

        // the whole ring back, so nothing is left showing a half-erased guide
        if (manager.loopGuide != null)
        {
            manager.loopGuide.ClearTrace();
        }

        if (manager.cameraFollow != null)
        {
            manager.cameraFollow.Angle = savedCameraAngle;
        }
        if (manager.scalpelFollow != null)
        {
            manager.scalpelFollow.Angle = savedScalpelAngle;
            manager.scalpelFollow.rotationSpeed = savedScalpelRotationSpeed;
        }

        // the scene's dirty flag is left alone: clearing it would wipe every pending edit, not just
        // this preview's
        SceneView.RepaintAll();
    }

    /// <summary>Pauses or resumes the sweep without giving the camera back.</summary>
    public static void SetPlaying(bool playing)
    {
        Playing = playing;
        lastTickTime = EditorApplication.timeSinceStartup;
    }

    /// <summary>Jumps the preview to an angle and pauses there.</summary>
    public static void ScrubTo(float degrees)
    {
        if (!IsRunning)
        {
            return;
        }
        Playing = false;
        Angle = degrees;
        Apply();
    }

    /// <summary>Advances the sweep by one editor frame and repaints.</summary>
    private static void Tick()
    {
        // the manager can be deleted, or the scene unloaded, while a preview is up
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
            clock += delta;
            Angle = Wrap(Angle + Speed * delta, Active.StartAngle, Active.EndAngle);
        }

        Apply();
    }

    /// <summary>Keeps an angle inside the cut's sweep, wrapping back to the start at the end.</summary>
    private static float Wrap(float degrees, float start, float end)
    {
        float span = end - start;
        if (Mathf.Abs(span) < 1e-3f)
        {
            return start;
        }
        return start + Mathf.Repeat(degrees - start, span);
    }

    /// <summary>Places both follows at the current angle and refreshes the views.</summary>
    private static void Apply()
    {
        CuttingManager manager = Active;
        if (manager == null)
        {
            return;
        }

        // re-read every tick, so editing the field of view or a framing preset reshapes the orbit
        // live rather than on a restart
        manager.RefreshLiveTuning();

        if (manager.cameraFollow != null)
        {
            manager.cameraFollow.ApplyPreset();
            manager.cameraFollow.PreviewAt(Angle, (float)clock);
        }

        if (manager.scalpelFollow != null)
        {
            manager.scalpelFollow.ApplyPreset();
            manager.scalpelFollow.PreviewAt(Angle + manager.ScalpelAngleLead, (float)clock);
        }

        // erased behind the sweep as play mode does, so the framing is judged against the line the
        // player actually sees
        if (manager.loopGuide != null)
        {
            float span = manager.EndAngle - manager.StartAngle;
            float traced = Mathf.Abs(span) > 1e-3f
                ? (Angle + manager.ScalpelAngleLead - manager.StartAngle) / span
                : 0f;
            manager.loopGuide.SetTraceProgress(manager.StartAngle, manager.EndAngle, traced);
        }

        SceneView.RepaintAll();

        // the Game view does not redraw on its own in edit mode, and it is the only view with the
        // real camera's framing
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
