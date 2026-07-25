using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs a cut's camera in edit mode so its framing can be judged and tuned without pressing play.</summary>
/// <remarks>
/// <see cref="CameraFollow"/> has no <c>ExecuteAlways</c>, so nothing orbits outside play mode. This
/// drives it from <see cref="EditorApplication.update"/> instead: the angle walks from the cut's
/// startAngle to its endAngle at a constant speed and loops, and the follow is placed with
/// <see cref="CameraFollow.PreviewAt"/> rather than being allowed to advance itself.
/// <para>
/// The scene camera really is moved -- that is the point, since only the real camera has the game's
/// FOV and aspect. Everything it disturbs is snapshotted on start and put back on stop, including
/// the scene's dirty flag, so previewing a clean scene does not force a save.
/// </para>
/// <para>
/// Setup and teardown go through <see cref="CuttingManager.EnterPreview"/> and
/// <see cref="CuttingManager.ExitPreview"/>, which share their bodies with the real cut's
/// SetupRig/RestoreRig. A preview that built its own version of the handover would drift out of
/// step and start lying about what play mode does.
/// </para>
/// </remarks>
[InitializeOnLoad]
public static class CutPreview
{
    /// <summary>The cut being previewed, or null when nothing is running.</summary>
    public static CuttingManager Active { get; private set; }

    /// <summary>Whether a preview is up. It may still be paused.</summary>
    public static bool IsRunning => Active != null;

    /// <summary>Whether the angle is advancing, as opposed to sitting where it was scrubbed to.</summary>
    public static bool Playing { get; private set; }

    /// <summary>Travel speed of the preview loop, in degrees per second. Deliberately constant -- the point is a repeatable sweep, not the player's variable speed.</summary>
    public static float Speed = 45f;

    /// <summary>Where the preview currently sits around the ring, in degrees.</summary>
    public static float Angle { get; private set; }

    /// <summary>Editor clock handed to the follow, so roll oscillation and pivot wander animate without a running Time.time.</summary>
    private static double clock;
    private static double lastTickTime;

    /// <summary>What the preview disturbed, so stopping can put it back.</summary>
    private static float savedCameraAngle;
    private static float savedScalpelAngle;
    private static float savedScalpelRotationSpeed;

    static CutPreview()
    {
        // a recompile drops every static below, taking the snapshot with it: stop while the
        // snapshot still exists, or the camera is stranded mid-orbit with no way back.
        AssemblyReloadEvents.beforeAssemblyReload += Stop;

        // entering play mode with the camera claimed would have the game start from the preview
        // pose rather than the authored one.
        EditorApplication.playModeStateChanged += _ => Stop();

        EditorSceneManager.sceneClosed += _ => Stop();
    }

    /// <summary>Starts previewing a cut, stopping whichever was previewing before.</summary>
    public static void Start(CuttingManager manager)
    {
        if (manager == null || Application.isPlaying)
        {
            return;
        }

        Stop();

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

        if (manager == null)
        {
            return;
        }

        manager.ExitPreview();

        if (manager.cameraFollow != null)
        {
            manager.cameraFollow.Angle = savedCameraAngle;
        }
        if (manager.scalpelFollow != null)
        {
            manager.scalpelFollow.Angle = savedScalpelAngle;
            manager.scalpelFollow.rotationSpeed = savedScalpelRotationSpeed;
        }

        // The scene's dirty flag is deliberately left alone. Clearing it would be nice -- a
        // preview only looks -- but ClearSceneDirtiness wipes ALL pending edits, so any real work
        // done while the preview was up would silently stop asking to be saved. Every value the
        // preview touched is restored above, so saving a scene that a preview dirtied is harmless.
        SceneView.RepaintAll();
    }

    /// <summary>Pauses or resumes the sweep without giving the camera back.</summary>
    public static void SetPlaying(bool playing)
    {
        Playing = playing;
        lastTickTime = EditorApplication.timeSinceStartup;
    }

    /// <summary>Jumps the preview to an angle and pauses there, so a specific spot can be examined.</summary>
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

    /// <summary>Advances the sweep and repaints. Registered on the editor's own update loop.</summary>
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

        // field of view and the framing-preset references, re-read every tick. Applied only on
        // entry they would look frozen: editing cameraFOV would do nothing until a restart.
        manager.RefreshLiveTuning();

        if (manager.cameraFollow != null)
        {
            // re-read the preset every tick: that is what makes editing a CameraFollowPreset
            // reshape the orbit live instead of only on restart.
            manager.cameraFollow.ApplyPreset();
            manager.cameraFollow.PreviewAt(Angle, (float)clock);
        }

        if (manager.scalpelFollow != null)
        {
            manager.scalpelFollow.ApplyPreset();
            manager.scalpelFollow.PreviewAt(Angle + manager.ScalpelAngleLead, (float)clock);
        }

        SceneView.RepaintAll();

        // the Game view does not redraw on its own in edit mode, and it is the only view with the
        // real camera's framing.
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
